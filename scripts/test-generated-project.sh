#!/usr/bin/env bash
set -euo pipefail

project_root="${1:?project root is required}"
package_manager="${2:-pnpm}"
solution="$(find "$project_root" -maxdepth 1 -name '*.sln' -print -quit)"
web="$(find "$project_root/src" -maxdepth 1 -type d -name '*.Web' -print -quit)"
[[ -n "$solution" && -n "$web" ]] || { echo "Generated solution or frontend was not found under $project_root" >&2; exit 1; }

dotnet restore "$solution"
dotnet build "$solution" -c Release --no-restore --warnaserror
dotnet test "$solution" -c Release --no-build --no-restore --verbosity minimal
"$package_manager" install --prefix "$web"
"$package_manager" run build --prefix "$web"
"$package_manager" test --prefix "$web"
