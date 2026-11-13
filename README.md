# 奇妙邻里

RimWorld 邻居模组开发框架，包含迎新、命名、工作、区域管理、建筑目录和百科。种族扩展可以使用这些功能，添加自己的外观和行为。

仓库包含核心 `0.8.0-dev.2`、原版外观模板和绒绒族 `0.2.0` 示例，API 版本为 `2`。目前为开发版，工坊核心尚未同步更新。请使用独立测试存档，同一游戏配置只加载一份核心。

## 开始开发

第一次做模组，先按[从零运行](Docs/GettingStarted.md)安装工具、编译核心，用原版外观模板做出第一个邻居。修改名字、岗位和迎新条件只需要编辑 XML。

想做清洁、搬运一类的小帮手，可以从[绒绒帮手教程](Docs/HelperDevelopment.md)开始。帮手使用 Animal 身份，贴图和动画由种族扩展提供。原有的人类型邻居仍受支持。

## 文档

| 内容 | 文档 |
| --- | --- |
| 安装、编译和首次运行 | [从零运行](Docs/GettingStarted.md) |
| 核心与扩展的分工、源码位置 | [项目结构](Docs/Architecture.md) |
| 名字、岗位、餐费、迎新和人口 | [XML 配置](Docs/Configuration.md) |
| 图集、四向外观、衣物和床位 | [绒绒族外观](Docs/FuzzyRendering.md) |
| C# 扩展 | [公共 API](NeighborCourtyardsFramework/Docs/PublicApi.md) · [适配器示例](NeighborCourtyardsFramework/Examples/Adapter/WelcomeAdapter.cs) |
| 测试命令和常见错误 | [测试与排错](Docs/Testing.md) |
| 提交修改 | [贡献规范](CONTRIBUTING.md) |
| 打包与发布 | [Git 与发布](Docs/Publishing.md) |
| 当前版本和测试结果 | [版本记录](Docs/OpenSourceStatus.md) |

## 目录

```text
NeighborCourtyardsFramework/
  Source/Core/                核心代码
  Source/Family/              幼体外观与年龄床位规则
  Mod/                        编译后的模组目录
  Examples/StarterNeighbor/   纯 XML 入门模板
  Examples/Adapter/           C# 适配器示例
NeighborCourtyardsFuzzy/       绒绒族与帮手示例
Docs/                         开发文档
Tools/                        仓库检查与打包脚本
```

## 环境

需要 RimWorld 1.7、Harmony、PowerShell 7 和支持 C# 9 的 Roslyn 编译器。安装 .NET SDK 即可取得编译器。Biotech 为可选依赖。

实机测试使用 Windows 的 Waylith/.NET 平铺运行时。传统 Unity 运行时、多人联机以及现有奇妙邻里全系列的最新组合尚未完成测试。

## 许可

代码、脚本、项目 XML 和文档使用 [MIT](LICENSE)。

仓库不附带 DLL、游戏文件或贴图。运行前需要编译，并按[素材准备步骤](Docs/GettingStarted.md)准备图集。美术不在 MIT 授权内，详见[核心素材说明](NeighborCourtyardsFramework/Docs/AssetScope.md)和[绒绒族素材说明](NeighborCourtyardsFuzzy/Art/AssetScope.md)。
