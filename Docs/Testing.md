# 测试与排错

## 不启动游戏的检查

从仓库根目录执行：

如果使用下载解压的 ZIP，先在该根目录执行 `git init -b main`；克隆仓库已有 `.git`，无需重复初始化。

```powershell
./Tools/Check-Repository.ps1
```

检查 Git 可见文件中的 XML 语法、PowerShell 语法、Markdown 本地链接及不应提交的二进制、游戏存档和运行日志。它不解析游戏 Def 交叉引用，不证明游戏功能正确。GitHub Actions 使用同一命令；其他 Git 托管平台可在自己的流水线调用它。

## 编译

按照[从零运行](GettingStarted.md)分别构建核心与绒绒族。构建启用警告视为错误。游戏二进制由本地提供，不把它们上传 CI。

帮手的历史测试结果见[帮手验证](../NeighborCourtyardsFuzzy/Validation/HelperResults.md)。此前历史实机结果见[核心质量记录](../NeighborCourtyardsFramework/Docs/Quality.md)与[绒绒族记录](../NeighborCourtyardsFuzzy/Validation/Results.md)。原始运行目录、游戏副本与截图未随 Git 仓库提供。克隆仓库后可用下面命令生成自己的验证证据。

## 自动实机验证

当前两个运行器只支持 Windows 的 Waylith/.NET 平铺游戏目录，会创建独立游戏与保存目录，并启动测试实例，不修改正式模组安装。运行器可能复制游戏文件，请预留本地磁盘空间。先完成编译与素材准备。

```powershell
$gameRoot = 'D:/Games/RimWorld'
$harmonyRoot = 'D:/Mods/Harmony'
$compilerPath = 'D:/Tools/Roslyn/csc.exe'

./NeighborCourtyardsFramework/Validation/Run-Checks.ps1 -GameRoot $gameRoot -HarmonyModRoot $harmonyRoot -CompilerPath $compilerPath
./NeighborCourtyardsFuzzy/Validation/Run-Checks.ps1 -GameRoot $gameRoot -HarmonyModRoot $harmonyRoot -CompilerPath $compilerPath
```

实机脚本需要显式编译器路径。若入门时采用 .NET SDK 自动发现，可从刚才核心构建输出的 `manifest.json` 中读取 `compiler` 字段，填入 `$compilerPath`；它指向 SDK 的 `csc.dll`，不是 `dotnet.exe`。

启用 Biotech 的验证，在相应命令末尾加 `-Biotech`。新结果位于各项目 `Validation/run-*/`：先读 `result.txt`，失败再读 `errors.txt`、`startup-errors.txt` 或 `build.log`；其余上下文见 `stdout.log`、`stderr.log`。`PASS` 后运行器仍会扫描启动错误，须以整个脚本成功结束为准。

核心覆盖配置、迎新异常与提交、工作、入床和读档；绒绒族覆盖注册、迎新、服装、名字、正常清洁 AI、原生进食、四向床与读档。帮手探针额外覆盖非殖民者身份、完整搬运入库、禁用中断、避难、优先项存档、公共图集帧与缺帧回退、六个管理页。

`Compare-Api.ps1` 需要自己提供基线与候选程序集目录；`LegacyProbe.cs` 还需要对应旧核心、种族扩展和种子存档。它们不属于无外部材料的一键新仓库检查。具体参数通过 `Get-Help <脚本路径>` 查看，历史比较结果为随仓库保留的记录。

## 按修改范围选择验证

| 修改 | 最少需要 |
| --- | --- |
| 文档 | 仓库检查、确认命令与当前脚本一致 |
| 名字 / 岗位 / 迎新 XML | 加载无新增错误、正常迎新、工作与读档 |
| 图集 / 渲染节点 | 四向站立、实际睡床、衣物、头像和读档 |
| 核心迎新 / 保存逻辑 | 编译、核心回归；涉及已有状态时加旧档验证 |
| 公共类型 / 方法签名 | API 比对、使用方编译与必要实机验证 |

## 常见故障

| 现象 | 检查 |
| --- | --- |
| 找不到 `Assembly-CSharp.dll` | `-ManagedPath` 必须指向实际程序集目录，而非模组目录 |
| 找不到自定义类型 | DLL 是否编译、是否在 `Assemblies`、XML 完整类名是否匹配、运行时是否一致 |
| 重复 Def / 包 ID | 新扩展前缀是否全部替换，是否同时加载两份核心或旧版本扩展 |
| 图集缺失 / 粉色或错误贴图 | 是否执行素材准备、`texPath` 是否省略扩展名、路径大小写是否一致 |
| 迎新不发生 | 开关、人口、可达且合适的空床、附近合格餐食、温度与安全条件；读取 `BlockReason()` |
| 修改 XML 后旧迎新点没变化 | 检查存档中的建筑覆盖设置，另用新建筑验证默认值 |
| 有清洁岗位但不打扫 | Home 区域、可达污物、污物成熟时间、手动工作开关和更高优先事项 |
| 睡床时整个身体露出 | 保留原生 Body worker 和头身分层；检查自己的床显示规则 |
| 读档后岗位丢失 | 是否改了岗位 defName / 包 ID，是否仍加载原种族与组件类型 |
| 测试超时 | 先看编译与启动日志；运行器默认 300 秒，可用 `-TimeoutSeconds` 设置合理上限 |
| 绒绒族验证提示路径过长，或旧 csc 报文件存在却找不到 | 将源码放在较短目录；绒绒族运行器支持 `-RunRoot 'C:/NC-QA'` 指定较短的测试输出目录 |

提问题时提供版本、运行时、DLC、模组列表、最小复现步骤，以及首条相关错误的上下文。外观问题附实际朝向或占用状态截图；不要用整份游戏目录作为附件。

## 帮手、语言、窗口和旧档

在绒绒运行命令后加 `-Helpers` 测新帮手；不加则验证保留的旧人类型绒绒。`-Language 'ChineseSimplified (简体中文)'` 从启动时选择中文，目录名以本机游戏为准；`-Width 1024 -Height 768` 检查较小窗口。测试固定 UI 缩放偏好为 1 并忽略系统 DPI 缩放，避免游戏把有效窗口尺寸降到最低要求以下。`-Biotech` 可与这些参数组合。

用 `-LegacySave '<旧核心生成的 FuzzyNeighborDemo.rws>'` 验证旧人类型绒绒存档，不与 `-Helpers` 合用。种子来自旧版 FuzzyProbe 的四人存档，测试保留人物身份、床位、岗位和工作开关，再保存并读入升级后的存档。旧档测试需使用修改前版本生成的存档。

睡床截图应在角色到达床位并睡着后检查；探针暂停快进时会在入床完成后同步渲染插值，避免画面滞后于真实位置。图集索引从左上按行排列；缺少动作方向时验证静态回退。

API 比对默认严格比较所有公开声明，包括版本常量。明确升级版本时可加 `-AllowVersionChange`，仅允许 `NeighborFramework.Version` 字符串值变化，报告仍单独记录该变化；不忽略程序集身份或签名删除。
