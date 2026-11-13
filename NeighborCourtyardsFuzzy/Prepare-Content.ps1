#requires -Version 7.0
param([Parameter(Mandatory)][string]$AtlasPath)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $AtlasPath).Path
if ([IO.Path]::GetExtension($source) -ne '.png') { throw 'Provide a transparent PNG atlas with three columns and two rows.' }
$modRoot = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Mod/About/About.xml')) { Join-Path $PSScriptRoot 'Mod' } else { $PSScriptRoot }
$destination = Join-Path $modRoot 'Textures/FuzzyNeighbor/Atlas.png'
New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($destination)) {
    Copy-Item -LiteralPath $source -Destination $destination -Force
}
Write-Output 'Prepared local atlas. Verify its layout and transparency in game; see Art/AssetScope.md.'
