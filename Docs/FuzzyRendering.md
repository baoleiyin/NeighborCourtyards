# 绒绒族外观开发

本页描述保留的旧人类型绒绒族渲染器。新帮手使用核心通用渲染组件，见[帮手教程](HelperDevelopment.md)和[动画接口](../NeighborCourtyardsFramework/Docs/Helpers.md)，无需复制种族 DLL。

入口是 [RenderTree.xml](../NeighborCourtyardsFuzzy/Mod/Defs/RenderTree.xml) 和 [AtlasRendering.cs](../NeighborCourtyardsFuzzy/Source/AtlasRendering.cs)。生活能力仍按[配置指南](Configuration.md)接入，不需要在渲染器中实现工作或迎新。

## 图集约定

单张透明 PNG，三列两行，各格尺寸相等。当前网格尺寸按正方形图集设计，建议保持正方形画布与相同留白比例。

| 行 | 第 1 列 | 第 2 列 | 第 3 列 |
| --- | --- | --- | --- |
| 第 1 行 `atlasRow=0` | 身体正面，无头 | 身体右侧，无头 | 身体背面，无头 |
| 第 2 行 `atlasRow=1` | 头部正面 | 头部右侧 | 头部背面 |

South 取第 1 列，East 取第 2 列，North 取第 3 列；West 镜像第 2 列。若角色左右存在不对称标记，当前三列布局不能表达独立西向，需要自己扩展图集布局与 UV。

各格之间留透明边距，避免角色内容跨格；背景必须是真透明。身体不要画头，头部不要画肩膀，衣服由游戏叠加。原始[生成提示词](../NeighborCourtyardsFuzzy/Art/Prompt.txt)可作为构图参考，但生成新素材后仍需四向实机验收。

## 渲染树与尺寸

`AtlasNodeProperties` 提供 `atlasRow`、`meshSize`；每份节点配置缓存四个方向的网格，角色复用这些网格。`AtlasNode` 根据朝向选择 UV 并读取同一张 `Graphic_Single`。不要把动态的角色专属顶点修改写进共享网格。

当前身体 `meshSize=(1,1.5)`，头部 `(0.8,1.2)`，单位是游戏世界中的网格尺寸，实际可见大小还取决于图集留白。改变画布内容比例后，需要在游戏里调整这些值。

身体与头部分别使用原生 `PawnRenderNodeWorker_Body`、`PawnRenderNodeWorker_Head`，保留 `Body`、`Head`、`ApparelBody`、`ApparelHead` 标签。原生头节点计算头部偏移；身体节点遵循床的显示规则。身体和头部需分层，才能按原版床位规则遮挡身体。调整站立外观后也要检查睡床效果。

贴图路径填写 `FuzzyNeighbor/Atlas`，不加 `Textures/` 前缀和 `.png` 后缀。节点 XML 中的 C# 类名需与程序集实际命名空间一致。

## 派生自己的独立种族

仅在本地修改示例时可以保留名称。与原示例共存或独立发布时，依次修改：

1. `Mod/About/About.xml` 中的包 ID、名字和作者。
2. 所有 `NCFZ_` Def 名称及引用；保留核心依赖 `local.neighborcourtyards.core`。
3. 图集目录与渲染树中的 `texPath`。
4. C# 命名空间 `FuzzyNeighbor` 与 XML 中两个自定义类的完整类型名。
5. `Build.ps1` 中输出的程序集名称 `NeighborCourtyards.Fuzzy.dll`，并重新编译。旧输出不要复制进新模组。

构建脚本会编译 `Source/` 及其子目录下的所有 `.cs` 文件。加入调用框架 API 的类时，增加 `-CoreAssembly '<核心模组>/Assemblies/NeighborCourtyards.Core.dll'`；[配置指南](Configuration.md)提供完整命令。不要把只用于检查的测试类放进运行 Source 目录。

不要把多个同名程序集散放到不同扩展中同时加载。如果希望多个种族共用一个渲染器，先独立为一份共同依赖，再声明统一的加载关系。

## 验收

同一角色检查北、东、南、西；确认眼睛朝向、背面无正脸、侧向镜像、耳尾边缘与透明度。再检查默认衣物和自己支持的衣物组合。

四个方向的床都要实际有人睡入，检查身体遮挡、头部枕位与角度。另检查进食、行走和头像，保存读档后再次查看。测试程序的截图在帧末捕获，捕获当帧不要立刻传送角色或换场景。

当前示例仅覆盖成年、默认部落服与原版床；幼体、基因附加外观、其他头饰和复杂互动需单独适配。
