#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [string]$CompilerPath = $env:CSC_PATH,
    [string]$ManagedPath,
    [string]$ModRoot,
    [string]$CoreAssembly
)
$ErrorActionPreference = 'Stop'
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (!$ManagedPath) {
    foreach ($candidate in @('RimWorldWin64_Data/Managed', 'RimWorldLinux_Data/Managed', 'RimWorldMac.app/Contents/Resources/Data/Managed', '.')) {
        $directory = Join-Path $GameRoot $candidate
        if (Test-Path -LiteralPath (Join-Path $directory 'Assembly-CSharp.dll')) { $ManagedPath = $directory; break }
    }
}
if (!$ManagedPath -or !(Test-Path -LiteralPath (Join-Path $ManagedPath 'Assembly-CSharp.dll'))) { throw 'Specify -ManagedPath containing Assembly-CSharp.dll from the target game runtime.' }
$ManagedPath = (Resolve-Path -LiteralPath $ManagedPath).Path
if (!$CompilerPath) {
    if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install a .NET SDK, or provide -CompilerPath for Roslyn csc.exe.' }
    $sdkLines = @(& dotnet --list-sdks)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to query .NET SDKs. Specify -CompilerPath for Roslyn csc.exe or csc.dll.' }
    foreach ($line in $sdkLines) {
        if ($line -match '^(\S+) \[(.+)\]$') {
            $candidate = Join-Path $Matches[2] ($Matches[1] + '/Roslyn/bincore/csc.dll')
            if (Test-Path -LiteralPath $candidate) { $CompilerPath = $candidate }
        }
    }
}
if (!$CompilerPath) { throw 'Install a .NET SDK (the runtime alone is insufficient), or specify -CompilerPath for Roslyn supporting C# 9 or later.' }
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
if ($CoreAssembly) { $CoreAssembly = (Resolve-Path -LiteralPath $CoreAssembly).Path }
if (!$ModRoot) {
    $ModRoot = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Mod/About/About.xml')) { Join-Path $PSScriptRoot 'Mod' } else { $PSScriptRoot }
}
$ModRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ModRoot)
New-Item -ItemType Directory -Path (Join-Path $ModRoot 'Assemblies') -Force | Out-Null
$buildDirectory = Join-Path $PSScriptRoot 'Validation/Build'
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
$arguments = @('/nologo','/target:library','/langversion:9.0','/warnaserror+','/optimize+','/deterministic+','/nostdlib+')
$arguments += '/out:"' + (Join-Path $ModRoot 'Assemblies/NeighborCourtyards.Fuzzy.dll') + '"'
$arguments += '/pathmap:"' + $PSScriptRoot + '=/_/NeighborCourtyardsFuzzy"'
foreach ($file in Get-ChildItem -LiteralPath $ManagedPath -Filter '*.dll' | Sort-Object Name) {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
    catch [BadImageFormatException] { continue }
    $arguments += '/reference:"' + $file.FullName + '"'
}
if ($CoreAssembly) { $arguments += '/reference:"' + $CoreAssembly + '"' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Recurse -Filter '*.cs' -File | Sort-Object FullName)
if (!$sources.Count) { throw 'No C# sources found in Source/.' }
$arguments += $sources | ForEach-Object { '"' + $_.FullName + '"' }
$responseFile = Join-Path $buildDirectory 'compile.rsp'
[IO.File]::WriteAllLines($responseFile, [string[]]$arguments, [Text.UTF8Encoding]::new($false))
if ([IO.Path]::GetExtension($CompilerPath) -eq '.dll') { & dotnet $CompilerPath ('@' + $responseFile) *> "$buildDirectory/build.log" }
else { & $CompilerPath ('@' + $responseFile) *> "$buildDirectory/build.log" }
if ($LASTEXITCODE -ne 0) { Get-Content "$buildDirectory/build.log" -Tail 20; throw 'Fuzzy neighbor compilation failed.' }
Write-Output 'Built NeighborCourtyards.Fuzzy.dll.'
