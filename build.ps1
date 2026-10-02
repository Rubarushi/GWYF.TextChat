param(
    [string]$GameDir = (Split-Path $PSScriptRoot -Parent),
    [string]$BepInExDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'BepInEx\core')
)
$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'TextChat.csproj') -c Release --configfile (Join-Path $PSScriptRoot 'NuGet.Config') "-p:GameDir=$GameDir" "-p:BepInExDir=$BepInExDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$manifest = Get-Content (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$stage = Join-Path $PSScriptRoot 'dist\package'
$plugin = Join-Path $stage 'BepInEx\plugins\TextChat'
New-Item -ItemType Directory -Force $plugin | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'bin\Release\netstandard2.1\GWYF.TextChat.dll') $plugin -Force
foreach ($file in @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png')) {
    Copy-Item (Join-Path $PSScriptRoot $file) $stage -Force
}
$zip = Join-Path $PSScriptRoot "dist\TextChat-$($manifest.version_number).zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Output "Package: $zip"
