# 版本记录

## 2026-11-13 · 0.8.0-dev.2

核心 `0.8.0-dev.2`，绒绒族 `0.2.0`，API 版本 `2`。源码已发布到 [GitHub](https://github.com/baoleiyin/NeighborCourtyards)，工坊核心保持原版本。

- 提供 Animal 帮手模式，保留人类型邻居。帮手使用新 Def，并设置 `helperMode=true`。
- 菜单沿用工坊 0.6.9 的六页布局，支持显示和管理 Animal 帮手。
- 包含原版外观模板、绒绒族示例、开发文档和验证脚本。

### 测试

环境：Windows，RimWorld 1.7，Waylith/.NET 平铺运行时。

| 项目 | 结果 |
| --- | --- |
| 核心、家庭模块和绒绒族编译 | 通过，警告视为错误 |
| 无 DLC、中文帮手回归 | 46 项通过，运行错误 0，启动扫描通过 |
| 已有四帮手存档 | 7 项检查通过，存档文件未改写 |
| 六页菜单与工作设置弹窗 | 无运行错误；已查看助手、迎新、工作设置和生物页截图 |
| 源码包在新目录编译 | 三个 DLL 与工作目录产物哈希一致 |
| GitHub Actions 仓库检查 | 通过 |

历史记录：[核心](../NeighborCourtyardsFramework/Docs/Quality.md)、[帮手](../NeighborCourtyardsFuzzy/Validation/HelperResults.md)、[仓库整理](RepositoryValidation.md)、[开发工具修复](BeginnerReview.md)。各记录只对应当时测试的版本。

传统 Unity 运行时、多人联机、长期大殖民地性能和全系列最新版本的专属互动尚未完成测试。
