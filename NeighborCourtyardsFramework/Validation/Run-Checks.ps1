param(
    [Parameter(Mandatory)][string]$GameRoot,
    [Parameter(Mandatory)][string]$HarmonyModRoot,
    [Parameter(Mandatory)][string]$CompilerPath,
    [string[]]$CompatibilityModRoots = @(),
    [string]$CoreModRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Mod'),
    [ValidateSet('FrameworkProbe.cs', 'LegacyProbe.cs')][string]$Probe = 'FrameworkProbe.cs',
    [string]$SeedSave,
    [string]$BaselineStartupErrors,
    [switch]$Biotech,
    [int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
$HarmonyModRoot = (Resolve-Path -LiteralPath $HarmonyModRoot).Path
# This runner targets the local flat .NET runtime. Build.ps1 also accepts Unity Managed directories.
if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'Assembly-CSharp.dll'))) { throw 'This validation runner requires a flat managed RimWorld runtime.' }
$profile = Join-Path $PSScriptRoot ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
$game = Join-Path $profile 'game'
New-Item -ItemType Directory -Path "$game/Mods/QA/About", "$game/Mods/QA/Assemblies", "$profile/Config", "$profile/Saves" -Force | Out-Null
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
if ($Probe -eq 'FrameworkProbe.cs') {
    Copy-Item -LiteralPath (Join-Path $repository 'Examples/StarterNeighbor') -Destination "$game/Mods/StarterNeighbor" -Recurse
    & (Join-Path $repository 'New-Neighbor.ps1') -OutputDirectory "$game/Mods/SecondNeighbor" -PackageId 'local.neighborcourtyards.second' -Prefix NCT -Name '第二示例邻居' -Author 'QA'
}
[IO.File]::WriteAllText("$game/Mods/QA/About/About.xml", '<ModMetaData><name>Framework QA</name><author>70</author><packageId>local.neighborcourtyards.frameworkqa</packageId><supportedVersions><li>1.7</li></supportedVersions></ModMetaData>')
$packages = @('brrainz.harmony', 'ludeon.rimworld')
if ($Biotech) { $packages += 'ludeon.rimworld.biotech' }
$packages += 'local.neighborcourtyards.core'
foreach ($directory in $CompatibilityModRoots) {
    $directory = (Resolve-Path -LiteralPath $directory).Path
    $about = [xml][IO.File]::ReadAllText((Join-Path $directory 'About/About.xml'))
    $package = $about.ModMetaData.packageId.ToLowerInvariant()
    New-Item -ItemType Junction -Path (Join-Path "$game/Mods" $package) -Target $directory | Out-Null
    $packages += $package
}
if ($Probe -eq 'FrameworkProbe.cs') { $packages += @('local.neighborcourtyards.starter', 'local.neighborcourtyards.second') }
$packages += 'local.neighborcourtyards.frameworkqa'
if ($SeedSave) {
    Copy-Item -LiteralPath $SeedSave -Destination "$profile/Saves/Autostart.rws"
    Copy-Item -LiteralPath (Join-Path (Split-Path (Split-Path $SeedSave -Parent) -Parent) 'legacy-state.txt') -Destination "$profile/legacy-state.txt"
    [IO.File]::WriteAllText("$profile/verify-legacy.txt", 'Verify the baseline save with the candidate core.')
}
$packageXml = ($packages | ForEach-Object { "<li>$_</li>" }) -join ''
[IO.File]::WriteAllText("$profile/Config/ModsConfig.xml", "<ModsConfigData><version>1.7.4990</version><activeMods>$packageXml</activeMods><knownExpansions><li>ludeon.rimworld.royalty</li><li>ludeon.rimworld.ideology</li><li>ludeon.rimworld.biotech</li><li>ludeon.rimworld.anomaly</li><li>ludeon.rimworld.odyssey</li></knownExpansions></ModsConfigData>")
[IO.File]::WriteAllText("$profile/Config/Prefs.xml", '<PrefsData><volumeMaster>0</volumeMaster><screenWidth>1280</screenWidth><screenHeight>800</screenHeight><fullscreen>False</fullscreen><devMode>True</devMode><langFolderName>English</langFolderName><pauseOnLoad>True</pauseOnLoad></PrefsData>')
$references = @()
foreach ($file in Get-ChildItem -LiteralPath $GameRoot -Filter '*.dll') {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($file.FullName) }
    catch [BadImageFormatException] { continue }
    $references += '/reference:"' + $file.FullName + '"'
}
$references += '/reference:"' + (Join-Path $HarmonyModRoot '1.7/Assemblies/0Harmony.dll') + '"'
foreach ($part in @('Core', 'Family')) { $references += '/reference:"' + "$game/Mods/NeighborCourtyardsCore/Assemblies/NeighborCourtyards.$part.dll" + '"' }
$arguments = @('/nologo', '/target:library', '/langversion:9.0', '/warnaserror+', '/nostdlib+', ('/out:"' + "$game/Mods/QA/Assemblies/FrameworkQA.dll" + '"')) + $references
$arguments += '"' + (Join-Path $PSScriptRoot $Probe) + '"'
if ($Probe -eq 'FrameworkProbe.cs') { $arguments += '"' + (Join-Path $repository 'Examples/Adapter/WelcomeAdapter.cs') + '"' }
[IO.File]::WriteAllLines("$profile/probe.rsp", [string[]]$arguments, [Text.UTF8Encoding]::new($false))
if ([IO.Path]::GetExtension($CompilerPath) -eq '.dll') { & dotnet $CompilerPath ("@$profile/probe.rsp") *> "$profile/build.log" }
else { & $CompilerPath ("@$profile/probe.rsp") *> "$profile/build.log" }
if ($LASTEXITCODE -ne 0) { Get-Content "$profile/build.log" -Tail 25; throw "Probe failed to compile. See $profile/build.log" }
$process = Start-Process "$game/RimWorld.exe" -WorkingDirectory $game -WindowStyle Hidden -ArgumentList @('-NoWindow', '-NoCrashDialog', ('"-savedatafolder=' + $profile + '"')) -RedirectStandardOutput "$profile/stdout.log" -RedirectStandardError "$profile/stderr.log" -PassThru
@{ profile = $profile; processId = $process.Id; biotech = [bool]$Biotech } | ConvertTo-Json | Set-Content "$profile/run.json"
Write-Output "Running isolated framework checks at $profile"
$timer = [Diagnostics.Stopwatch]::StartNew()
try {
    while (!$process.HasExited -and $timer.Elapsed.TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Seconds 2; $process.Refresh() }
    if (!$process.HasExited) { throw "Timed out after $TimeoutSeconds seconds. See $profile/stdout.log" }
    if (!(Test-Path "$profile/result.txt")) { throw "No test result. See $profile/stdout.log and stderr.log" }
    Get-Content "$profile/result.txt" -TotalCount 1
    if ((Get-Content "$profile/result.txt" -TotalCount 1) -notmatch '^PASS ') { Get-Content "$profile/result.txt" -Tail 12; throw "Checks failed at $profile" }
    $startupErrors = @(Select-String -LiteralPath "$profile/stdout.log" -CaseSensitive -Pattern '\bERROR\b|XML error|Could not resolve cross-reference|Could not find type|Exception loading|Exception parsing')
    $known = @($startupErrors | Where-Object { $_.Line -match 'GLFW Error \(PlatformError\): Win32: Failed to create interface|QA_EXPECTED_CALLBACK' })
    $known.Line | Set-Content "$profile/known-log-messages.txt"
    $unexpected = @($startupErrors | Where-Object { $_ -notin $known })
    if ($unexpected.Count) { $unexpected.Line | Set-Content "$profile/startup-errors.txt" }
    if ($BaselineStartupErrors) {
        $baselineMessages = @(Get-Content -LiteralPath $BaselineStartupErrors | ForEach-Object { $_ -replace '^\S+ ERROR ', '' } | Sort-Object)
        $candidateMessages = @($unexpected | ForEach-Object { $_.Line -replace '^\S+ ERROR ', '' } | Sort-Object)
        $difference = @(Compare-Object -ReferenceObject $baselineMessages -DifferenceObject $candidateMessages)
        if ($difference.Count) { $difference | Out-File "$profile/startup-difference.txt"; throw "Startup error set differs from baseline at $profile/startup-difference.txt" }
        Copy-Item -LiteralPath $BaselineStartupErrors -Destination "$profile/baseline-startup-errors.txt"
        Write-Output "Matched $($candidateMessages.Count) pre-existing baseline startup errors."
    } elseif ($unexpected.Count) { throw "Startup errors at $profile/startup-errors.txt" }
} finally {
    if (!$process.HasExited) { Stop-Process -InputObject $process }
}
