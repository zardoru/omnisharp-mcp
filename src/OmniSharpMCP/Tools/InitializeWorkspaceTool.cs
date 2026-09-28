using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace OmniSharpMCP.Tools;

[McpServerToolType]
public static class InitializeWorkspaceTool
{
    [McpServerTool(Name = "initialize_workspace")]
    [Description("Initialize or switch OmniSharp to a C# solution. Pass a .sln file or a directory containing exactly one .sln file. Use this when OMNISHARP_SOLUTION was not configured.")]
    public static async Task<string> InitializeWorkspaceAsync(
        OmniSharpManager manager,
        [Description("Absolute path to a .sln file or a directory containing one")] string path,
        CancellationToken cancellationToken = default)
    {
        var solutionPath = ResolveSolution(path);
        await manager.InitializeAsync(solutionPath, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            initialized = true,
            solutionPath = manager.SolutionPath,
            port = manager.Port
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    internal static string ResolveSolution(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A solution or directory path is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            if (!string.Equals(Path.GetExtension(fullPath), ".sln", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The file must be a Visual Studio .sln file.", nameof(path));
            }

            return fullPath;
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Workspace path not found: {fullPath}");
        }

        var solutions = Directory.GetFiles(fullPath, "*.sln", SearchOption.TopDirectoryOnly);
        return solutions.Length switch
        {
            1 => Path.GetFullPath(solutions[0]),
            0 => throw new FileNotFoundException($"No .sln file found in: {fullPath}"),
            _ => throw new InvalidOperationException(
                $"Multiple .sln files found in {fullPath}. Pass the desired solution explicitly: " +
                string.Join(", ", solutions.Select(Path.GetFileName)))
        };
    }
}
