# XML 配置指南

下文保留人类型接入字段。新增 `helperMode=true` 的 Animal 帮手要求与工作限制见[帮手配置](../NeighborCourtyardsFramework/Docs/Helpers.md)。

完整可运行配置见[入门模板](../NeighborCourtyardsFramework/Examples/StarterNeighbor/Defs/Neighbors.xml)和[绒绒族配置](../NeighborCourtyardsFuzzy/Mod/Defs/Neighbors.xml)。以下代码块是对应节点内的替换片段，不是额外新增一份同名 Def。

## 身份与引用先配对

为自己的模组选择独立 `packageId`，为 Def 使用统一唯一前缀。race 的助手组件指向本种族配置；配置的 `race` 与 `pawnKind.race` 必须一致。每个 race 恰好一份助手组件，配置至少一个岗位。迎新建筑继承 `NC_WelcomeBase`，它提供所需的 Rare ticking 等共同设置。

`defName` 是程序与存档引用，`label` 是显示名称。仅改显示文字时修改 `label`；已有用户存档后不要随意重命名 Def。

## 岗位与工作

在 `AssistantRoleDef` 中配置：

```xml
<primaryWork>Cleaning</primaryWork>
<work>
  <li><workType>Cleaning</workType><priority>2</priority></li>
  <li><workType>Hauling</workType><priority>3</priority></li>
</work>
```

优先级范围 `0..4`，正数越小越优先；0 不赋予该岗位的正优先级。每个工作类型只列一次。`allowOptionalWork=true` 时还可能启用岗位以外的可选工作，因此不要把岗位里写 0 当成所有情况下的硬性禁用。玩家的手动开关、年龄、避难与原版工作条件也会影响实际选择。

技能通过岗位 `skills` 配置，技能范围 `0..20`，同一技能不要重复。配置有某项工作并不意味着现场已有可执行任务；检查目标、路径、区域、工作能力和其他更高优先事项。

## 迎新参数

下面列的是 `AssistantSpeciesDef` 的代码默认值；绒绒族会将人口上限改为 4。

| 字段 | 默认 | 作用 / 约束 |
| --- | --- | --- |
| `arrivalTicks` | 120000 | 随机迎新的有效等待上限，必须大于 0；原版一天 60000 ticks |
| `mealCost` | 1 | 每位到来成员的餐食成本，非负 |
| `mealReserve` | 0 | 扣除成本后要求保留的餐食数量，非负 |
| `mealRadius` | 8 | 搜索餐食半径，有限正数 |
| `minimumMealNutrition` | 0.5 | 合格餐食的营养门槛，有限非负数 |
| `defaultPopulationLimit` | 6 | 初始人口上限，范围 0..50 |
| `minAge` / `maxAge` | 20 / 35 | 成人迎新年龄范围，需符合目标种族成长阶段 |
| `keepStartingApparel` | false | 保留生成时的衣物；绒绒族设置为 true |
| `allowOptionalWork` | false | 是否开放配置岗位之外的可选工作策略 |
| `familyChildren` | false | 启用家庭儿童迎新，需要 Biotech |

迎新点可以保存运行时覆盖值。修改 XML 默认值后，旧迎新点未必变成新数值；先检查该建筑的设置，再用新建迎新点验证默认值。家庭场景的成本与成员生成由核心统一处理，不要在扩展中重复扣餐。

计时只在迎新条件满足时推进，实际到来时刻在上限以内随机选择，并非每隔固定两天到来。即使 `familyChildren=false`，床位、餐食和人口空位足够时也可能一次来两位成人。需要控制首次测试只来一位时，准备一张空床，并把该迎新点人口上限设为 1。

## 名字池

```xml
<pools>
  <li>
    <key>default</key>
    <requireTwoChineseCharacters>false</requireTwoChineseCharacters>
    <names><li>松团</li><li>绒绒</li><li>Hazel</li></names>
  </li>
</pools>
```

配置的 `naming` 指向这份 `AssistantNameSetDef`。关闭双汉字限制后允许最多 64 字符的单行名称；不允许首尾空白、控制字符和富文本尖括号。不同角色需要不同名字池时，通过适配器 `NamePoolKey` 返回池的 key。

## 建筑与百科

`CourtyardBuildingSetDef` 登记种族建筑；`CourtyardArticleDef` 提供种族介绍或其他条目。两者都指向对应的 species。同一个内容包放多个种族时要明确分组，避免建筑被自动归入首个种族。

先复用模板里的原版灯与床验证接入。自定义家具涉及占地、角色位置、四向和占用状态，应在角色实际使用时验收。

## 扩展自己的行为

在种族配置里指定自己程序集中的完整类型名：

```xml
<adapterClass>MyNeighbor.WelcomeAdapter</adapterClass>
```

类必须是非抽象 `AssistantSpeciesAdapter` 子类，具有公开无参构造函数。参考[最小适配器](../NeighborCourtyardsFramework/Examples/Adapter/WelcomeAdapter.cs)，编译时引用匹配游戏环境的程序集与 `NeighborCourtyards.Core.dll`。回调时机和失败处理以[公共 API 契约](../NeighborCourtyardsFramework/Docs/PublicApi.md)为准。

若在绒绒族上练习，将自己的类放入 `NeighborCourtyardsFuzzy/Source/`，使用下面命令编译所有源文件（路径变量沿用入门教程）：

```powershell
./NeighborCourtyardsFuzzy/Build.ps1 -GameRoot $gameRoot -CoreAssembly './NeighborCourtyardsFramework/Mod/Assemblies/NeighborCourtyards.Core.dll'
```

没有 .NET SDK、使用单独 Roslyn 时再加 `-CompilerPath $compilerPath`。代码中的命名空间和类名要与 `adapterClass` 一致；只复制代码而没有编译到加载的 DLL 中，游戏找不到该类型。
