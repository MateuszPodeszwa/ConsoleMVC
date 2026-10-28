#!/usr/bin/env bash
# Packs the framework and the testing package into artifacts/feed and runs the acceptance tests against them.
# Usage (from anywhere): tests/ConsoleMVC.AcceptanceTests/run.sh [extra dotnet test arguments]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
rm -rf "$root/artifacts/feed" "$root/artifacts/packages/consolemvc.framework" "$root/artifacts/packages/consolemvc.testing"
dotnet pack "$root/src/ConsoleMVC/ConsoleMVC.csproj" --configuration Release --output "$root/artifacts/feed"
dotnet pack "$root/src/ConsoleMVC.Testing/ConsoleMVC.Testing.csproj" --configuration Release --output "$root/artifacts/feed"
dotnet test "$root/tests/ConsoleMVC.AcceptanceTests" "$@"
