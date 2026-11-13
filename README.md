# 奇妙邻里 · 开发者仓库

为 RimWorld 制作自己的邻居：核心提供迎新、岗位、命名、管理、建筑目录和百科；扩展提供种族、内容与外观。本仓库包含核心框架 `0.8.0-dev.2`、纯 XML 入门模板和绒绒族 `0.2.0` 示例。公共接入契约版本为 `2`。

新开发推荐从[自主帮手教程](Docs/HelperDevelopment.md)开始：框架维护身份、家务与菜单，种族提供贴图、帧序列和专属行为。旧人类型接入仍保留。

**当前公开的是开发版源码，不是工坊核心更新包。** 现有奇妙邻里系列继续使用原工坊核心；请在独立环境测试源码构建的版本，同一游戏配置只加载一份核心。新帮手能力默认不影响旧人类型种族，菜单沿用工坊 0.6.9 的六页布局。尚未完成当前开发版与全系列最新版本的联合发布验收。当前检查范围见[开源准备记录](Docs/OpenSourceStatus.md)。

**第一次开发，从[从零运行](Docs/GettingStarted.md)开始。** 先用原版外观跑通迎新，再替换为自己的毛茸茸角色。

新手第一阶段只改 XML，不用写 C#；自定义外观与行为放到第二阶段。需要先安装开发工具并取得自己的核心运行素材，本仓库还不是“下载后立即制作角色”的一键工具。

## 开发文档

| 目标 | 文档 |
| --- | --- |
| 安装依赖、编译、启动首个邻居 | [从零运行](Docs/GettingStarted.md) |
| 理解核心与扩展分工、定位源码 | [架构与能力地图](Docs/Architecture.md) |
| 改名字、岗位、餐费、迎新和人口 | [XML 配置指南](Docs/Configuration.md) |
| 修改图集、四向外观、衣物和床位渲染 | [绒绒族外观开发](Docs/FuzzyRendering.md) |
| 增加自己的 C# 行为 | [公共 API 契约](NeighborCourtyardsFramework/Docs/PublicApi.md)及[适配器示例](NeighborCourtyardsFramework/Examples/Adapter/WelcomeAdapter.cs) |
| 确认功能与定位故障 | [测试与排错](Docs/Testing.md) |
| 提交代码与处理存档兼容 | [贡献规范](CONTRIBUTING.md) |
| 将这个目录上传 Git | [Git 与发布](Docs/Publishing.md) |
| 查看新手流程的已修复问题与限制 | [新手开发审查](Docs/BeginnerReview.md) |

## 目录

```text
NeighborCourtyardsFramework/   核心、家庭模块、纯 XML 模板与公共 API
  Source/Core/                生活和管理能力
  Source/Family/              可选幼体外观与床位规则
  Mod/                        核心运行目录，编译后安装此目录
  Examples/StarterNeighbor/   无需自定义 C# 的入门扩展
NeighborCourtyardsFuzzy/       成年毛茸茸邻居示例
  Source/AtlasRendering.cs     图集渲染类
  Mod/                        绒绒族运行目录
Docs/                         跨项目开发教程
Tools/                        仓库检查工具
```

## 环境与范围

需要自己的 RimWorld 1.7、Harmony、PowerShell 7 和支持 C# 9 的 Roslyn。Biotech 为可选依赖。已验证环境是 Windows 上的 Waylith/.NET 平铺运行时；传统 Unity 运行时需匹配程序集重新编译并单独实机验证。这里不是完整种族引擎，也未验证多人联机和所有第三方模组组合。

核心已有接入、异常处理与读档回归记录；绒绒族无 DLC 和 Biotech 两组均通过 29 项检查。详细范围分别见[核心质量记录](NeighborCourtyardsFramework/Docs/Quality.md)和[绒绒族验证记录](NeighborCourtyardsFuzzy/Validation/Results.md)。历史测试记录不等同于当前提交重新运行了全部游戏测试。

本次组合仓库另完成源码重建、教程命令检查，以及新构建的 29 项实机验证，见[仓库整理验证记录](Docs/RepositoryValidation.md)。

## 许可与素材

项目代码、脚本、项目 XML 和文档采用 [MIT](LICENSE)。仓库不包含游戏程序集、Harmony 二进制、运行 DLL、测试游戏副本或美术素材。核心 UI 图集需要从自己的核心安装准备；绒绒图集需要从自己的本地示例准备，或换成自行授权的同布局图集。具体命令在[从零运行](Docs/GettingStarted.md)。

素材未自动纳入代码 MIT：见[核心素材范围](NeighborCourtyardsFramework/Docs/AssetScope.md)和[绒绒族素材范围](NeighborCourtyardsFuzzy/Art/AssetScope.md)。仅克隆仓库还不能直接启用两个模组，需先完成编译和素材准备。
