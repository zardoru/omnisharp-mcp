#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Starts OmniSharp for a solution and waits until its HTTP endpoint is ready.

.DESCRIPTION
  Configure OMNISHARP_SOLUTION, OMNISHARP_PORT, and OMNISHARP_PATH to match
  the MCP server. OMNISHARP_PATH should point to OmniSharp.dll.
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = $(if ($env:OMNISHARP_PORT) { [int]$env:OMNISHARP_PORT } else { 2050 }),
    [ValidateRange(1, 1800)]
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'

function Test-OmniSharpReady {
    param([int]$TargetPort)

    try {
        $result = Invoke-RestMethod -Method Post `
            -Uri "http://localhost:$TargetPort/checkreadystatus" `
            -ContentType 'application/json' -Body '{}' -TimeoutSec 3
        return ($result.Ready -eq $true)
    }
    catch {
        return $false
    }
}

if (Test-OmniSharpReady -TargetPort $Port) {
    Write-Host "OmniSharp is already running and ready on port $Port."
    exit 0
}

$solutionPath = $env:OMNISHARP_SOLUTION
if (-not $solutionPath) {
    $candidate = Get-ChildItem -LiteralPath (Get-Location) -File -Filter '*.sln' |
        Select-Object -First 1
    if ($candidate) { $solutionPath = $candidate.FullName }
}
if (-not $solutionPath) {
    throw 'Set OMNISHARP_SOLUTION to a .sln path, or run this script from a directory containing one.'
}
$solutionPath = [System.IO.Path]::GetFullPath($solutionPath)
if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Solution file not found: $solutionPath"
}

$omnisharpPath = $env:OMNISHARP_PATH
if (-not $omnisharpPath) {
    $omnisharpPath = Join-Path $env:USERPROFILE '.omnisharp-mcp/omnisharp/OmniSharp.dll'
}
$omnisharpPath = [System.IO.Path]::GetFullPath($omnisharpPath)
if (-not (Test-Path -LiteralPath $omnisharpPath -PathType Leaf)) {
    throw "OmniSharp.dll not found at '$omnisharpPath'. Start the MCP server once to download it, or set OMNISHARP_PATH."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET CLI (dotnet) was not found on PATH.'
}

$logBase = Join-Path ([System.IO.Path]::GetTempPath()) 'omnisharp-warmup'
$stdoutLog = "$logBase.out.log"
$stderrLog = "$logBase.err.log"
$arguments = @(
    "`"$omnisharpPath`"",
    '-s', "`"$solutionPath`"",
    '-p', "$Port",
    '--encoding', 'utf-8'
)

Write-Host "Starting OmniSharp for solution: $solutionPath"
Write-Host "Port: $Port"
$process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments `
    -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog
Write-Host "OmniSharp started (PID: $($process.Id)); waiting up to $TimeoutSeconds seconds..."

$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    if (Test-OmniSharpReady -TargetPort $Port) {
        Write-Host "OmniSharp is ready on port $Port."
        Write-Host "Logs: $stdoutLog and $stderrLog"
        exit 0
    }
    if ($process.HasExited) {
        throw "OmniSharp exited with code $($process.ExitCode). Check logs: $stdoutLog and $stderrLog"
    }
    Start-Sleep -Seconds 1
}

try { Stop-Process -Id $process.Id -Force -ErrorAction Stop } catch { }
throw "Timed out waiting for OmniSharp. Check logs: $stdoutLog and $stderrLog"
