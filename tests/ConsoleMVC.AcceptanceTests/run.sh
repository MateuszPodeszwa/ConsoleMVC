#!/usr/bin/env bash
# Packs the framework and the testing package into artifacts/feed and runs the acceptance tests against them.
# Usage (from anywhere): tests/ConsoleMVC.AcceptanceTests/run.sh [extra dotnet test arguments]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
# A package packed again under the same version must never be read from an older copy: clear the feed, the extracted
# packages and the test project's build output
rm -rf "$root/artifacts/feed" "$root/artifacts/packages/consolemvc.framework" "$root/artifacts/packages/consolemvc.testing" \
       "$root/tests/ConsoleMVC.AcceptanceTests/bin" "$root/tests/ConsoleMVC.AcceptanceTests/obj"
dotnet pack "$root/src/ConsoleMVC/ConsoleMVC.csproj" --configuration Release --output "$root/artifacts/feed"
dotnet pack "$root/src/ConsoleMVC.Testing/ConsoleMVC.Testing.csproj" --configuration Release --output "$root/artifacts/feed"
dotnet test "$root/tests/ConsoleMVC.AcceptanceTests" "$@"
