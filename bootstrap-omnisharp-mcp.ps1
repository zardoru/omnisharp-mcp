#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Builds and starts OmniSharp MCP; OmniSharp is downloaded and started automatically.

.EXAMPLE
  ./bootstrap-omnisharp-mcp.ps1 -SolutionPath ./MySolution.sln

.EXAMPLE
  # Use this script as the command of a stdio MCP server.
  ./bootstrap-omnisharp-mcp.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [string]$SolutionPath = $env:OMNISHARP_SOLUTION,
    [ValidateRange(1, 65535)]
    [int]$Port = $(if ($env:OMNISHARP_PORT) { [int]$env:OMNISHARP_PORT } else { 2050 }),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/OmniSharpMCP/OmniSharpMCP.csproj'
$publishPath = Join-Path $repoRoot 'publish'
$serverDll = Join-Path $publishPath 'OmniSharpMCP.dll'

function Write-Status([string]$Message) {
    [Console]::Error.WriteLine("[bootstrap] $Message")
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK was not found. Install .NET SDK 10 or newer and ensure dotnet is on PATH.'
}

$sdkMajor = [int]((& dotnet --version).Split('.')[0])
if ($sdkMajor -lt 10) {
    throw "This project targets .NET 10, but dotnet SDK $(& dotnet --version) was found. Install SDK 10 or newer."
}

if (-not $SolutionPath) {
    $solutions = @(Get-ChildItem -LiteralPath (Get-Location) -File -Filter '*.sln')
    if ($solutions.Count -eq 0 -and (Resolve-Path $repoRoot) -ne (Resolve-Path (Get-Location))) {
        $solutions = @(Get-ChildItem -LiteralPath $repoRoot -File -Filter '*.sln')
    }
    if ($solutions.Count -eq 1) {
        $SolutionPath = $solutions[0].FullName
    }
    elseif ($solutions.Count -eq 0) {
        throw 'No .sln file found. Pass -SolutionPath or set OMNISHARP_SOLUTION.'
    }
    else {
        throw "Multiple .sln files found. Pass -SolutionPath explicitly: $($solutions.Name -join ', ')"
    }
}

$SolutionPath = [System.IO.Path]::GetFullPath($SolutionPath)
if (-not (Test-Path -LiteralPath $SolutionPath -PathType Leaf) -or
    [System.IO.Path]::GetExtension($SolutionPath) -ine '.sln') {
    throw "Solution file not found or not a .sln: $SolutionPath"
}

if (-not $SkipBuild) {
    Write-Status "Publishing MCP server ($Configuration)..."
    & dotnet publish $projectPath -c $Configuration -o $publishPath |
        ForEach-Object { [Console]::Error.WriteLine($_) }
    $publishExitCode = $LASTEXITCODE
    if ($publishExitCode -ne 0) { throw "dotnet publish failed with exit code $publishExitCode." }
}
elseif (-not (Test-Path -LiteralPath $serverDll -PathType Leaf)) {
    throw "Published server not found at '$serverDll'. Run without -SkipBuild first."
}

$env:OMNISHARP_SOLUTION = $SolutionPath
$env:OMNISHARP_PORT = [string]$Port
Write-Status "Starting MCP for '$SolutionPath' on OmniSharp port $Port."
& dotnet $serverDll
exit $LASTEXITCODE
