param([Parameter(Mandatory)][string]$InstalledCoreRoot, [string]$HelperIconsPath)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $InstalledCoreRoot).Path
$about = [xml][IO.File]::ReadAllText((Join-Path $source 'About/About.xml'))
if ($about.ModMetaData.packageId -ne 'local.neighborcourtyards.core') { throw 'Select an installed Neighbor Courtyards core directory.' }
$textures = @('Textures/CourtyardUI/EmotionIcons.png', 'Textures/CourtyardUI/IconsBold.png')
foreach ($relative in $textures) {
    if (!(Test-Path -LiteralPath (Join-Path $source $relative))) { throw "Required runtime artwork is missing: $relative" }
}
foreach ($relative in $textures) {
    $destination = Join-Path $PSScriptRoot ('Mod/' + $relative)
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $destination -Force
}
if (!$HelperIconsPath -and (Test-Path -LiteralPath (Join-Path $source 'Textures/CourtyardUI/HelperIcons.png'))) {
    $HelperIconsPath = Join-Path $source 'Textures/CourtyardUI/HelperIcons.png'
}
if ($HelperIconsPath) {
    $icons = (Resolve-Path -LiteralPath $HelperIconsPath).Path
    Copy-Item -LiteralPath $icons -Destination (Join-Path $PSScriptRoot 'Mod/Textures/CourtyardUI/HelperIcons.png') -Force
}
Write-Output 'Prepared runtime atlases. HelperIcons is optional; the UI falls back to original icons when absent. See Docs/AssetScope.md.'
