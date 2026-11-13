# 绒绒族 0.1.0 验证记录

这是原型的历史实机结果；运行目录和截图未包含在 Git 仓库中。复现命令见本页后文，使用[实机验证脚本](Run-Checks.ps1)。

日期：2026-11-08。环境：本机 RimWorld 1.7 Waylith/.NET 平铺运行时、Harmony、奇妙邻里核心 0.7.0-dev.1。

| 运行 | DLC | 结果 |
| --- | --- | --- |
| `run-20261108-183732-d04e05` | 无 | PASS checks=29 errors=0 |
| `run-20261108-184154-3e3dad` | Biotech | PASS checks=29 errors=0 |

两次均通过额外启动日志错误扫描。仅排除已知 GLFW 平台接口提示，未排除模组错误。测试隔离保存目录和模组副本，不覆盖正式安装。

自动检查覆盖：Def 配置、渲染树、公共目录和百科、正常迎新、扣餐、分床、默认衣物、姓名、原生入场、四向渲染初始化、正常工作 AI 选择清洁、原生 Ingest 恢复营养、四个朝向的床位分配与实际睡入、保存加载后的家、岗位、手动工作开关与睡眠状态。

人工查看第二次运行的 `standing-four-directions.png`、`sleeping-four-directions.png` 和 `eating.png`：前后左右外观正常，侧面镜像与背面可辨，默认衣服叠层正常，四张床均有实际睡眠角色，身体由被褥遮挡，头部保留于枕边。截图是测试游戏直接输出。进食截图用于外观观察，进食成功以原生作业后的饱食恢复断言为准。

可复现命令：

```powershell
./Build.ps1 -GameRoot '<游戏目录>' -CompilerPath '<Roslyn csc.exe>'
./Validation/Run-Checks.ps1 -GameRoot '<游戏目录>' -HarmonyModRoot '<Harmony 模组目录>' -CompilerPath '<Roslyn csc.exe>' -CoreModRoot '<核心开发版模组目录>'
# 增加 -Biotech 可运行第二组
```

编译使用警告即错误。仅截图用探针调整过镜头、测试站位和截图时序；正式模组不包含这些测试行为。

范围限制：成年原型；未单独执行搬运完整过程；未验证所有衣物、头饰、基因外观、幼体或其他种族模组组合；传统 Unity 运行时尚未实机验证。测试程序与反编译资料不进入正式模组程序集。
