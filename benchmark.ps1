param([string]$GamePath = 'D:\steam\steamapps\common\OxygenNotIncluded')
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GamePath 'OxygenNotIncluded_Data\Managed'
$testOutput = Join-Path $PSScriptRoot 'tests\bin'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /optimize+ /utf8output ('/out:' + (Join-Path $testOutput 'LibraryBenchmark.exe')) ('/r:' + (Join-Path $managed 'Newtonsoft.Json.dll')) ('/r:' + (Join-Path $managed 'netstandard.dll')) (Join-Path $PSScriptRoot 'src\PresetCore.cs') (Join-Path $PSScriptRoot 'src\PresetLibrary.cs') (Join-Path $PSScriptRoot 'tests\LibraryBenchmark.cs')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark compilation failed.' }
Copy-Item -LiteralPath (Join-Path $managed 'Newtonsoft.Json.dll') -Destination $testOutput
& (Join-Path $testOutput 'LibraryBenchmark.exe')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark failed.' }
