# 素材与第三方依赖

项目代码、脚本、XML 和文档使用 MIT。贴图不在该许可内，也不随源码仓库分发；修改、抽取或再分发贴图需另行取得授权。

核心 UI 使用 `Mod/Textures` 下的图集，预览图为 `Mod/About/Preview.png`。开发时可运行 `Prepare-Content.ps1`，从已安装的核心复制所需的两张 UI 图集。

`HelperIcons.png` 是使用 OpenAI 图像生成工具制作的试作图集。当前菜单不使用它，可选接口和 `-HelperIconsPath` 参数仍保留。来源与布局见 [HelperIcons.md](HelperIcons.md)。

入门示例通过路径引用 RimWorld 的灯具、角色和床，仓库只提供配置，不包含游戏贴图。

RimWorld、Harmony、AlienRace、Roslyn 和 Mono.Cecil 遵循各自许可证，由开发者自行安装。
