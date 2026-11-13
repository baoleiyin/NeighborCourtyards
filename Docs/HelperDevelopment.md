# 从绒绒帮手开始开发

推荐新邻居使用核心 `0.8.0-dev.2` 的帮手模式。先按[环境与构建](GettingStarted.md)准备核心和素材，再看完整[帮手配置与动画接口](../NeighborCourtyardsFramework/Docs/Helpers.md)。

## 第一个可玩的帮手

在独立测试游戏加载 Harmony、核心、绒绒族。打开邻里手账 → 建筑，建「绒绒营地灯」，准备动物睡位和附近未禁止的餐食。在「迎新」页开启迎新后最多等待两天有效时间；缺床、缺餐或危险会暂停迎新。每次来一位，默认上限 4。

把污渍区域加入家区、准备仓库，然后在「助手」页选中邻居，从右侧「工作设置」启用或禁用清洁和搬运；关闭的工作不会执行。在「迎新」页设置日常区域与避难区域，检查角色能吃饭、入库和睡觉。

旧「绒绒迎新灯」仍召来人类型绒绒族，用于旧档兼容；新功能请使用「绒绒营地灯」。不要混淆床位要求。

## 派生一个无需 C# 的种族

1. 新建模组目录，包含 `About/About.xml`、`Defs/` 和 `Textures/`；About 声明核心依赖 `local.neighborcourtyards.core`。
2. 复制 [Helpers.xml](../NeighborCourtyardsFuzzy/Mod/Defs/Helpers.xml) 到自己的 Defs，将全部 `NCFZ_` 名称与引用统一替换为自己的前缀，包 ID 也必须独立。
3. 从 [Neighbors.xml](../NeighborCourtyardsFuzzy/Mod/Defs/Neighbors.xml) 复制岗位 `NCFZ_Helper` 和名字池 `NCFZ_Names` 两个 Def，并同步改前缀。不要复制其中旧人类 race、旧迎新灯和旧渲染树。
4. 把自己的透明三列两行图集放到 `Textures/MyNeighbor/Atlas.png`，修改 Helpers.xml 中所有 `FuzzyNeighbor/Atlas` 路径。上排是身体正面、右侧、背面，下排是对应头部；西向使用镜像。
5. 按自己的设计修改名字、描述、餐费、人口上限与岗位。身体和头部尺寸在 XML 中调整。这个纯帮手扩展不需要 `NeighborCourtyards.Fuzzy.dll`，渲染器由核心提供。
6. 通过正常迎新验证，不要只用开发者直接生成代替。验收清单见[测试与排错](Testing.md)。

想增加动画，先画真实动作帧，再配置公共渲染器的 `animations`。想增加一种新工作，才编写适配器 C#。这两个扩展方向可以独立完成。

## 当前边界

成年帮手底层是 Animal，会出现在动物页；不穿人类衣服、不参与人类家庭、不提供任意生产岗位。现有示例只含静态四向美术与通用动画接口。旧人类型开发教程仍保留在[外观开发](FuzzyRendering.md)，不应直接套到帮手上。

代码、配置和文档使用 MIT；贴图授权单独见[素材范围](../NeighborCourtyardsFramework/Docs/AssetScope.md)。Git 源码包不含 DLL 与运行图集，本地试玩包包含已准备好的运行文件。
