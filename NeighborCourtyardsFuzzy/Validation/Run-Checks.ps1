param(
    [Parameter(Mandatory)][string]$GameRoot,
    [Parameter(Mandatory)][string]$HarmonyModRoot,
    [Parameter(Mandatory)][string]$CompilerPath,
    [string]$CoreModRoot,
    [string]$RunRoot = $PSScriptRoot,
    [switch]$Biotech,
    [switch]$Helpers,
    [int]$Width = 1280,
    [int]$Height = 800,
    [string]$Language = 'English',
    [string]$LegacySave,
    [int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
if ($LegacySave -and $Helpers) { throw 'Use LegacySave to test the preserved humanlike definition; do not combine with Helpers.' }
$repository = Split-Path $PSScriptRoot -Parent
$fuzzyModRoot = if (Test-Path -LiteralPath (Join-Path $repository 'Mod/About/About.xml')) { Join-Path $repository 'Mod' } else { $repository }
if (!$CoreModRoot) {
    $CoreModRoot = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot '../../NeighborCourtyardsFramework/Mod/About/About.xml')) {
        Join-Path $PSScriptRoot '../../NeighborCourtyardsFramework/Mod'
    } else { Join-Path $PSScriptRoot '../../NeighborCourtyardsCore' }
}
$CoreModRoot = (Resolve-Path -LiteralPath $CoreModRoot).Path
foreach ($relative in @('Assemblies/NeighborCourtyards.Core.dll', 'Assemblies/NeighborCourtyards.Family.dll', 'Textures/CourtyardUI/EmotionIcons.png', 'Textures/CourtyardUI/IconsBold.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $CoreModRoot $relative))) { throw "Core is not ready: $relative. Run its Build.ps1 and Prepare-Content.ps1 first." }
}
foreach ($relative in @('Assemblies/NeighborCourtyards.Fuzzy.dll', 'Textures/FuzzyNeighbor/Atlas.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $fuzzyModRoot $relative))) { throw "Fuzzy mod is not ready: $relative. Run its Build.ps1 and Prepare-Content.ps1 first." }
}
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
$HarmonyModRoot = (Resolve-Path -LiteralPath $HarmonyModRoot).Path
# This runner targets the local flat .NET runtime. Build.ps1 also accepts Unity Managed directories.
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'Assembly-CSharp.dll'))) { throw 'This validation runner requires a flat managed RimWorld runtime.' }
$RunRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RunRoot)
$profile = Join-Path $RunRoot ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
$game = Join-Path $profile 'game'
if ([IO.Path]::GetExtension($CompilerPath) -eq '.exe' -and (Join-Path $game 'Mods/NeighborCourtyardsCore/Assemblies/NeighborCourtyards.Core.dll').Length -ge 260) {
    throw 'Validation path is too long for a legacy csc.exe host. Use -RunRoot with a shorter output directory, or use a supported csc.dll compiler.'
}
New-Item -ItemType Directory -Path "$game/Mods/QA/About", "$game/Mods/QA/Assemblies", "$profile/Config", "$profile/Saves" -Force | Out-Null
if ($LegacySave) { Copy-Item -LiteralPath $LegacySave -Destination "$profile/Saves/LegacyBaseline.rws" }
foreach ($file in Get-ChildItem -LiteralPath $GameRoot -File | Where-Object Extension -In @('.dll', '.exe', '.json', '.bin', '.dat', '.config', '.pak', '.zip', '.txt', '.vdf')) {
    if ([IO.Path]::GetPathRoot($game) -eq [IO.Path]::GetPathRoot($file.FullName)) {
        New-Item -ItemType HardLink -Path (Join-Path $game $file.Name) -Target $file.FullName | Out-Null
    } else {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $game $file.Name)
    }
}
foreach ($part in @('Data', 'Rendering', 'vulkan')) {
    if (Test-Path -LiteralPath (Join-Path $GameRoot $part)) {
        New-Item -ItemType Junction -Path (Join-Path $game $part) -Target (Join-Path $GameRoot $part) | Out-Null
    }
}
New-Item -ItemType Junction -Path "$game/Mods/Harmony" -Target $HarmonyModRoot | Out-Null
Copy-Item -LiteralPath $CoreModRoot -Destination "$game/Mods/NeighborCourtyardsCore" -Recurse
# A packaged mod also contains Source/ and Validation/. Copy runtime content only,
# otherwise the new run directory would recursively copy itself in that layout.
New-Item -ItemType Directory -Path "$game/Mods/NeighborCourtyardsFuzzy" -Force | Out-Null
foreach ($part in @('About', 'Assemblies', 'Defs', 'Textures', 'Languages', 'Patches', 'LoadFolders.xml')) {
    $source = Join-Path $fuzzyModRoot $part
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination "$game/Mods/NeighborCourtyardsFuzzy" -Recurse }
}
[IO.File]::WriteAllText("$game/Mods/QA/About/About.xml", '<ModMetaData><name>Fuzzy neighbor QA</name><author>70</author><packageId>local.neighborcourtyards.fuzzyqa</packageId><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>')
$packages = @('brrainz.harmony', 'ludeon.rimworld')
if ($Biotech) { $packages += 'ludeon.rimworld.biotech' }
$packages += @('local.neighborcourtyards.core','local.neighborcourtyards.fuzzy','local.neighborcourtyards.fuzzyqa')
$packageXml = ($packages | ForEach-Object { "<li>$_</li>" }) -join ''
[IO.File]::WriteAllText("$profile/Config/ModsConfig.xml", "<ModsConfigData><version>1.7.4990</version><activeMods>$packageXml</activeMods><knownExpansions><li>ludeon.rimworld.royalty</li><li>ludeon.rimworld.ideology</li><li>ludeon.rimworld.biotech</li><li>ludeon.rimworld.anomaly</li><li>ludeon.rimworld.odyssey</li></knownExpansions></ModsConfigData>")
[IO.File]::WriteAllText("$profile/Config/Prefs.xml", "<PrefsData><volumeMaster>0</volumeMaster><screenWidth>$Width</screenWidth><screenHeight>$Height</screenHeight><fullscreen>False</fullscreen><uiScale>1</uiScale><adaptiveTrainingEnabled>False</adaptiveTrainingEnabled><ignoreSystemScaling>True</ignoreSystemScaling><devMode>True</devMode><langFolderName>$([Security.SecurityElement]::Escape($Language))</langFolderName><pauseOnLoad>True</pauseOnLoad></PrefsData>")
$references = @()
foreach ($file in Get-ChildItem -LiteralPath $GameRoot -Filter '*.dll') {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
    catch [BadImageFormatException] { continue }
    $references += '/reference:"' + $file.FullName + '"'
}
$references += '/reference:"' + (Join-Path $HarmonyModRoot '1.7/Assemblies/0Harmony.dll') + '"'
$references += '/reference:"' + "$game/Mods/NeighborCourtyardsCore/Assemblies/NeighborCourtyards.Core.dll" + '"'
$arguments = @('/nologo', '/target:library', '/langversion:9.0', '/warnaserror+', '/nostdlib+', ('/out:"' + "$game/Mods/QA/Assemblies/FuzzyQA.dll" + '"')) + $references
$arguments += '"' + (Join-Path $PSScriptRoot $(if ($LegacySave) { 'LegacyFuzzyProbe.cs' } elseif ($Helpers) { 'HelperProbe.cs' } else { 'FuzzyProbe.cs' })) + '"'
[IO.File]::WriteAllLines("$profile/probe.rsp", [string[]]$arguments, [Text.UTF8Encoding]::new($false))
if ([IO.Path]::GetExtension($CompilerPath) -eq '.dll') { & dotnet $CompilerPath ("@$profile/probe.rsp") *> "$profile/build.log" }
else { & $CompilerPath ("@$profile/probe.rsp") *> "$profile/build.log" }
if ($LASTEXITCODE -ne 0) { Get-Content "$profile/build.log" -Tail 25; throw "Probe failed to compile. See $profile/build.log" }
$process = Start-Process "$game/RimWorld.exe" -WorkingDirectory $game -WindowStyle Hidden -ArgumentList @('-NoWindow', '-NoCrashDialog', ('"-savedatafolder=' + $profile + '"')) -RedirectStandardOutput "$profile/stdout.log" -RedirectStandardError "$profile/stderr.log" -PassThru
@{ profile = $profile; processId = $process.Id; biotech = [bool]$Biotech } | ConvertTo-Json | Set-Content "$profile/run.json"
Write-Output "Running isolated furry neighbor checks at $profile"
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    while (!$process.HasExited -and $timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Seconds 2; $process.Refresh() }
    if (!$process.HasExited) { throw "Timed out after $TimeoutSeconds seconds. See $profile/stdout.log" }
    if (!(Test-Path "$profile/result.txt")) { throw "No test result. See $profile/stdout.log and stderr.log" }
    Get-Content "$profile/result.txt" -TotalCount 1
    if ((Get-Content "$profile/result.txt" -TotalCount 1) -notmatch '^PASS ') { Get-Content "$profile/result.txt" -Tail 12; throw "Checks failed at $profile" }
    $startupErrors = @(Select-String -LiteralPath "$profile/stdout.log" -CaseSensitive -Pattern '\bERROR\b|XML error|Could not resolve cross-reference|Could not find type|Exception loading|Exception parsing')
    $known = @($startupErrors | Where-Object { $_.Line -match 'GLFW Error \(PlatformError\): Win32: Failed to create interface' })
    $known.Line | Set-Content "$profile/known-log-messages.txt"
    $unexpected = @($startupErrors | Where-Object { $_ -notin $known })
    if ($unexpected.Count) { $unexpected.Line | Set-Content "$profile/startup-errors.txt" }
    if ($unexpected.Count) { throw "Startup errors at $profile/startup-errors.txt" }
} finally {
    if (!$process.HasExited) { Stop-Process -InputObject $process }
}
