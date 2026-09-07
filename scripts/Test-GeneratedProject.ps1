[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [ValidateSet('minimal', 'identity', 'saas', 'maximal', 'custom')][string]$Profile = 'minimal',
    [ValidateSet('npm', 'pnpm')][string]$PackageManager = 'pnpm'
)

$ErrorActionPreference = 'Stop'
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$createdRoot = $false
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dotisan-acceptance-" + [Guid]::NewGuid().ToString('N'))
    $cliArguments = @('run', '--project', (Join-Path $PSScriptRoot '..\src\Dotisan.Cli'), '--', 'new', 'GeneratedAcceptance', '--profile', $Profile, '--package-manager', $PackageManager, '--yes', '--output', $ProjectRoot)
    & dotnet @cliArguments
    if ($LASTEXITCODE -ne 0) { throw "dotisan new failed with exit code $LASTEXITCODE." }
    $createdRoot = $true
}
$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$solution = Get-ChildItem -LiteralPath $ProjectRoot -Filter '*.sln' | Select-Object -First 1
$web = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Directory -Filter '*.Web' | Select-Object -First 1
if ($null -eq $solution -or $null -eq $web) { throw "Generated solution or frontend was not found under $ProjectRoot." }

dotnet restore $solution.FullName
dotnet build $solution.FullName -c Release --no-restore --warnaserror
dotnet test $solution.FullName -c Release --no-build --no-restore --verbosity minimal
if ($PackageManager -eq 'pnpm') {
    & pnpm --dir $web.FullName install --frozen-lockfile
    & pnpm --dir $web.FullName audit --audit-level high
    & pnpm --dir $web.FullName run build
    & pnpm --dir $web.FullName test
} else {
    & npm --prefix $web.FullName ci
    & npm --prefix $web.FullName audit --audit-level=high
    & npm --prefix $web.FullName run build
    & npm --prefix $web.FullName test
}

if ($createdRoot -and (Test-Path -LiteralPath $ProjectRoot)) {
    Remove-Item -LiteralPath $ProjectRoot -Recurse -Force
}
if ($null -eq $previousEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment }
