# Steam 创意工坊发布说明

> 前置模组：Mod Updater（Steam Workshop ID：`2018291283`）。用户必须先安装并启用 Mod Updater。

## 发布文件

在 Oxygen Not Included Uploader 中选择下面这个文件夹本身：

`D:\缺氧mod开发\ModListPresets\dist\ModListPresets`

该文件夹根目录应只有：

- `ModListPresets.dll`
- `mod_info.yaml`
- `mod.yaml`

不要选择项目根目录，也不要上传 `src`、`tests`、`installation-backups`、个人清单或配置文件。

## 第一次发布

1. 在 Steam 库的筛选器中勾选“工具”，安装并启动 **Oxygen Not Included Uploader**（AppID `636750`）。
2. 点击 `Add` 新建项目。
3. 内容目录选择上面的 `dist\ModListPresets`。
4. 选择一张 512×512 的 PNG 或 JPG 作为预览图。
5. 填写标题、说明和适合的标签，先以“仅自己可见”或“不公开”发布。
6. 上传完成后打开项目的创意工坊页面，接受 Steam Workshop Legal Agreement。
7. 在另一台电脑订阅测试。测试时只保留创意工坊版，避免本地版和创意工坊版以同一 `staticID` 同时加载。
8. 验证能进入游戏、打开模组清单并正常预览后，再把可见性改为“公开”。

## 建议标题

`模组清单管理 / Mod List Presets`

## 建议说明

保存、导入、导出并恢复《缺氧》的模组启用状态和加载顺序。

主要功能：

- 将当前启用状态和顺序保存为多个清单
- 导入、导出清单，方便换电脑或分享
- 预览应用后的启用、停用和缺失数量
- 单项或批量调整应用后的状态
- 输入序号调整加载顺序
- 缺失模组安全跳过，并提供对应工坊页面入口
- 可选自动备份，默认关闭

当前版本：1.4.0

支持游戏内容：原版及 DLC
最低支持版本：744825

本模组依赖 Mod Updater，但不会修改 Mod Updater 的 DLL 或接管其更新功能。

个人清单保存在游戏配置目录中，不会随创意工坊模组自动上传。换电脑时请在模组内导出清单，再在新电脑导入。

## 后续更新

在 Uploader 中选择第一次发布得到的同一个项目，更新内容目录后提交。不要为每个版本重新点击 `Add`，否则会产生多个创意工坊项目。

每次上传前：

1. 更新 `mod_info.yaml` 中的版本号。
2. 运行 `build.ps1` 和 `test.ps1`。
3. 确认 `dist\ModListPresets` 中只有三个发布文件。
4. 先以不公开状态验证，再更新公开项目。
