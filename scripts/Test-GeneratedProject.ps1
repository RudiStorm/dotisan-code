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
& $PackageManager install --prefix $web.FullName
& $PackageManager run build --prefix $web.FullName
& $PackageManager test --prefix $web.FullName
