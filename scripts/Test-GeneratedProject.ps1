[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [ValidateSet('npm', 'pnpm')][string]$PackageManager = 'pnpm'
)

$ErrorActionPreference = 'Stop'
$solution = Get-ChildItem -LiteralPath $ProjectRoot -Filter '*.sln' | Select-Object -First 1
$web = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Directory -Filter '*.Web' | Select-Object -First 1
if ($null -eq $solution -or $null -eq $web) { throw "Generated solution or frontend was not found under $ProjectRoot." }

dotnet restore $solution.FullName
dotnet build $solution.FullName -c Release --no-restore --warnaserror
dotnet test $solution.FullName -c Release --no-build --no-restore --verbosity minimal
if ($PackageManager -eq 'pnpm') {
    & pnpm --dir $web.FullName install --frozen-lockfile
    & pnpm --dir $web.FullName run build
    & pnpm --dir $web.FullName test
} else {
    & npm --prefix $web.FullName ci
    & npm --prefix $web.FullName run build
    & npm --prefix $web.FullName test
}
