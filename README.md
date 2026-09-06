# 模组清单管理 / Mod List Presets

我平时会同时使用大量《缺氧》模组。换电脑、游戏报错后批量禁用模组，以及手动拖动加载顺序都很麻烦，因此开发了这个模组，主要满足个人多模组管理需求。

> [!IMPORTANT]
> 前置模组：需要安装并启用 [Mod Updater](https://steamcommunity.com/sharedfiles/filedetails/?id=2018291283)（Workshop ID：`2018291283`）。

## 下载与安装

1. 安装并启用 Mod Updater。
2. 从 [Releases](https://github.com/baiye-gift/ModListPresets/releases/latest) 下载最新版 ZIP。
3. 解压后，将 `ModListPresets` 文件夹放入：

   ```text
   文档/Klei/OxygenNotIncluded/mods/local/
   ```

4. 在游戏模组列表中启用“模组清单管理 / Mod List Presets”，然后按提示重启游戏。

## 功能

- 保存多份模组启用状态和加载顺序清单。
- 导入、导出清单，方便换电脑或备份。
- 游戏因报错批量停用模组后，可用清单快速恢复。
- 预览应用后的启用、停用和缺失模组。
- 单项或批量调整本次应用结果。
- 输入序号调整模组加载顺序。
- 缺失模组会自动跳过，并提供对应工坊页面入口。
- 可选自动备份，默认关闭。
- 删除不再需要的清单。

个人清单保存在：

```text
文档/Klei/OxygenNotIncluded/mods/config/ModListPresets/
```

清单只记录模组信息和启用顺序，不包含其他作者的模组文件。换电脑后仍需下载对应模组；没有安装的项目不会阻止其余内容应用。

## 兼容性

- Windows 版《缺氧》
- 支持原版及 DLC
- 最低支持游戏版本：744825
- 必须与 Mod Updater 同时启用

## 更新说明

这是以个人使用为主的模组。只有我自己在玩《缺氧》期间才会按实际需要适配和更新，更新频率不会很高。

## 开发

```powershell
./build.ps1 -GamePath 'D:\steam\steamapps\common\OxygenNotIncluded'
./test.ps1 -GamePath 'D:\steam\steamapps\common\OxygenNotIncluded'
```

源码和构建文件位于本仓库；发布包只包含一个 DLL 和两个 YAML 文件。
