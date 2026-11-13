#requires -Version 7.0
param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Check-Repository.ps1')
if (!$OutputPath) { $OutputPath = Join-Path $root ('Dist/NeighborCourtyards-Source-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip') }
$OutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) { throw 'Choose a new output path; an existing archive will not be replaced.' }
$files = @(& git -c core.quotepath=false -C $root ls-files --cached --others --exclude-standard | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository source.' }
New-Item -ItemType Directory -Path (Split-Path $OutputPath -Parent) -Force | Out-Null
$stream = [IO.File]::Open($OutputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($relative in $files) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $root $relative), ('NeighborCourtyards/' + $relative.Replace('\', '/')), [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
} finally { $stream.Dispose() }
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256 | Select-Object Path, Hash
