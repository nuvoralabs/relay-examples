#!/usr/bin/env bash
# Restore, build (Release) and test every Relay sample.
# Requires the .NET 10 SDK on PATH. Some later samples' integration tests also require Docker.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
solution="${script_dir}/Relay.Samples.slnx"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: the .NET SDK ('dotnet') was not found on PATH. Install .NET 10 to build the samples." >&2
  exit 127
fi

echo "==> dotnet --version: $(dotnet --version)"
echo "==> restoring ${solution}"
dotnet restore "${solution}"
echo "==> building (Release)"
dotnet build "${solution}" --configuration Release --no-restore
echo "==> testing"
dotnet test "${solution}" --configuration Release --no-build
echo "==> done"
