# 奇妙邻里开发者框架

新增自主帮手、直接工作管理与通用图集动画接口，见[帮手开发](Docs/Helpers.md)。

本目录可单独作为核心源码包使用，入门见[创建一个邻居扩展](Docs/QuickStart.md)。若使用组合仓库，新手完整教程在仓库根目录的 `Docs/GettingStarted.md`；本页说明核心单独构建与打包。

当前版本为 `0.8.0-dev.2`，公共接入契约版本为 `2`。本目录基于正式发布的核心 `0.6.9` 整理，供第三方种族作者开发和集成验证。

## 从这里开始

- `Docs/QuickStart.md` 说明如何创建一个独立邻居扩展并运行。
- `Docs/PublicApi.md` 列出公共能力、配置约束、调用时机和存档契约。
- `Docs/Quality.md` 记录审查范围、修复与验证证据。
- `Source/Core` 保存迎新、岗位、命名、管理、目录与百科实现。
- `Source/Family` 保存可选幼体外观和年龄床位规则。
- `Examples/StarterNeighbor` 是可直接加载的纯 XML 扩展。
- `Examples/Adapter` 是 C# 扩展回调示例。

示例使用原版人类外观与原版床。制作毛茸茸种族时，可替换成作者自己的种族定义、HAR 配置或自定义渲染组件，再接入框架提供的生活能力。

## 编译

安装 PowerShell 7，准备 RimWorld 1.7、Harmony 和支持 C# 9 的 Roslyn 编译器。已安装 .NET SDK 时，脚本会寻找 SDK 中的编译器。也可以显式指定 `-CompilerPath`。

```powershell
./Build.ps1 -GameRoot 'D:/Games/RimWorld' -HarmonyAssembly 'D:/Mods/Harmony/1.7/Assemblies/0Harmony.dll'
```

`-ManagedPath` 可指定包含 `Assembly-CSharp.dll` 的引用目录。编译脚本支持传统 Unity Managed 目录与平铺程序集目录的发现。每次编译记录日志和源文件哈希，输出到 `Mod/Assemblies`。

使用与玩家实际运行环境匹配的游戏程序集编译。当前自动运行验证使用本机 RimWorld 1.7 的 Waylith/.NET 平铺运行时。传统 Unity 运行时的加载兼容性需要另行实机验证。

源码 ZIP 中的运行美术由现有核心安装提供。在编译后的开发版首次运行前，执行下面的准备命令。

```powershell
./Prepare-Content.ps1 -InstalledCoreRoot 'D:/Games/RimWorld/Mods/NeighborCourtyardsCore'
```

`Package-Source.ps1` 生成可分享的源码 ZIP，包含代码、配置、文档、模板与验证代码。分享前检查 `Docs/Quality.md` 中的验证边界。

## 版本与升级

开发版与正式版共用 `local.neighborcourtyards.core`，测试配置中每次选择一个核心版本。扩展作者发布自己的种族包，并依赖核心包。

`NeighborFramework.ContractVersion` 可在运行时读取。公开契约变更须更新文档与兼容性测试。程序集身份保留基线值，文件版本与信息版本标记开发版。现有存档键和公开类型名称沿用基线。

## 授权

本目录的 C#、PowerShell、项目 XML 与开发文档使用 MIT，保留 `LICENSE` 中的版权声明即可按许可复用。

`Mod/Textures` 和 `Mod/About/Preview.png` 是现有框架美术，授权范围单列于 `Docs/AssetScope.md`。游戏与 Harmony 的二进制由开发者自行提供。生成的游戏测试副本和本地反编译审查材料保存在忽略目录中。
