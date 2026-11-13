# 项目结构

核心不按名字加载某一个种族 DLL。它读取 Def 与 Pawn 上的助手组件，扩展通过 XML 注册内容，需要种族专有行为时再实现适配器。

## 三层分工

| 层 | 负责内容 | 修改时机 |
| --- | --- | --- |
| 核心 `NeighborCourtyards.Core` | 迎新条件与提交、名字、岗位、手账、目录、百科 | 修复共同问题或增加多种族都需要的能力 |
| 可选家庭模块 `NeighborCourtyards.Family` | 幼体外观接入与年龄床位规则 | 做幼体、家庭或专用年龄床时 |
| 种族扩展 | race、PawnKind、岗位配置、素材、渲染树和专用行为 | 开发自己的邻居时主要在这里工作 |

绒绒帮手在第三层：XML 接入家务与核心通用图集渲染器，素材与动画帧归种族所有，无需新增种族 C#。旧人类型绒绒仍保留 AtlasRendering.cs 以兼容旧定义。详见[帮手开发](HelperDevelopment.md)。

## 迎新配置的引用关系

```text
迎新建筑 CompProperties_Courtyard.species
  → AssistantSpeciesDef
      → race → 恰好一份 CompProperties_Assistant.species → 同一个配置
      → pawnKind.race → 同一个 race
      → roles / naming / adapterClass
```

迎新点检查人口、食物、床和安全条件，生成角色并调用外观配置。所有成员成功安置、扣餐后才调用 `OnWelcomed`。外观配置失败会清理未完成的迎新；完成回调失败会记录错误并保留已提交入住。回调说明见[公共 API](../NeighborCourtyardsFramework/Docs/PublicApi.md)。

## 按功能定位源码

| 要研究的能力 | 首先阅读 |
| --- | --- |
| 种族、岗位、字段校验与适配器 | [Definitions.cs](../NeighborCourtyardsFramework/Source/Core/Definitions.cs) |
| 助手状态、迎新建筑及存档 | [Assistants.cs](../NeighborCourtyardsFramework/Source/Core/Assistants.cs) |
| 迎新批次与家庭成员 | [ArrivalParties.cs](../NeighborCourtyardsFramework/Source/Core/ArrivalParties.cs) |
| 到来后的入场行走 | [ArrivalWalk.cs](../NeighborCourtyardsFramework/Source/Core/ArrivalWalk.cs) |
| 名字池 | [AssistantNames.cs](../NeighborCourtyardsFramework/Source/Core/AssistantNames.cs) |
| 建筑目录 | [BuildingCatalog.cs](../NeighborCourtyardsFramework/Source/Core/BuildingCatalog.cs) |
| 百科 | [Encyclopedia.cs](../NeighborCourtyardsFramework/Source/Core/Encyclopedia.cs) |
| 对外契约版本 | [NeighborFramework.cs](../NeighborCourtyardsFramework/Source/Core/NeighborFramework.cs) |
| 成年毛茸茸图集 | [AtlasRendering.cs](../NeighborCourtyardsFuzzy/Source/AtlasRendering.cs) |

## 何时使用 C#

名称、岗位、餐费、人口、等待时间、普通百科和建筑登记优先修改 XML。外观设置、特殊工作、头像构图或迎新成功后的种族行为可继承 `AssistantSpeciesAdapter`，并在配置写入完整 `adapterClass`。

每份种族配置共享一个适配器实例；不要把某一角色的状态存在适配器字段里。需要跨存档保留的角色状态写入自己的 Pawn 组件并实现相应存档逻辑。读取存档不会再次触发 `OnWelcomed`。

核心接管部分工作策略、征召限制、避难和初始化行为；接入前先确认这些约定符合玩法。所有公开 C# 成员并非都属于稳定契约，第三方开发优先依赖文档列出的 API。
