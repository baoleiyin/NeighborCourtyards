#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9]+(?:[._][a-z0-9]+)+$')][string]$PackageId,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')][string]$Prefix,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Author
)
$ErrorActionPreference = 'Stop'
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw "Destination already exists: $destination" }
if ($PackageId -eq 'local.neighborcourtyards.core') { throw 'Choose a package ID belonging to your extension.' }
New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Examples/StarterNeighbor') -Destination $destination -Recurse
foreach ($file in Get-ChildItem -LiteralPath $destination -Recurse -Filter '*.xml') {
    $content = [IO.File]::ReadAllText($file.FullName).Replace('NCS_', $Prefix + '_').Replace('local.neighborcourtyards.starter', $PackageId)
    [IO.File]::WriteAllText($file.FullName, $content, [Text.UTF8Encoding]::new($false))
}
$aboutPath = Join-Path $destination 'About/About.xml'
$about = [xml][IO.File]::ReadAllText($aboutPath)
$about.ModMetaData.name = $Name
$about.ModMetaData.author = $Author
$about.Save($aboutPath)
Write-Output "Created $Name at $destination. Next edit Defs/Neighbors.xml and follow Docs/QuickStart.md."
