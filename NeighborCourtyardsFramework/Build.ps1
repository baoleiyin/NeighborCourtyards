#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [Parameter(Mandatory)][string]$HarmonyAssembly,
    [string]$CompilerPath = $env:CSC_PATH,
    [string]$ManagedPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Mod/Assemblies')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$HarmonyAssembly = (Resolve-Path -LiteralPath $HarmonyAssembly).Path

if (!$ManagedPath) {
    foreach ($candidate in @('RimWorldWin64_Data/Managed', 'RimWorldLinux_Data/Managed', 'RimWorldMac.app/Contents/Resources/Data/Managed', '.')) {
        $directory = Join-Path $GameRoot $candidate
        if (Test-Path -LiteralPath (Join-Path $directory 'Assembly-CSharp.dll')) {
            $ManagedPath = $directory
            break
        }
    }
}
if (!$ManagedPath -or !(Test-Path -LiteralPath (Join-Path $ManagedPath 'Assembly-CSharp.dll'))) {
    throw 'Specify -ManagedPath containing Assembly-CSharp.dll and the matching game runtime references.'
}
$ManagedPath = (Resolve-Path -LiteralPath $ManagedPath).Path

if (!$CompilerPath) {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install a .NET SDK, or provide -CompilerPath for Roslyn csc.exe.' }
    $sdkLines = @(& dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to locate a .NET SDK. Specify -CompilerPath for Roslyn csc.exe or csc.dll.' }
    foreach ($line in $sdkLines) {
        if ($line -match '^(\S+) \[(.+)\]$') {
            $candidate = Join-Path $Matches[2] ($Matches[1] + '/Roslyn/bincore/csc.dll')
            if (Test-Path -LiteralPath $candidate) { $CompilerPath = $candidate }
        }
    }
}
if (!$CompilerPath) { throw 'Install a .NET SDK, or specify -CompilerPath for a Roslyn compiler supporting C# 9 or later.' }
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$buildDirectory = Join-Path $PSScriptRoot ('Validation/Build/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null

$references = @()
foreach ($file in Get-ChildItem -LiteralPath $ManagedPath -Filter '*.dll' | Sort-Object Name) {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
    catch [BadImageFormatException] { continue }
    if ($file.Name -eq '0Harmony.dll') { continue }
    $references += '/reference:"' + $file.FullName + '"'
}
$references += '/reference:"' + $HarmonyAssembly + '"'

foreach ($part in @('Core', 'Family')) {
    $output = Join-Path $buildDirectory ('NeighborCourtyards.' + $part + '.dll')
    $arguments = @('/nologo', '/target:library', '/langversion:9.0', '/optimize+', '/deterministic+', '/warn:4', '/warnaserror+', '/nostdlib+', ('/out:"' + $output + '"')) + $references
    $arguments += '/pathmap:"' + $PSScriptRoot + '=/_/NeighborCourtyardsFramework"'
    if ($part -eq 'Family') {
        $arguments += '/reference:"' + (Join-Path $buildDirectory 'NeighborCourtyards.Core.dll') + '"'
    }
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "Source/$part") -Filter '*.cs' | Sort-Object Name)
    $arguments += $sources | ForEach-Object { '"' + $_.FullName + '"' }
    $responseFile = Join-Path $buildDirectory ($part + '.rsp')
    [IO.File]::WriteAllLines($responseFile, [string[]]$arguments, [Text.UTF8Encoding]::new($false))
    $log = Join-Path $buildDirectory ($part + '.log')
    if ([IO.Path]::GetExtension($CompilerPath) -eq '.dll') {
        & dotnet $CompilerPath ('@' + $responseFile) *> $log
    } else {
        & $CompilerPath ('@' + $responseFile) *> $log
    }
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath $log -Tail 35
        throw "Compilation failed. Full log at $log"
    }
}

# Publish only after both assemblies have compiled successfully.
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach ($part in @('Core', 'Family')) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory "NeighborCourtyards.$part.dll") -Destination $OutputDirectory -Force
}
$manifest = [ordered]@{
    version = '0.8.0-dev.2'
    compiler = $CompilerPath
    gameReferences = $ManagedPath
    harmony = $HarmonyAssembly
    sourceHashes = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object {
        @{ path = $_.FullName.Substring($PSScriptRoot.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
    })
    assemblies = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter 'NeighborCourtyards.*.dll' | ForEach-Object {
        @{ file = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
    })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $buildDirectory 'manifest.json') -Encoding utf8
Write-Output "Built core and family assemblies. Manifest at $buildDirectory/manifest.json"
