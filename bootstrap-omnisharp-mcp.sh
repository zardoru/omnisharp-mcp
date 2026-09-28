#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_PATH="$SCRIPT_DIR/src/OmniSharpMCP/OmniSharpMCP.csproj"
PUBLISH_PATH="$SCRIPT_DIR/publish"
SERVER_DLL="$PUBLISH_PATH/OmniSharpMCP.dll"
SOLUTION_PATH=${OMNISHARP_SOLUTION:-}
PORT=${OMNISHARP_PORT:-2050}
CONFIGURATION=Release
SKIP_BUILD=0

usage() {
    echo "Usage: $0 [--solution PATH] [--port PORT] [--configuration Debug|Release] [--skip-build]" >&2
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --solution|-s) [ "$#" -ge 2 ] || { usage; exit 2; }; SOLUTION_PATH=$2; shift 2 ;;
        --port|-p) [ "$#" -ge 2 ] || { usage; exit 2; }; PORT=$2; shift 2 ;;
        --configuration|-c) [ "$#" -ge 2 ] || { usage; exit 2; }; CONFIGURATION=$2; shift 2 ;;
        --skip-build) SKIP_BUILD=1; shift ;;
        --help|-h) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 2 ;;
    esac
done

command -v dotnet >/dev/null 2>&1 || {
    echo "The .NET SDK was not found. Install .NET SDK 10 or newer and ensure dotnet is on PATH." >&2
    exit 1
}

SDK_MAJOR=$(dotnet --version | cut -d. -f1)
if [ "$SDK_MAJOR" -lt 10 ]; then
    echo "This project targets .NET 10, but dotnet SDK $(dotnet --version) was found. Install SDK 10 or newer." >&2
    exit 1
fi

case "$PORT" in *[!0-9]*|'') echo "Port must be an integer." >&2; exit 2 ;; esac
if [ "$PORT" -lt 1 ] || [ "$PORT" -gt 65535 ]; then
    echo "Port must be between 1 and 65535." >&2
    exit 2
fi
case "$CONFIGURATION" in Debug|Release) ;; *) echo "Configuration must be Debug or Release." >&2; exit 2 ;; esac

if [ -z "$SOLUTION_PATH" ]; then
    set -- ./*.sln
    if [ "$1" = './*.sln' ]; then
        echo "No .sln file found. Pass --solution or set OMNISHARP_SOLUTION." >&2
        exit 1
    fi
    if [ "$#" -ne 1 ]; then
        echo "Multiple .sln files found. Pass --solution explicitly." >&2
        exit 1
    fi
    SOLUTION_PATH=$1
fi

case "$SOLUTION_PATH" in
    /*) ;;
    *) SOLUTION_PATH=$(CDPATH= cd -- "$(dirname -- "$SOLUTION_PATH")" && pwd)/$(basename -- "$SOLUTION_PATH") ;;
esac
if [ ! -f "$SOLUTION_PATH" ]; then
    echo "Solution file not found: $SOLUTION_PATH" >&2
    exit 1
fi
case "$SOLUTION_PATH" in *.sln) ;; *) echo "Solution must be a .sln file: $SOLUTION_PATH" >&2; exit 1 ;; esac

if [ "$SKIP_BUILD" -eq 0 ]; then
    echo "[bootstrap] Publishing MCP server ($CONFIGURATION)..." >&2
    dotnet publish "$PROJECT_PATH" -c "$CONFIGURATION" -o "$PUBLISH_PATH" >&2
elif [ ! -f "$SERVER_DLL" ]; then
    echo "Published server not found at '$SERVER_DLL'. Run without --skip-build first." >&2
    exit 1
fi

export OMNISHARP_SOLUTION="$SOLUTION_PATH"
export OMNISHARP_PORT="$PORT"
echo "[bootstrap] Starting MCP for '$SOLUTION_PATH' on OmniSharp port $PORT." >&2
exec dotnet "$SERVER_DLL"
