#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$files = @(& git -c core.quotepath=false -C $root ls-files --cached --others --exclude-standard | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0 -or !$files.Count) { throw 'Run this check inside an initialized Git repository containing the source files.' }
$failures = [Collections.Generic.List[string]]::new()
$known = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($relative in $files) { [void]$known.Add($relative.Replace('\', '/')) }
$xmlCount = 0; $scriptCount = 0; $linkCount = 0
foreach ($relative in $files) {
    $path = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { $failures.Add("Missing indexed file: $relative"); continue }
    if ($relative -match '\.(dll|exe|pdb|rws|log|rsp|zip)$|(^|/)(Dist|Local|game|Baseline)/|/Validation/(run-[^/]+/|package-check-[^/]+/|Build/)|/Mod/Textures/|/Mod/About/Preview\.png$') {
        $failures.Add("Local output or excluded asset is visible to Git: $relative")
    }
    switch ([IO.Path]::GetExtension($path)) {
        '.xml' {
            $xmlCount++
            try { [xml]$document = [IO.File]::ReadAllText($path) }
            catch { $failures.Add("XML parse failed: $relative : $($_.Exception.Message)") }
        }
        '.ps1' {
            $scriptCount++
            $tokens = $null; $parseErrors = $null
            [void][Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
            foreach ($errorItem in $parseErrors) { $failures.Add("PowerShell parse failed: $relative : $($errorItem.Message)") }
        }
        '.md' {
            foreach ($match in [regex]::Matches([IO.File]::ReadAllText($path), '\[[^\]]+\]\(([^)]+)\)')) {
                $target = $match.Groups[1].Value.Trim('<', '>')
                if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:|^#') { continue }
                $target = [Uri]::UnescapeDataString(($target -split '#', 2)[0])
                if (!$target) { continue }
                $linkCount++
                $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path $path -Parent) $target))
                $repoRelative = [IO.Path]::GetRelativePath($root, $resolved).Replace('\', '/')
                if (!$known.Contains($repoRelative)) { $failures.Add("Broken, excluded or wrong-case local link: $relative -> $target") }
            }
        }
    }
}
if ($failures.Count) { $failures | ForEach-Object { Write-Output $_ }; throw "$($failures.Count) repository check(s) failed." }
Write-Output "PASS: $($files.Count) Git-visible files; $xmlCount XML files; $scriptCount PowerShell scripts; $linkCount local Markdown links. Game behavior and Markdown anchors are not checked."
