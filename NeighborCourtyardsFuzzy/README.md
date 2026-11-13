# 奇妙邻里 · 绒绒族

本页同时适用于源码目录和独立试玩包。组合仓库的新手教程位于根目录 `Docs/GettingStarted.md`，详细外观教程在 `Docs/FuzzyRendering.md`。Git 源码不包含运行 DLL 和图集，需先编译并执行本目录 `Prepare-Content.ps1 -AtlasPath '<自己的图集.png>'`；本地试玩包已经包含它们。

版本 `0.2.0`，包含 Animal 绒绒帮手和旧人类型绒绒族两种示例。旧种族和迎新灯保留用于旧档兼容。

## 新增帮手模式

使用「绒绒营地灯」与动物床/动物睡位，每次迎新一位。帮手自主清洁、搬运、进食和休息，不属于殖民者，仍出现在原生动物页，不穿人类衣物。在「助手」页选中邻居，打开右侧「工作设置」启用或禁用工作；「迎新」页设置迎新、区域和避难。

帮手定义集中在 `Defs/Helpers.xml`，复用 `Neighbors.xml` 的岗位与名字池。渲染使用核心公共组件，无需复制 `Source/AtlasRendering.cs`。派生新模组只复制 Helpers.xml 及它引用的岗位、名字池，改独立前缀和贴图路径即可；不要复制旧人类定义。动画接口已提供，示例尚无新增动作帧。组合仓库教程在 `Docs/HelperDevelopment.md`；独立试玩包见核心 `Docs/Helpers.md`。

## 试玩

需要 RimWorld 1.7、Harmony 和奇妙邻里核心开发版 `0.8.0-dev.2`。Biotech 可选。当前二进制在本机 Waylith/.NET 平铺运行时编译并验证；传统 Unity 运行时尚未实机验证，应使用对应游戏程序集重新编译再测试。

本地试玩 ZIP 内有 `NeighborCourtyardsCore` 与 `NeighborCourtyardsFuzzy` 两个模组目录，将它们放进自己的独立测试游戏 `Mods` 目录。加载顺序为游戏本体及所需 DLC、Harmony、奇妙邻里核心、绒绒族（Harmony 也可按自身要求在游戏本体之前加载）。

开发核心与已安装正式核心共用包 ID；同一配置只保留一个核心版本。这个包不会自动替换正式安装。建议先用新测试存档试玩。

旧模式：在邻里建筑目录找到「绒绒迎新灯」，准备可达的原版空床、灯附近的餐食与安全适宜的环境，开启迎新。默认在最多两天的有效等待时间内随机迎新，条件受阻时暂停计时；每位到来的邻居消耗一份餐，人口上限 4。床位和食物足够时可能一次来两位成人。到来的绒绒族使用名字池并分配家务岗位，可在邻里手账查看与管理。

包含：

- 四向毛茸茸身体和头部，西向复用东向镜像；默认穿原版部落服。
- 清洁优先级 2、搬运优先级 3；实际工作由框架和原版 AI 执行。
- 迎新、名字池、岗位、建筑目录、百科与存档接入。
- 原生进食与睡床；头部、身体分开渲染，身体随原版床规则隐藏。

这是成年原型，没有幼体专用外观、繁殖设计、自定义家具或动作。只验收了默认衣物与原版床，未验证所有服装、头饰、基因外观和第三方种族模组组合。

## 复制旧人类型邻居

源码项目使用 `Mod/` 存放运行文件；试玩包中的模组目录直接包含 `About/`、`Defs/`、`Textures/` 和 `Assemblies/`。下列路径均相对于运行文件目录。

| 修改位置 | 控制内容 |
| --- | --- |
| `About/About.xml` | 模组名、作者、独立包 ID、核心依赖 |
| `Defs/Neighbors.xml` | 种族、PawnKind、迎新、岗位、姓名、目录和百科 |
| `Defs/RenderTree.xml` | 图集路径、头身尺寸、原生衣物与床位渲染节点 |
| `Textures/FuzzyNeighbor/Atlas.png` | 透明三列两行图集 |
| `Source/AtlasRendering.cs` | 图集 UV、四向镜像和共享网格缓存 |

1. 为新模组分配独立包 ID；统一替换所有 `NCFZ_` Def 名称及引用，并修改纹理路径。
2. 修改名字、描述、岗位优先级、迎新餐费和人口上限。这些内容仅改 XML，无需重编译。
3. 准备同布局透明图集：上排为无头身体的正面、右侧、背面；下排为对应头部。各格等宽等高。角色本身不画衣服，由游戏叠加衣物。
4. 若继续使用本例的图集类，多个扩展不要重复加载同一份 DLL。独立发布派生包时修改 C# 命名空间、XML 的类名、程序集输出名后重编译，或先把该渲染器整理成共同依赖。
5. 用正常迎新确认入场与工作，再检查四向站立、有人占用的床和保存读档。

更换渲染方案时，保留种族的 `CompProperties_Assistant` 与 `AssistantSpeciesDef` 引用。

## 编译

安装 PowerShell 7 和支持 C# 9 的 Roslyn，提供自己安装的游戏程序集与编译器。

```powershell
./Build.ps1 -GameRoot 'D:/Games/RimWorld' -CompilerPath 'D:/Tools/Roslyn/csc.exe'
```

脚本自动寻找平铺或传统 Unity Managed 目录，也可通过 `-ManagedPath` 指定。有 .NET SDK 时可省略 `-CompilerPath`；仅安装 dotnet 运行时不够。脚本编译 `Source/` 下所有 C# 文件；新增核心适配器时增加 `-CoreAssembly '<核心模组>/Assemblies/NeighborCourtyards.Core.dll'`。脚本兼容源码项目与试玩包两种目录布局，通过 `-ModRoot` 指定输出目录。编译启用警告即错误与确定性构建，日志写入 `Validation/Build`。

## 验证与授权

旧原型历史结果见 `Validation/Results.md`。当前帮手改动与验证见 `Validation/HelperResults.md`；测试运行日志和截图保留在本地。

代码、脚本、项目 XML 和文档使用 MIT，保留 `LICENSE` 版权声明。美术与第三方文件的范围见 `Art/AssetScope.md`。本地试玩包包含开发核心的运行美术，不作为已取得全部素材再分发授权的公开发行包。
