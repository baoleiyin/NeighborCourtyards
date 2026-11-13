param(
    [Parameter(Mandatory)][string]$BaselineDirectory,
    [Parameter(Mandatory)][string]$CandidateDirectory,
    [Parameter(Mandatory)][string]$CecilAssembly,
    [switch]$AllowVersionChange
)
$ErrorActionPreference = 'Stop'
Add-Type -LiteralPath (Resolve-Path -LiteralPath $CecilAssembly).Path
function Get-VisibleTypes($types) {
    foreach ($type in $types) {
        if (!$type.IsPublic -and !$type.IsNestedPublic -and !$type.IsNestedFamily -and !$type.IsNestedFamilyOrAssembly) { continue }
        $type
        Get-VisibleTypes $type.NestedTypes
    }
}
function Get-PublicMembers([string]$assemblyPath) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
    try {
        "ASSEMBLY $($assembly.Name.Name) $($assembly.Name.Version)"
        foreach ($type in Get-VisibleTypes $assembly.MainModule.Types) {
            "TYPE $($type.FullName) base=$($type.BaseType) abstract=$($type.IsAbstract) sealed=$($type.IsSealed)"
            foreach ($interface in $type.Interfaces) { "INTERFACE $($type.FullName) $($interface.InterfaceType.FullName)" }
            foreach ($method in $type.Methods | Where-Object { $_.IsPublic -or $_.IsFamily -or $_.IsFamilyOrAssembly }) {
                "METHOD $($method.FullName) static=$($method.IsStatic) virtual=$($method.IsVirtual) final=$($method.IsFinal) names=$($method.Parameters.Name -join ',')"
            }
            foreach ($field in $type.Fields | Where-Object { $_.IsPublic -or $_.IsFamily -or $_.IsFamilyOrAssembly }) {
                "FIELD $($field.FullName) static=$($field.IsStatic) readonly=$($field.IsInitOnly) constant=$($field.Constant)"
            }
            foreach ($property in $type.Properties | Where-Object { $_.GetMethod.IsPublic -or $_.GetMethod.IsFamily -or $_.SetMethod.IsPublic -or $_.SetMethod.IsFamily }) {
                "PROPERTY $($property.FullName)"
            }
            foreach ($event in $type.Events | Where-Object { $_.AddMethod.IsPublic -or $_.AddMethod.IsFamily }) {
                "EVENT $($event.FullName)"
            }
        }
    } finally { $assembly.Dispose() }
}
$results = @()
foreach ($part in @('Core', 'Family')) {
    $name = "NeighborCourtyards.$part.dll"
    $baseline = @(Get-PublicMembers (Join-Path $BaselineDirectory $name))
    $candidate = @(Get-PublicMembers (Join-Path $CandidateDirectory $name))
    $removed = @($baseline | Where-Object { $_ -notin $candidate })
    $versionValueChanges = @()
    $versionPrefix = 'FIELD System.String NeighborCourtyards.NeighborFramework::Version static=True readonly=False constant='
    if ($AllowVersionChange -and @($candidate | Where-Object { $_.StartsWith($versionPrefix) }).Count -eq 1) {
        $versionValueChanges = @($removed | Where-Object { $_.StartsWith($versionPrefix) })
        $removed = @($removed | Where-Object { $_ -notin $versionValueChanges })
    }
    $added = @($candidate | Where-Object { $_ -notin $baseline })
    $results += @{ assembly = $name; baselineMembers = $baseline.Count; removed = $removed; added = $added; versionValueChanges = $versionValueChanges }
}
$results | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $PSScriptRoot 'api-comparison.json') -Encoding utf8
if (@($results | Where-Object { $_.removed.Count -gt 0 }).Count) { throw 'Public API removals detected. See Validation/api-comparison.json.' }
Write-Output ('PASS API comparison, preserved ' + ($results.baselineMembers | Measure-Object -Sum).Sum + ' baseline declarations.')
