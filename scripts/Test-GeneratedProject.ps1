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
function Invoke-Checked([scriptblock]$Command, [string]$Description) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Description failed with exit code $LASTEXITCODE." }
}

try {
    if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
        $ProjectRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dotisan-acceptance-" + [Guid]::NewGuid().ToString('N'))
        $cliArguments = @('run', '--project', (Join-Path $PSScriptRoot '..\src\Dotisan.Cli'), '--', 'new', 'GeneratedAcceptance', '--profile', $Profile, '--package-manager', $PackageManager, '--yes', '--output', $ProjectRoot)
        Invoke-Checked { dotnet @cliArguments } 'dotisan new'
        $createdRoot = $true
    }
    $ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
    $solution = Get-ChildItem -LiteralPath $ProjectRoot -Filter '*.sln' | Select-Object -First 1
    $web = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src') -Directory -Filter '*.Web' | Select-Object -First 1
    if ($null -eq $solution -or $null -eq $web) { throw "Generated solution or frontend was not found under $ProjectRoot." }

    Invoke-Checked { dotnet restore $solution.FullName } 'dotnet restore'
    Invoke-Checked { dotnet build $solution.FullName -c Release --no-restore --warnaserror } 'dotnet build'
    Invoke-Checked { dotnet test $solution.FullName -c Release --no-build --no-restore --verbosity minimal } 'dotnet test'
    if ($PackageManager -eq 'pnpm') {
        Invoke-Checked { pnpm --dir $web.FullName install --frozen-lockfile } 'pnpm install'
        Invoke-Checked { pnpm --dir $web.FullName audit --audit-level high } 'pnpm audit'
        Invoke-Checked { pnpm --dir $web.FullName run build } 'pnpm build'
        Invoke-Checked { pnpm --dir $web.FullName test } 'pnpm test'
    } else {
        Invoke-Checked { npm --prefix $web.FullName ci } 'npm ci'
        Invoke-Checked { npm --prefix $web.FullName audit --audit-level=high } 'npm audit'
        Invoke-Checked { npm --prefix $web.FullName run build } 'npm build'
        Invoke-Checked { npm --prefix $web.FullName test } 'npm test'
    }
    $lockfileName = if ($PackageManager -eq 'pnpm') { 'pnpm-lock.yaml' } else { 'package-lock.json' }
    if (-not (Test-Path -LiteralPath (Join-Path $web.FullName $lockfileName))) {
        throw "Generated frontend did not produce the selected package-manager lockfile: $lockfileName"
    }
} finally {
    if ($createdRoot -and (Test-Path -LiteralPath $ProjectRoot)) {
        Remove-Item -LiteralPath $ProjectRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($null -eq $previousEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment }
}
