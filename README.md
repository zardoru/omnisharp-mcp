# OmniSharp MCP Server

A Model Context Protocol (MCP) server that provides C# language intelligence to Codex and other MCP clients by wrapping the OmniSharp language server.

## What It Does

This MCP server gives MCP clients C# language and refactoring capabilities:

- **Find Symbols** - Search for classes, methods, properties by name
- **Go to Definition** - Jump to where a symbol is defined
- **Find References** - Find all usages of a symbol across the codebase
- **Find Implementations** - Find classes implementing an interface
- **Get Type Info** - Get type information and documentation
- **Get Diagnostics** - Get compiler errors and warnings
- **Code Completion** - Get autocomplete suggestions
- **Signature Help** - Get method parameter hints
- **Rename Preview** - Preview what a rename would change
- **Code Actions** - Discover available refactorings and quick fixes, preview their changes, and apply a selected action
- **Workspace Initialization** - Start OmniSharp and load the configured solution through an MCP tool
- **Decompiled Source** - View decompiled .NET framework code

## Prerequisites

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download) installed
- An MCP-compatible client, such as [Codex](https://openai.com/codex/) or Claude Code
- A C# solution file (`.sln`)

## Installation

### Quick bootstrap

The bootstrap scripts validate `dotnet`, locate the solution, restore and publish
the MCP server, then start it over stdio. On its first start, the MCP server
downloads the pinned OmniSharp HTTP release and starts it automatically.

Windows PowerShell:

```powershell
./bootstrap-omnisharp-mcp.ps1 -SolutionPath C:\path\to\project.sln
```

Linux/macOS:

```bash
./bootstrap-omnisharp-mcp.sh --solution /path/to/project.sln
```

If the current directory contains exactly one `.sln`, the solution argument can
be omitted. After the first build, use `-SkipBuild` or `--skip-build` for faster
starts. The scripts also accept `OMNISHARP_SOLUTION` and `OMNISHARP_PORT`.

The bootstrap script itself can be configured as the command of a stdio MCP
server. Keep all status/build output on stderr so stdout remains reserved for
the MCP protocol.

### 1. Clone and Build

```bash
git clone https://github.com/your-repo/omnisharp-mcp.git
cd omnisharp-mcp
dotnet publish src/OmniSharpMCP/OmniSharpMCP.csproj -c Release -o publish
```

### 2. Configure an MCP client

Add the server to the MCP client's configuration. The examples below show Codex and Claude Code; other clients that support stdio MCP servers can use the same command and environment variables.

#### Codex

Add an entry to `~/.codex/config.toml` (or the project's `.codex/config.toml`):

```toml
[mcp_servers.csharp]
command = "dotnet"
args = ["/path/to/omnisharp-mcp/publish/OmniSharpMCP.dll"]

[mcp_servers.csharp.env]
OMNISHARP_SOLUTION = "/path/to/your/project.sln"
```

Restart Codex or reload its MCP servers, then check that `csharp` is connected.

#### Claude Code

This configuration can be added per project.

A. Create `.mcp.json` in your project root:

```json
{
  "mcpServers": {
    "csharp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["/path/to/omnisharp-mcp/publish/OmniSharpMCP.dll"],
      "env": {
        "OMNISHARP_SOLUTION": "/path/to/your/project.sln"
      }
    }
  }
}
```

B. Edit the .claude/mcp.json file args and env values.

Example project configuration:
```json
{
  "mcpServers": {
    "csharp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["/path/to/omnisharp-mcp/publish/OmniSharpMCP.dll"],
      "env": {
        "OMNISHARP_SOLUTION": "/path/to/your/project.sln"
      }
    }
  }
}
```

### 3. Enable the MCP Server in Claude Code (if applicable)

Create (if not already created) and edit `~/.claude/settings.local.json`:

```json
{
  "enabledMcpjsonServers": ["csharp"],
  "enableAllProjectMcpServers": true
}
```

### 4. Restart the client

The MCP server starts when the client connects to it. For Claude Code, close its running instances before restarting if the server configuration changed.

Start Claude Code again:
```bash
claude
```

In Claude Code, verify the server is connected:

```
/mcp
```

You should see `csharp` in the list of connected servers.

Once connected, the MCP client can discover and call the server's tools. Some clients choose tools automatically based on the request; others let you call them directly.

## First Run

On first run, the MCP server will:

1. Download OmniSharp HTTP server (~50MB) to `~/.omnisharp-mcp/omnisharp/` if it is not already installed
2. Start OmniSharp and load your solution when the client connects or calls `initialize_workspace`

**Note:** The first startup takes 2-3 minutes while OmniSharp loads your solution. Tools will return errors during this time. Subsequent startups are faster if OmniSharp is already running.

## Warming Up OmniSharp (Optional)

To avoid waiting for OmniSharp to load, you can pre-start it with the script for your platform. Set `OMNISHARP_SOLUTION` to the solution path when it is not in the current directory. Both scripts also honor `OMNISHARP_PORT` (default `2050`) and `OMNISHARP_PATH` (custom `OmniSharp.dll` path).

```bash
./warmup-omnisharp.sh
```

On Windows, run:

```powershell
./warmup-omnisharp.ps1
```

This script:
- Starts OmniSharp if not already running
- Waits until it's fully loaded
- Keeps it running for instant tool availability

### Auto-Start on macOS Login

Create `~/Library/LaunchAgents/com.omnisharp.mcp.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.omnisharp.mcp</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/share/dotnet/dotnet</string>
        <string>/Users/YOUR_USERNAME/.omnisharp-mcp/omnisharp/OmniSharp.dll</string>
        <string>-s</string>
        <string>/path/to/your/project.sln</string>
        <string>-p</string>
        <string>2050</string>
    </array>
    <key>EnvironmentVariables</key>
    <dict>
        <key>DOTNET_ROLL_FORWARD</key>
        <string>LatestMajor</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/tmp/omnisharp.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/omnisharp.log</string>
</dict>
</plist>
```

Load it:

```bash
launchctl load ~/Library/LaunchAgents/com.omnisharp.mcp.plist
```

## Available Tools

Once connected, MCP clients can discover and use these tools:

| Tool | Description |
|------|-------------|
| `find_symbols` | Search for symbols by name pattern |
| `go_to_definition` | Navigate to symbol definition |
| `find_references` | Find all references to a symbol |
| `find_implementations` | Find interface implementations |
| `get_type_info` | Get type and documentation info |
| `get_diagnostics` | Get compiler errors/warnings for a file |
| `get_completions` | Get code completion suggestions |
| `get_signature_help` | Get method signature help |
| `get_file_members` | Get outline of a file |
| `get_workspace_info` | Get solution/project information |
| `preview_rename` | Preview rename changes |
| `get_code_actions` | List refactorings and quick fixes available at a file position or selection |
| `preview_code_action` | Preview the edits from a selected code action without applying them |
| `apply_code_action` | Apply a selected code action |
| `initialize_workspace` | Ensure OmniSharp is started and the configured solution is loaded |
| `get_decompiled_source` | Get decompiled metadata source |

## Architecture

```
Codex, Claude Code, or another MCP client
      |
      | (stdio - JSON-RPC)
      v
OmniSharp MCP Server (this project)
      |
      | (HTTP - REST API)
      v
OmniSharp HTTP Server (port 2050)
      |
      | (Roslyn)
      v
Your C# Solution
```

The MCP server acts as a bridge:
- Receives tool calls from an MCP client via stdio
- Translates them to HTTP requests to OmniSharp
- Returns formatted responses to the MCP client

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OMNISHARP_SOLUTION` | Path to your `.sln` file | Auto-detected or set with `initialize_workspace` |
| `OMNISHARP_PORT` | OmniSharp HTTP port | `2050` |
| `OMNISHARP_PATH` | Custom OmniSharp DLL path | Auto-download |

The bundled downloader is pinned to OmniSharp `v1.39.15`.

### Command Line Arguments

```bash
dotnet OmniSharpMCP.dll --solution /path/to/solution.sln --port 2050
```

## Troubleshooting

### MCP server fails to connect

1. Check the MCP server status in your client (for Claude Code, use `/mcp`)
2. Verify the server command and path in your client's MCP configuration
3. For Claude Code, ensure `settings.local.json` has `"csharp"` in `enabledMcpjsonServers`

### Tools return errors

OmniSharp may still be loading. Check status:

```bash
curl -X POST http://localhost:2050/checkreadystatus -d '{}'
```

If it returns `false`, wait for OmniSharp to finish loading. A ready server returns `true`.

### OmniSharp won't start

Check the log:

```bash
tail -f /tmp/omnisharp.log
```

Common issues:
- Solution file not found
- .NET SDK not installed
- Port 2050 already in use

### Symbol search returns empty

OmniSharp needs time to index. Large solutions (like Unity projects) can take 2-3 minutes.

### Reset OmniSharp

```bash
# Kill existing processes
pkill -f "omnisharp/OmniSharp.dll"

# Delete downloaded OmniSharp (will re-download on next start)
rm -rf ~/.omnisharp-mcp/omnisharp
```

## Development

### Building from Source

```bash
cd omnisharp-mcp
dotnet build src/OmniSharpMCP/OmniSharpMCP.csproj
```

### Running Locally

```bash
OMNISHARP_SOLUTION="/path/to/solution.sln" dotnet run --project src/OmniSharpMCP/OmniSharpMCP.csproj
```

### Testing OmniSharp Directly

```bash
# Find symbols
curl -X POST http://localhost:2050/findsymbols \
  -H "Content-Type: application/json" \
  -d '{"Filter": "MyClass"}'

# Check ready status
curl -X POST http://localhost:2050/checkreadystatus -d '{}'
```

## License

MIT
