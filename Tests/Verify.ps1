param(
    [string]$ProfilePath = "$env:APPDATA\r2modmanPlus-local\RiskOfRain2\profiles\Singleplayer"
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $ProfilePath 'BepInEx\core\Mono.Cecil.dll')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$project = Split-Path $PSScriptRoot
$ravagerPath = Get-ChildItem -LiteralPath (Join-Path $ProfilePath 'BepInEx\plugins') -Recurse -File -Filter 'RedGuyMod.dll' | Select-Object -First 1 -ExpandProperty FullName
if (-not $ravagerPath) {
    $ravagerPath = Get-ChildItem -LiteralPath (Join-Path $ProfilePath 'BepInEx\plugins') -Recurse -File -Filter '*Ravager*.dll' |
        Where-Object Name -ne 'RavagerNoSacrifice.dll' | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $ravagerPath) { throw 'The installed Ravager assembly was not found.' }
$ravager = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($ravagerPath)
$addon = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $project 'bin\Release\netstandard2.1\RavagerNoSacrifice.dll'))
$script:checks = 0

function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw $Name }
    $script:checks++
    Write-Output "PASS $Name"
}

function Method($Assembly, [string]$Type, [string]$Name) {
    $result = @($Assembly.MainModule.GetType($Type).Methods | Where-Object Name -eq $Name)
    if ($result.Count -ne 1) { throw "Expected one $Type.$Name" }
    return $result[0]
}

$blink = Method $ravager 'RedGuyMod.SkillStates.Ravager.BlinkBig' 'OnEnter'
Check (@($blink.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.r4' -and [Math]::Abs($_.Operand - 0.1) -lt 0.000001 }).Count -eq 1) 'Twisted Mutation health constant'
Check (@($blink.Body.Instructions | Where-Object { $_.Operand.Name -eq 'TakeDamage' }).Count -eq 1) 'Twisted Mutation damage call'
$plugin = $addon.MainModule.GetType('RavagerNoSacrifice.Plugin')
$pluginVersion = ($plugin.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInPlugin' }).ConstructorArguments[2].Value
Check ($null -ne $addon.MainModule.GetType('RavagerNoSacrifice.RavagerMeterController')) 'Centered meter controller is included'
Check ($null -ne $addon.MainModule.GetType('RavagerNoSacrifice.LookingGlassSupport')) 'LookingGlass skill support is included'
$healthPatch = Method $addon 'RavagerNoSacrifice.Plugin' 'ChangeHealthCost'
Check (@($healthPatch.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq 'ApplyHealthCost' }).Count -eq 1) 'Zero-cost damage wrapper is installed'
$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $project 'Thunderstore\manifest.json') | ConvertFrom-Json
Check ($manifest.version_number -eq $pluginVersion -and $addon.Name.Version.ToString(3) -eq $pluginVersion) 'Manifest, plugin and assembly versions agree'
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $project "RavagerNoSacrifice-$pluginVersion.zip"))
try {
    foreach ($entry in @('manifest.json','README.md','CHANGELOG.md','icon.png','plugins/RavagerNoSacrifice/RavagerNoSacrifice.dll')) {
        Check ($null -ne $zip.GetEntry($entry)) "Package contains $entry"
    }
    Check ($zip.Entries.Count -eq 5) 'Package contains only the intended five files'
}
finally { $zip.Dispose() }
$ravager.Dispose()
$addon.Dispose()
Write-Output "$script:checks static checks passed. In-game testing is still needed."
