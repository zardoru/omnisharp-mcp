---
name: setup-omnisharp-mcp
description: Configure the OmniSharp C# MCP server for a Codex project when the user asks to set up OmniSharp, C# language intelligence, Roslyn refactorings, or this repository's MCP server.
---

# Set up OmniSharp MCP for Codex

Configure the target repository without removing or replacing its existing MCP servers.

1. Resolve the target project directory from the user's request, or use the current working directory.
2. Find `.sln` files beneath that directory. If none exist, report that OmniSharp requires a solution and stop. If multiple exist and the intended one is not clear from repository guidance, ask which to use.
3. Resolve `dotnet` to an absolute executable path when possible. This MCP targets .NET 10; verify an SDK 10 or newer is available.
4. Locate a current published `OmniSharpMCP.dll`. Prefer the `publish` directory belonging to this skill's plugin or repository. If the source repository is available but the DLL is absent or stale, run its platform bootstrap or `dotnet publish` before configuration.
5. Update `<project>/.codex/config.toml`, preserving unrelated settings and MCP entries. Add or update:

   ```toml
   [mcp_servers.csharp]
   command = '<absolute-dotnet-path>'
   args = ['<absolute-OmniSharpMCP.dll-path>']

   [mcp_servers.csharp.env]
   OMNISHARP_SOLUTION = '<absolute-sln-path>'
   OMNISHARP_PORT = '2050'
   ```

   TOML literal strings are convenient for Windows paths because backslashes do not need escaping. Use a different free port only when `2050` is already assigned to another service.
6. Validate the TOML and confirm the configured files exist. When practical, run `codex mcp list` from the target project. Do not start an additional long-lived OmniSharp process merely to validate configuration.
7. Tell the user which solution, DLL, runtime, and port were configured. Explain that a new Codex session may be required to load the new MCP server.

Do not write Claude-specific configuration unless the user explicitly asks for Claude Code support.
