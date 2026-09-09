#!/usr/bin/env bash
set -euo pipefail
export ASPNETCORE_ENVIRONMENT=Development

project_root="${1:?project root is required}"
package_manager="${2:-pnpm}"
solution="$(find "$project_root" -maxdepth 1 -name '*.sln' -print -quit)"
web="$(find "$project_root/src" -maxdepth 1 -type d -name '*.Web' -print -quit)"
[[ -n "$solution" && -n "$web" ]] || { echo "Generated solution or frontend was not found under $project_root" >&2; exit 1; }

dotnet restore "$solution"
dotnet build "$solution" -c Release --no-restore --warnaserror
dotnet test "$solution" -c Release --no-build --no-restore --verbosity minimal
if [[ "$package_manager" == "pnpm" ]]; then
  pnpm --dir "$web" install --frozen-lockfile
  pnpm --dir "$web" audit --audit-level high
  pnpm --dir "$web" run build
  pnpm --dir "$web" test
else
  npm --prefix "$web" ci
  npm --prefix "$web" audit --audit-level=high
  npm --prefix "$web" run build
  npm --prefix "$web" test
fi
if [[ "$package_manager" == "pnpm" ]]; then
  [[ -f "$web/pnpm-lock.yaml" ]] || { echo "Selected pnpm lockfile is missing" >&2; exit 1; }
else
  [[ -f "$web/package-lock.json" ]] || { echo "Selected npm lockfile is missing" >&2; exit 1; }
fi
