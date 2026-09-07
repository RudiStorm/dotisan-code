[CmdletBinding()]
param([string]$OutputDirectory = "artifacts/release-check")

$ErrorActionPreference = 'Stop'
$requiredFiles = @('LICENSE', 'CHANGELOG.md', 'CONTRIBUTING.md', 'SECURITY.md', 'SUPPORT.md', '.github/ISSUE_TEMPLATE/bug.yml', '.github/ISSUE_TEMPLATE/feature.yml')
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Required release file is missing: $file" }
}

git diff --check
dotnet test Dotisan.sln -c Release --no-restore --filter 'FullyQualifiedName!~GoldenPathTests' --verbosity minimal

# Validate the user-facing artifact in a fresh temporary workspace before packing.
$acceptanceScript = Join-Path $PSScriptRoot 'Test-GeneratedProject.ps1'
& powershell -NoProfile -ExecutionPolicy Bypass -File $acceptanceScript -Profile minimal -PackageManager npm
if ($LASTEXITCODE -ne 0) { throw "Generated minimal-project acceptance failed with exit code $LASTEXITCODE." }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
dotnet pack src/Dotisan.Cli/Dotisan.Cli.csproj -c Release --no-restore --output $OutputDirectory --include-symbols
$package = Get-ChildItem -LiteralPath $OutputDirectory -Filter 'Dotisan.*.nupkg' | Where-Object { $_.Name -notlike '*.snupkg' } | Select-Object -First 1
$symbols = Get-ChildItem -LiteralPath $OutputDirectory -Filter 'Dotisan.*.snupkg' | Select-Object -First 1
if ($null -eq $package -or $null -eq $symbols) { throw 'Both the NuGet package and symbol package are required.' }
$nuspecName = tar -tf $package.FullName | Where-Object { $_ -like '*.nuspec' } | Select-Object -First 1
$nuspec = tar -xOf $package.FullName $nuspecName | Out-String
foreach ($tag in @('<repository ', '<license type="expression">MIT</license>', '<readme>README.md</readme>', '<tags>')) {
    if ($nuspec -notmatch [regex]::Escape($tag)) { throw "NuGet metadata is missing: $tag" }
}
Write-Host "Release gates passed for $($package.Name) and $($symbols.Name)."
