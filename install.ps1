param([switch]$Enable)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name OxygenNotIncluded -ErrorAction SilentlyContinue) { throw '请先完全退出缺氧，再安装。' }
$modsRoot = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Klei\OxygenNotIncluded\mods'
$target = Join-Path $modsRoot 'local\ModListPresets'
$package = Join-Path $PSScriptRoot 'dist\ModListPresets'
if (!(Test-Path -LiteralPath (Join-Path $package 'ModListPresets.dll'))) { throw '请先运行 build.ps1。' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-ChildItem -LiteralPath $package -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $target -Force }
if ($Enable) {
    $file = Join-Path $modsRoot 'mods.json'
    if (!(Test-Path -LiteralPath $file)) { throw '未找到 mods.json。文件已安装，请进游戏手动启用。' }
    $data = Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json
    $backupDir = Join-Path $PSScriptRoot 'installation-backups'
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $file -Destination (Join-Path $backupDir ('mods-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.json'))
    $item = $data.mods | Where-Object { $_.label.distribution_platform -eq 0 -and $_.label.id -eq 'ModListPresets' } | Select-Object -First 1
    if (!$item) {
        $item = [pscustomobject]@{ label = [pscustomobject]@{distribution_platform=0;id='ModListPresets';title='模组清单管理 / Mod List Presets';version=0};status=1;enabled=$false;enabledForDlc=@('','EXPANSION1_ID');crash_count=0;reinstall_path=$null;staticID='Local.ModListPresets' }
        $data.mods = @($data.mods) + @($item)
    } else { $item.enabledForDlc = @('','EXPANSION1_ID') }
    $json = $data | ConvertTo-Json -Depth 30
    $temp = $file + '.modlistpresets.tmp'
    [IO.File]::WriteAllText($temp, $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Replace($temp, $file, ($file + '.before-modlistpresets-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
}
Write-Host "Installed companion mod: $target"
Write-Host '原版 Mod Updater 和工坊文件没有修改。未使用 -Enable 时，请在游戏中手动启用。'
