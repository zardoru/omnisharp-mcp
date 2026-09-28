using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OmniSharpMCP.Models;

namespace OmniSharpMCP.Tools;

[McpServerToolType]
public static class CodeActionsTool
{
    [McpServerTool(Name = "get_code_actions")]
    [Description("List Roslyn quick fixes and refactorings available at a C# position or selection. This does not modify files. Use the returned identifier with preview_code_action or apply_code_action.")]
    public static async Task<string> GetCodeActionsAsync(
        OmniSharpClient client,
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Selection start line (1-based)")] int line,
        [Description("Selection start column (1-based)")] int column,
        [Description("Selection end line (1-based); defaults to the start line")] int? endLine = null,
        [Description("Selection end column (1-based); defaults to the start column")] int? endColumn = null)
    {
        ValidateLocation(filePath, line, column, endLine, endColumn);
        var response = await client.GetCodeActionsAsync(
            Path.GetFullPath(filePath), line, column, endLine ?? line, endColumn ?? column);

        return JsonSerializer.Serialize(new
        {
            count = response?.CodeActions.Count ?? 0,
            actions = response?.CodeActions.Select(action => new
            {
                name = action.Name,
                identifier = action.Identifier,
                kind = action.CodeActionKind
            }) ?? []
        }, JsonOptions);
    }

    [McpServerTool(Name = "preview_code_action")]
    [Description("Preview every text and file operation for a Roslyn code action without applying it. Obtain the identifier from get_code_actions using the same selection.")]
    public static Task<string> PreviewCodeActionAsync(
        OmniSharpClient client,
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Opaque identifier returned by get_code_actions")] string identifier,
        [Description("Selection start line (1-based)")] int line,
        [Description("Selection start column (1-based)")] int column,
        [Description("Selection end line (1-based); defaults to the start line")] int? endLine = null,
        [Description("Selection end column (1-based); defaults to the start column")] int? endColumn = null) =>
        RunAsync(client, filePath, identifier, line, column, endLine, endColumn, applyChanges: false);

    [McpServerTool(Name = "apply_code_action")]
    [Description("Apply a Roslyn quick fix or refactoring to the workspace. Obtain the identifier from get_code_actions using the same file and selection. This can modify or rename files.")]
    public static Task<string> ApplyCodeActionAsync(
        OmniSharpClient client,
        [Description("Absolute path to the C# file")] string filePath,
        [Description("Opaque identifier returned by get_code_actions")] string identifier,
        [Description("Selection start line (1-based)")] int line,
        [Description("Selection start column (1-based)")] int column,
        [Description("Selection end line (1-based); defaults to the start line")] int? endLine = null,
        [Description("Selection end column (1-based); defaults to the start column")] int? endColumn = null) =>
        RunAsync(client, filePath, identifier, line, column, endLine, endColumn, applyChanges: true);

    private static async Task<string> RunAsync(
        OmniSharpClient client, string filePath, string identifier,
        int line, int column, int? endLine, int? endColumn, bool applyChanges)
    {
        ValidateLocation(filePath, line, column, endLine, endColumn);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("A code action identifier is required.", nameof(identifier));
        }

        var response = await client.RunCodeActionAsync(
            Path.GetFullPath(filePath), line, column, endLine ?? line, endColumn ?? column,
            identifier, applyChanges);

        var operations = response?.Changes ?? [];
        return JsonSerializer.Serialize(new
        {
            applied = applyChanges,
            operationCount = operations.Count,
            operations = operations.Select(FormatOperation)
        }, JsonOptions);
    }

    private static object FormatOperation(FileOperationResponse operation) => new
    {
        file = operation.FileName,
        modificationType = FormatModificationType(operation.ModificationType),
        newFile = operation.NewFileName,
        buffer = operation.Buffer,
        changes = operation.Changes?.Select(change => new
        {
            startLine = change.StartLine,
            startColumn = change.StartColumn,
            endLine = change.EndLine,
            endColumn = change.EndColumn,
            newText = change.NewText
        })
    };

    private static object? FormatModificationType(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt32(out var number) => number switch
        {
            0 => "Modified",
            1 => "Opened",
            2 => "Renamed",
            _ => number.ToString()
        },
        _ => null
    };

    private static void ValidateLocation(
        string filePath, int line, int column, int? endLine, int? endColumn)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("The C# file does not exist.", filePath);
        }

        if (line < 1 || column < 1 || endLine is < 1 || endColumn is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line), "Lines and columns are 1-based and must be positive.");
        }

        var resolvedEndLine = endLine ?? line;
        var resolvedEndColumn = endColumn ?? column;
        if (resolvedEndLine < line || (resolvedEndLine == line && resolvedEndColumn < column))
        {
            throw new ArgumentException("The selection end must not precede its start.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
