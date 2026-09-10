param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $ProjectRoot,
    [Parameter(Position = 1)]
    [ValidateSet('npm', 'pnpm')]
    [string] $PackageManager = 'pnpm'
)

$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'

function Invoke-Checked {
    param([string] $FilePath, [string[]] $Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$FilePath failed with exit code $LASTEXITCODE." }
}

$solution = Get-ChildItem -LiteralPath $ProjectRoot -Filter '*.sln' -File | Select-Object -First 1
$web = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Directory -Filter '*.Web' | Select-Object -First 1
if ($null -eq $solution -or $null -eq $web) { throw "Generated solution or frontend was not found under $ProjectRoot." }

Invoke-Checked 'dotnet' @('restore', $solution.FullName)
Invoke-Checked 'dotnet' @('build', $solution.FullName, '-c', 'Release', '--no-restore', '--warnaserror')
Invoke-Checked 'dotnet' @('test', $solution.FullName, '-c', 'Release', '--no-build', '--no-restore', '--verbosity', 'minimal')

if ($PackageManager -eq 'pnpm') {
    Invoke-Checked 'pnpm.cmd' @('--dir', $web.FullName, 'install', '--frozen-lockfile')
    Invoke-Checked 'pnpm.cmd' @('--dir', $web.FullName, 'audit', '--audit-level', 'high')
    Invoke-Checked 'pnpm.cmd' @('--dir', $web.FullName, 'run', 'build')
    Invoke-Checked 'pnpm.cmd' @('--dir', $web.FullName, 'test')
    if (-not (Test-Path (Join-Path $web.FullName 'pnpm-lock.yaml'))) { throw 'Selected pnpm lockfile is missing.' }
}
else {
    Invoke-Checked 'npm.cmd' @('--prefix', $web.FullName, 'ci')
    Invoke-Checked 'npm.cmd' @('--prefix', $web.FullName, 'audit', '--omit=dev', '--audit-level=high')
    Invoke-Checked 'npm.cmd' @('--prefix', $web.FullName, 'run', 'build')
    Invoke-Checked 'npm.cmd' @('--prefix', $web.FullName, 'test')
    if (-not (Test-Path (Join-Path $web.FullName 'package-lock.json'))) { throw 'Selected npm lockfile is missing.' }
}
