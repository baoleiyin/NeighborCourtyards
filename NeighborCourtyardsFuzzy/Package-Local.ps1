#requires -Version 7.0
param(
    [string]$CoreModRoot = (Join-Path $PSScriptRoot '../NeighborCourtyardsFramework/Mod'),
    [Parameter(Mandatory)][string]$EvidenceRun
)
$ErrorActionPreference = 'Stop'
$CoreModRoot = (Resolve-Path -LiteralPath $CoreModRoot).Path
$EvidenceRun = (Resolve-Path -LiteralPath $EvidenceRun).Path
if ((Get-Content (Join-Path $EvidenceRun 'result.txt') -TotalCount 1) -notmatch '^PASS ') { throw 'A passing validation run is required.' }
foreach ($relative in @('About/About.xml', 'Assemblies/NeighborCourtyards.Core.dll', 'Assemblies/NeighborCourtyards.Family.dll', 'Textures/CourtyardUI/EmotionIcons.png', 'Textures/CourtyardUI/IconsBold.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $CoreModRoot $relative))) { throw "Missing core runtime content: $relative. Build and prepare content before packaging." }
}
foreach ($relative in @('Mod/Assemblies/NeighborCourtyards.Fuzzy.dll', 'Mod/Textures/FuzzyNeighbor/Atlas.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $relative))) { throw "Missing fuzzy runtime content: $relative. Build and prepare content before packaging." }
}
foreach ($name in @('standing-four-directions.png', 'sleeping-four-directions.png', 'eating.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $EvidenceRun $name))) { throw "Missing evidence screenshot: $name" }
}
$dist = Join-Path $PSScriptRoot 'Dist'
$name = 'FuzzyNeighbor-0.2.0-LocalDemo-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$stage = Join-Path $dist $name
if (Test-Path -LiteralPath $stage) { throw 'Package destination already exists.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $CoreModRoot -Destination (Join-Path $stage 'NeighborCourtyardsCore') -Recurse
$fuzzy = Join-Path $stage 'NeighborCourtyardsFuzzy'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Mod') -Destination $fuzzy -Recurse
foreach ($part in @('Source', 'Art', 'Build.ps1', 'Prepare-Content.ps1', 'README.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $part) -Destination $fuzzy -Recurse
}
New-Item -ItemType Directory -Path "$fuzzy/Validation", "$stage/Evidence" -Force | Out-Null
foreach ($part in @('Results.md', 'HelperResults.md', 'FuzzyProbe.cs', 'HelperProbe.cs', 'LegacyFuzzyProbe.cs', 'Run-Checks.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Validation/$part") -Destination "$fuzzy/Validation"
}
foreach ($part in @('result.txt', 'standing-four-directions.png', 'sleeping-four-directions.png', 'eating.png')) {
    Copy-Item -LiteralPath (Join-Path $EvidenceRun $part) -Destination "$stage/Evidence"
}
Get-ChildItem -LiteralPath $EvidenceRun -Filter 'ui-*.png' | Copy-Item -Destination "$stage/Evidence"
$framework = Join-Path $PSScriptRoot '../NeighborCourtyardsFramework'
Copy-Item -LiteralPath (Join-Path $framework 'LICENSE') -Destination "$stage/NeighborCourtyardsCore/LICENSE"
Copy-Item -LiteralPath (Join-Path $framework 'Docs/AssetScope.md') -Destination "$stage/NeighborCourtyardsCore/AssetScope.md"
New-Item -ItemType Directory -Path "$stage/NeighborCourtyardsCore/Docs" -Force | Out-Null
foreach ($part in @('Helpers.md', 'HelperIcons.md', 'AssetScope.md')) {
    Copy-Item -LiteralPath (Join-Path $framework "Docs/$part") -Destination "$stage/NeighborCourtyardsCore/Docs"
}
# Keep the local package's asset-scope relative link valid at both entry points.
Copy-Item -LiteralPath (Join-Path $framework 'Docs/HelperIcons.md') -Destination "$stage/NeighborCourtyardsCore/HelperIcons.md"
@'
# 绒绒族本地试玩包

把 NeighborCourtyardsCore 与 NeighborCourtyardsFuzzy 两个目录放进独立测试游戏的 Mods，另行安装 Harmony。
同一模组配置只保留一个奇妙邻里核心版本。需要 RimWorld 1.7；本包在 Waylith/.NET 平铺运行时验证。
完整安装、改造、编译、功能与验证边界见 NeighborCourtyardsFuzzy/README.md。
Evidence 是直接输出的实机截图与测试结果，不是运行依赖。
代码 MIT；美术范围见两个模组中的说明。本包用于本地试玩，未作为公开发行包发布。
'@ | Set-Content -LiteralPath "$stage/README.md" -Encoding utf8
$manifest = foreach ($file in Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName) {
    (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetRelativePath($stage, $file.FullName).Replace('\', '/')
}
$manifest | Set-Content -LiteralPath "$stage/SHA256SUMS.txt" -Encoding utf8
$zip = Join-Path $dist ($name + '.zip')
Compress-Archive -Path "$stage/*" -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
Write-Output "Staged files: $stage"
