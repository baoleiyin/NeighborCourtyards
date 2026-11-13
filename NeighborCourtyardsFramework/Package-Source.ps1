#requires -Version 7.0
param([string]$OutputPath = (Join-Path $PSScriptRoot 'Dist/NeighborCourtyards-DeveloperSource-0.8.0-dev.1.zip'))
$ErrorActionPreference = 'Stop'
$OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $OutputPath -Parent) -Force | Out-Null
$files = @('LICENSE', '.gitignore', 'README.md', 'Build.ps1', 'New-Neighbor.ps1', 'Prepare-Content.ps1', 'Package-Source.ps1')
foreach ($folder in @('Source', 'Docs', 'Examples', 'Mod')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -Recurse -File |
        Where-Object Extension -In @('.cs', '.xml', '.md', '.json') |
        ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/') }
}
$files += @('Validation/FrameworkProbe.cs', 'Validation/LegacyProbe.cs', 'Validation/Run-Checks.ps1', 'Validation/Compare-Api.ps1', 'Validation/api-comparison.json', 'Validation/verification.json')
foreach ($relative in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot $relative))) { throw "Missing deliverable: $relative" }
}
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($relative in $files | Sort-Object -Unique) {
            $entry = $archive.CreateEntry('NeighborCourtyardsFramework/' + $relative, [IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            $inputStream = [IO.File]::OpenRead((Join-Path $PSScriptRoot $relative))
            try { $inputStream.CopyTo($entryStream) }
            finally { $inputStream.Dispose(); $entryStream.Dispose() }
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
Get-FileHash -LiteralPath $OutputPath | Select-Object Path, Hash
