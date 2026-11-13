# 公共 API v2

API v2 保留原有接口，新增 [Animal 帮手与通用表现接口](Helpers.md)。`helperMode` 默认 false；新帮手应使用新的 race Def，不能原地改变旧角色的种族类型。

以下列出对外接口。使用未列出的公开成员时，需自行检查版本兼容性。

## 核心定义

| 定义 | 用途与约束 |
| --- | --- |
| `AssistantSpeciesDef` | 注册 Humanlike 种族（或 helperMode=true 的 Animal 帮手）、PawnKind、岗位、命名与迎新规则。同一 race 恰好挂载一份指向自身配置的助手组件。 |
| `AssistantRoleDef` | 注册岗位。工作优先级范围 0 至 4，技能范围 0 至 20。同一岗位中的工作与技能条目各自唯一。 |
| `CompProperties_Assistant` | 挂载到种族 Pawn 上，指向其种族配置。 |
| `CompProperties_Courtyard` | 挂载到 Rare ticking 的迎新建筑，指向接待种族。 |
| `AssistantNameSetDef` / `AssistantNamePool` | 配置名字白名单与权重。默认使用双汉字规则。每个池可设置 `requireTwoChineseCharacters=false`，允许最长 64 字符的单行名称。名称首尾空白、控制字符和富文本尖括号会被拒绝。 |
| `CourtyardBuildingSetDef` | 登记建筑分组。当前目录也会收集同一内容包内具有建筑菜单的其他可建造定义，并自动归类。一个内容包含多种族时应明确指定各组，避免落到首个种族分组。 |
| `CourtyardArticleDef` | 注册种族介绍、谱系或材料来源条目。 |
| `CourtyardContentDef` | 供动物扩展登记物种。 |
| `CompProperties_AnimalWelcome` | 动物迎新建筑的食物、等待时间与数量配置。 |
| `CourtyardPetArrivalDef` / `CourtyardBedSlots` | 随种族宠物及动物窝位配置。 |

新加入的年龄字段默认值为 `childMinAge=4`、`childMaxAge=11`。配置中的浮点数需要为有限数值。`defaultPopulationLimit` 范围为 0 至 50。类型和 Def 引用在加载完成后由配置检查报告错误。

## 种族适配器

继承 `AssistantSpeciesAdapter`，提供公开无参构造函数。框架为每份种族配置缓存一个实例。角色专属状态应保存在 Pawn 的组件中。

| 方法 | 调用约定 |
| --- | --- |
| `RoleIndex(Pawn)` | 返回岗位列表中的索引。默认优先读取已保存的岗位 Def，再兼容旧索引。避免在查询中改变状态。 |
| `ConfigureAppearance(Pawn,int)` | 迎新角色生成后、生成流程返回前调用。角色尚未正式入住。抛出异常会中止本次迎新并清理已生成角色。 |
| `OnWelcomed(Pawn,CompCourtyard)` | 本次迎新成员全部生成、安置并完成餐食扣除后，逐人调用一次。异常会报告到日志，已完成入住的状态继续保留。存档加载不会补发此回调。 |
| `NamePoolKey(Pawn)` | 返回命名池 key，返回 null 使用符合条件的池。 |
| `PortraitFraming(Pawn,out Vector3,out float)` | 提供头像位置偏移与缩放。 |
| `SpecialJob(Pawn)` | 返回可执行的种族特殊工作，返回 null 继续正常岗位流程。避免每次查询重复创建世界对象。 |
| `WorkTypeForJob(Pawn,Job)` | 将特殊工作映射到工作类型，供工作开关和中断处理使用。 |
| `PreserveGizmo(Pawn,Gizmo)` | 保留扩展需要显示的命令按钮。 |

上述回调在游戏主线程执行。回调中的重新迎新调用会被同一个迎新点的重入保护拒绝。首次配置异常清理与完成回调异常隔离属于本版验证范围。

## 查询与操作

- `Roles.Profile`、`Roles.Managed`、`Roles.For`、`Roles.Priority` 用于识别邻居及查询有效岗位规则。
- `CompAssistant.SetWorkEnabled` 和 `SetUnskilledThreshold` 更新工作策略并刷新有效优先级。
- `CompCourtyard.BlockReason` 返回迎新阻塞原因，null 表示条件满足。
- `CompCourtyard.TryWelcome(out Pawn)` 尝试接待一批成员，成功时返回首位成员。调用会改变地图、床位和食物状态。内部失败可能抛出异常，调用者应报告上下文。迎新点需处于正常地图生命周期中。
- `CompCourtyard.Advance` 推进迎新时间，仍检查人口、餐食、床位与安全条件。
- `AssistantNames.Personal`、`Display` 用于读取个人名字与带身份说明的显示文本。

接入助手组件后，框架会管理岗位优先级、工作开关、避难区域及部分人物初始化，并限制征召和玩家强制工作。`allowOptionalWork`、`allowGrowthTraits`、`preserveChildSkills`、`preserveGrowingSchedules`、`keepStartingApparel` 控制已开放的策略。种族作者应在实机中确认这些策略符合自己的玩法。

## 家庭公共能力

`NeighborCourtyards.Family` 提供 `LifeStageSpriteDef`、`FamilyAppearanceExtension`、`CompProperties_GrowingNeighbor` 和 `FamilyBedExtension`。幼体图集为三列两行，帧索引范围 0 至 5，高度为有限正数。

`IFamilyPoseDriver.DrawExternalPose` 允许扩展的工作驱动声明其正在绘制外部姿态。`FamilyBedRules.Allowed` 可查询年龄与种族床位规则。具体种族的成人图层、装饰和互动行为需要扩展自行配置。

## 存档与升级约束

本版沿用现有类型、命名空间、程序集身份与 `nc*` 存档键。新增配置通过 Def 提供默认值。角色身份应以 Def 和持久引用保存，避免将岗位列表索引作为唯一身份。

第三方作者应稳定保留 packageId、种族与岗位 defName、自定义组件类型名和自定义存档键。修改这些标识时提供自己的迁移方案。多人联机、跨运行时二进制兼容和卸载已有种族的存档恢复需另行专项验证。
