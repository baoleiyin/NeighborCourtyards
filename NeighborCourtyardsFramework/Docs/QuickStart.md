# 创建一个邻居扩展

本页生成的是人类型邻居。新自主帮手的字段、Animal 床位和通用渲染组件见[帮手开发](Helpers.md)。

## 生成模板

```powershell
./New-Neighbor.ps1 -OutputDirectory 'D:/MyMods/FuzzyNeighbor' -PackageId 'yourname.fuzzyneighbor' -Prefix Fuzzy -Name '毛茸茸邻居' -Author '你的名字'
```

输出目录必须尚未存在。脚本替换包 ID 和全部示例 Def 前缀，保留核心依赖。作者与模组名称通过 XML 写入，支持中文和 XML 特殊字符。

将框架的 `Mod` 目录内容放入独立测试游戏的核心模组目录，将生成的扩展放入同一测试游戏的 Mods 目录。在模组列表依次启用 Harmony、核心和扩展。

## 改动顺序

1. 在 `Defs/Neighbors.xml` 修改种族、岗位、名字池与迎新条件。
2. 保持 `AssistantSpeciesDef.race`、`pawnKind.race` 和种族组件的 `species` 相互对应。
3. 创建新外观时替换示例的种族定义。接入已有种族时，用自己的补丁为目标种族添加一份 `CompProperties_Assistant`，并移除示例种族定义。
4. 注册自己的建筑与百科。整个扩展需要唯一的 packageId，每个 Def 需要唯一前缀。
5. 检查加载日志中的配置错误，再开始地图测试。

同一个种族对应一份邻居配置。为同一种族配置多套岗位时，将岗位加入同一配置的 `roles`。每份配置的适配器实例由该种族共享。

## 首次实机检查

在开发模式中放置迎新灯、原版空床和灯附近的简易餐食。灯需要属于玩家，床需要可达且适合该种族，现场温度与安全条件需要满足迎新规则。

检查邻里手账中是否出现种族、建筑目录和百科条目。通过正常迎新确认角色从地图边缘走入、领取床位、执行配置的工作，再确认吃饭与睡眠。保存后重新加载，检查角色姓名、岗位、手动工作开关、迎新点引用和床位。

迎新等待时间可通过种族 XML 调整。C# 测试代码也可调用 `CompCourtyard.Advance(ticks)`，该方法仍检查原有迎新条件。

## 添加自己的 C# 行为

引用与运行环境匹配的游戏程序集、Harmony 和 `NeighborCourtyards.Core.dll`，编译 `Examples/Adapter/WelcomeAdapter.cs` 或自己的实现。将编译结果放入扩展的 `Assemblies` 目录，然后在种族定义中填写实际完整类型名。

```xml
<adapterClass>StarterNeighbor.WelcomeAdapter</adapterClass>
```

普通外观配置使用 `ConfigureAppearance`。需要在成功到来后运行的逻辑使用 `OnWelcomed`。回调时机与失败语义见 `PublicApi.md`。

## 家庭与外观

家庭迎新通过 `familyChildren` 启用，并要求 Biotech 处于启用状态。成人与儿童迎新年龄分别使用 `minAge/maxAge`、`childMinAge/childMaxAge`，范围应匹配目标种族的成长阶段。

幼体外观可使用 `FamilyAppearanceExtension` 和 `LifeStageSpriteDef`，图集固定为三列两行。索引从左上角开始，范围 0 至 5。面向、蜷缩、睡眠和玩耍帧可分别配置。具体渲染节点和成人图层隐藏需在自己的种族渲染树中完成。

制作家具时应结合实际占地、人物位置、四向结构和透明画布检查。床位验收需要角色实际睡入，座椅验收需要实际有人使用。耳朵、尾巴、成年双人互动和种族专用动作由扩展提供，并分别做运行验证。
