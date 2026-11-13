# 自主帮手与表现接口（契约 2）

核心 `0.8.0-dev.2` 新增可选的 `helperMode`。旧人类型邻居默认仍走原有流程；不要把已有存档中的 Humanlike race 直接改成 Animal。

## 分工

框架负责迎新、名字、岗位开关、优先工作、日常区域、避难、存档和管理页，以及清洁与入库搬运。原生动物 AI 负责进食、休息、受伤与威胁响应。种族提供自己的 race、PawnKind、渲染树、贴图、帧序列和特殊行为。

帮手使用 Animal Pawn，显示在原生动物页。当前支持清洁和搬运，不支持人类衣物、征召、婚姻和幼体家庭。示例使用 Human 身体结构做头身分层，但没有人类技能或 story tracker，代码中访问这些字段前需检查是否为空。

## 配置要求

1. 新建独立 Def 名称的 Animal race：`intelligence=Animal`、`thinkTreeMain=Animal`、`thinkTreeConstant=AnimalConstant`，成年 lifeStage，`trainability=None`、`petness=0`（不随机作为开局宠物）；不需要训练后才工作。
2. race 挂一份 `CompProperties_Assistant`，其 `species` 指向自己的 `AssistantSpeciesDef`。
3. `AssistantSpeciesDef` 中设 `helperMode=true`，填写匹配的 `race`、`pawnKind`、`roles`、`naming`。`familyChildren` 和 `allowOptionalWork` 保持 false。
4. 岗位 `work` 配置 `Cleaning` 和/或 `Hauling`；大于 0 表示允许，小数字优先。清洁需要家区，搬运需要有容量且允许存放该物品的仓库，两者都遵守禁用与区域限制。
5. `CompProperties_Courtyard.species` 指向该种族。迎新需要可达、空闲且尺寸适用的动物床/动物睡位和未禁止的餐食，每次迎新一位。人类床不适用。

其他服务需要自定义适配器，重写 `SpecialJob(Pawn)` 返回工作，并在 `WorkTypeForJob(Pawn,Job)` 返回对应岗位类型。框架仅在该工作已启用时接受任务。适配器负责自己的目标可达性、区域、预约及失败条件；特殊工作在内置服务之前查询，因此不要一直返回无法执行的任务。只有自定义岗位 XML 而没有工作实现，不会自动获得生产能力。

`NeighborRegistry.Residents(Map)` 返回本地图上由框架管理的存活角色，包含被搬运者。`Roles.Helper(Pawn)` 查询帮手身份。`CompAssistant.SetPreferredWork(work)` 设置优先项，传 null 清除；其余已启用岗位优先级至少为 2。禁用工作优先于“优先此项”。`preferredWork` 使用存档键 `ncPreferredWork`，旧档缺失时默认 null。

## 公共图集渲染器

`NeighborSpriteProperties`、`NeighborSpriteNode` 和 `NeighborHeadWorker` 位于核心 DLL，不需要种族复制一份 C# 渲染器。种族也可选用自己的渲染方案，逻辑不依赖这些类。

以下是渲染树 `children` 中一个身体节点，假设图集有 3 列、3 行。索引从左上角的 0 开始，按行排列；这里索引 6、7 是两张已经画好的动作帧：

```xml
<li Class="NeighborCourtyards.NeighborSpriteProperties">
  <tagDef>Body</tagDef>
  <workerClass>PawnRenderNodeWorker_Body</workerClass>
  <texPath>MyNeighbor/Atlas</texPath>
  <columns>3</columns><rows>3</rows>
  <south>0</south><east>1</east><north>2</north>
  <meshSize>(1,1.5)</meshSize>
  <animations>
    <li>
      <pose>Clean</pose><ticksPerFrame>12</ticksPerFrame>
      <south><li>6</li><li>7</li></south>
    </li>
  </animations>
</li>
```

未配置某个状态或朝向的帧时，使用该朝向的静态格。西向镜像东向；当前不支持独立西向帧。头像始终取静态格。索引必须小于 `columns × rows`；错误索引会报告一次错误并回退第 0 格，发布前应修正错误索引。帧间隔必须大于 0，运行时最低按 1 tick 处理。网格按配置共享，不要在运行中修改其顶点或缓存。

可用状态为 `Idle`、`Walk`、`Work`、`Clean`、`Haul`、`Eat`、`Sleep`、`Downed`。`NeighborPresentation.Pose(pawn)` 只读查询动作；优先级为倒地、移动、实际躺卧、进食、清洁、搬运、其他工作、闲置。`Sleep` 表示正在实际躺卧的 LayDown 任务，也包含躺着休息；移动中使用 Walk。循环帧由游戏 tick 驱动，暂停时停止；同帧率角色同步播放，不从每次任务开始重置。动画不决定实际工作进度。

头节点可用相同属性类，设置 `tagDef=Head`、`workerClass=NeighborCourtyards.NeighborHeadWorker`、`headOffset=(0,0,0.3)`，并配置头部的格索引。不要对 Animal Pawn 使用依赖 `story.headType` 的人类头部 worker。身体使用原生 Body worker，遵循实际睡床的显示规则。

绒绒示例使用三列两行静态图集，未新增行走/清洁等动作帧。新增帧必须由种族作者画入图集并配置，不能仅复制上述索引到六格图集。

## 菜单与验收

界面恢复工坊 0.6.9 的六页布局：助手、产出、迎新、建筑、百科、生物，对应 `Open` 页编号 0–5。默认打开助手列表；选中邻居后通过右侧「工作设置」启用或禁用工作，通过「定位」跳到角色。迎新、避难和区域设置位于「迎新」页。帮手仍显示在助手列表中；没有技能的 Animal 帮手不显示技能筛选。工作优先数据与 API 保留，界面不再提供此前试作的列表状态格。

至少验证正常迎新、非殖民者身份、清洁、完整搬运入库、工作中禁用、区域与避难、进食、四向实际睡床、保存读档。外观检查包括四向、头像和实际睡床时的显示。
