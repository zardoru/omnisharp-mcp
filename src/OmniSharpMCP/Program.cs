using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using OmniSharpMCP;
using OmniSharpMCP.Tools;

// Get solution path from environment variable or command line
var solutionPath = Environment.GetEnvironmentVariable("OMNISHARP_SOLUTION");
var port = int.TryParse(Environment.GetEnvironmentVariable("OMNISHARP_PORT"), out var p) ? p : 2050;

if (string.IsNullOrEmpty(solutionPath))
{
    // Try to get from args
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--solution" || args[i] == "-s")
        {
            if (i + 1 < args.Length)
            {
                solutionPath = args[i + 1];
            }
        }
        else if (args[i] == "--port" || args[i] == "-p")
        {
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var argPort))
            {
                port = argPort;
            }
        }
    }
}

if (string.IsNullOrEmpty(solutionPath))
{
    // Auto-detect: find .sln files in current working directory
    var slnFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.sln");
    if (slnFiles.Length == 1)
    {
        solutionPath = slnFiles[0];
        Console.Error.WriteLine($"[OmniSharpMCP] Auto-detected solution: {solutionPath}");
    }
    else if (slnFiles.Length > 1)
    {
        Console.Error.WriteLine("[OmniSharpMCP] Multiple .sln files found; use initialize_workspace to pick one:");
        foreach (var sln in slnFiles)
        {
            Console.Error.WriteLine($"  - {sln}");
        }
    }
    else
    {
        // Keep the MCP server available so a client can initialize it dynamically.
        solutionPath = TryResolveFromLegacyClaudePlugins();
        if (string.IsNullOrEmpty(solutionPath))
        {
            Console.Error.WriteLine(
                "[OmniSharpMCP] No solution configured. Call initialize_workspace or set OMNISHARP_SOLUTION.");
        }
    }
}

if (!string.IsNullOrEmpty(solutionPath) && !File.Exists(solutionPath))
{
    Console.Error.WriteLine($"[OmniSharpMCP] Configured solution not found: {solutionPath}");
    solutionPath = null;
}

Console.Error.WriteLine($"[OmniSharpMCP] Solution: {solutionPath ?? "not initialized"}");
Console.Error.WriteLine($"[OmniSharpMCP] OmniSharp port: {port}");

// Create OmniSharp manager and client
var omnisharpManager = new OmniSharpManager(solutionPath, port);
var omnisharpClient = new OmniSharpClient(port);

// Build and run MCP server (start immediately so Claude Code can connect)
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(omnisharpClient);
builder.Services.AddSingleton(omnisharpManager);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Cleanup on exit
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    Console.Error.WriteLine("[OmniSharpMCP] Shutting down...");
    omnisharpManager.Stop();
    omnisharpClient.Dispose();
});

// Start OmniSharp in background after MCP server is ready (if not already running)
lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            if (string.IsNullOrEmpty(omnisharpManager.SolutionPath))
            {
                return;
            }

            // Check if OmniSharp is already running
            if (await omnisharpClient.CheckReadyAsync())
            {
                Console.Error.WriteLine("[OmniSharpMCP] OmniSharp is already running and ready.");
                return;
            }

            Console.Error.WriteLine("[OmniSharpMCP] Starting OmniSharp in background...");
            await omnisharpManager.StartAsync();
            Console.Error.WriteLine("[OmniSharpMCP] OmniSharp is ready.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[OmniSharpMCP] Failed to start OmniSharp: {ex.Message}");
            Console.Error.WriteLine("[OmniSharpMCP] Tools will not work until OmniSharp is running.");
        }
    });
});

Console.Error.WriteLine("[OmniSharpMCP] MCP server starting...");
await app.RunAsync();

// Backward-compatible discovery for existing plugin installs. Generic MCP clients
// normally use the current directory, OMNISHARP_SOLUTION, or initialize_workspace.
static string? TryResolveFromLegacyClaudePlugins()
{
    try
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var pluginsFile = Path.Combine(home, ".claude", "plugins", "installed_plugins.json");
        if (!File.Exists(pluginsFile))
        {
            return null;
        }

        // AppContext.BaseDirectory returns the publish/ dir; parent is the install path
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var installPath = Path.GetDirectoryName(baseDir);
        if (string.IsNullOrEmpty(installPath))
        {
            return null;
        }

        var json = File.ReadAllText(pluginsFile);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("plugins", out var plugins))
        {
            return null;
        }

        foreach (var pluginEntry in plugins.EnumerateObject())
        {
            foreach (var install in pluginEntry.Value.EnumerateArray())
            {
                if (!install.TryGetProperty("installPath", out var ip))
                {
                    continue;
                }

                var candidatePath = ip.GetString();
                if (candidatePath == null)
                {
                    continue;
                }

                // Normalize both paths for comparison
                var normalizedCandidate = Path.GetFullPath(candidatePath);
                var normalizedInstall = Path.GetFullPath(installPath);

                if (!string.Equals(normalizedCandidate, normalizedInstall, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!install.TryGetProperty("projectPath", out var pp))
                {
                    continue;
                }

                var projectPath = pp.GetString();
                if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
                {
                    continue;
                }

                var slnFiles = Directory.GetFiles(projectPath, "*.sln");
                if (slnFiles.Length == 1)
                {
                    Console.Error.WriteLine($"[OmniSharpMCP] Auto-detected solution via Claude plugins: {slnFiles[0]}");
                    return slnFiles[0];
                }
                else if (slnFiles.Length > 1)
                {
                    Console.Error.WriteLine("[OmniSharpMCP] Multiple .sln files found via Claude plugins:");
                    foreach (var sln in slnFiles)
                    {
                        Console.Error.WriteLine($"  - {sln}");
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[OmniSharpMCP] Claude plugins lookup failed: {ex.Message}");
    }

    return null;
}
