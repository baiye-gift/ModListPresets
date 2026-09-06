param([string]$GamePath = 'D:\steam\steamapps\common\OxygenNotIncluded')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$managed = Join-Path $GamePath 'OxygenNotIncluded_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $projectRoot 'dist\ModListPresets'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$names = @('Assembly-CSharp','Assembly-CSharp-firstpass','0Harmony','Newtonsoft.Json','UnityEngine','UnityEngine.CoreModule','UnityEngine.UI','UnityEngine.UIModule','UnityEngine.TextRenderingModule','UnityEngine.InputLegacyModule','Unity.TextMeshPro','netstandard')
$refs = $names | ForEach-Object { '/reference:' + (Join-Path $managed ($_ + '.dll')) }
$sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /target:library /optimize+ /warn:4 /utf8output ('/out:' + (Join-Path $output 'ModListPresets.dll')) @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host "Built: $output"
