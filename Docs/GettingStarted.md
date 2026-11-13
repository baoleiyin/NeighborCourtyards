# 从零运行一个邻居

新开发推荐先读[自主帮手教程](HelperDevelopment.md)。下文的生成器创建旧式人类型模板，便于保留原有接入流程；创建 Animal 帮手请复制帮手示例。

这个教程使用原版人类外观模板。先编译核心、准备素材，再修改 XML 并在游戏里测试迎新，无需编写 C#。

## 0. 打开正确的目录与终端

安装 [PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows)、[Git](https://git-scm.com/downloads) 和 [.NET SDK](https://dotnet.microsoft.com/download)。SDK 包含编译器，只有 .NET Runtime 不够。也可用自己已有的 Roslyn C# 9 或更新编译器。

克隆或解压源码后，在 PowerShell 7 中进入同时包含 README、Tools 和两个项目目录的文件夹。后续命令均从此处执行：

```powershell
Set-Location 'D:/Projects/NeighborCourtyards'  # 改成自己的源码目录
$PSVersionTable.PSVersion.Major              # 应为 7 或更高
Test-Path './NeighborCourtyardsFramework/Build.ps1'  # 应为 True
git --version
dotnet --list-sdks                           # SDK 路线应至少列出一个版本
```

若版本号为 5，请运行 `pwsh` 切换到 PowerShell 7。若下载的是 ZIP、当前目录没有 `.git`，先运行 `git init -b main`，仓库检查与源码打包工具才可工作。初始化不会上传文件。

## 1. 找到游戏与依赖

准备 RimWorld 1.7、[Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) 和已安装的[奇妙邻里核心](https://steamcommunity.com/sharedfiles/filedetails/?id=3822167069)。这里的已安装核心仅用于取得 UI 素材，实际运行将使用源码构建的开发核心。

Steam 游戏属性的“已安装文件 → 浏览”可定位游戏目录。工坊模组通常在同一 Steam 库的 `steamapps/workshop/content/294100/<工坊编号>/`；手工安装模组通常在游戏的 `Mods/` 下。选择的模组目录应直接包含 `About/About.xml`。

```powershell
$gameRoot = 'D:/Games/RimWorld'
$harmonyRoot = 'D:/SteamLibrary/steamapps/workshop/content/294100/2009463077'
$installedCore = 'D:/SteamLibrary/steamapps/workshop/content/294100/3822167069'

Test-Path "$harmonyRoot/1.7/Assemblies/0Harmony.dll"  # 应为 True
Test-Path "$installedCore/About/About.xml"            # 应为 True
Test-Path "$gameRoot/Assembly-CSharp.dll"              # True 表示平铺运行时
Test-Path "$gameRoot/RimWorldWin64_Data/Managed/Assembly-CSharp.dll"
```

最后两项至少一项应为 True，否则游戏路径可能不正确。脚本可发现这两类目录，但实机验证只覆盖 Windows 的 Waylith/.NET 平铺运行时；传统 Unity Managed 环境仍需单独游戏验收。

```powershell
./NeighborCourtyardsFramework/Prepare-Content.ps1 -InstalledCoreRoot $installedCore
```

成功时显示以 `Prepared runtime atlases` 开头的提示。它复制必要的两张 UI 图集，不复制已安装 DLL；来源中存在旧试作的 HelperIcons 时也会复制，但当前菜单不使用它。源码包不含这些美术，需先安装工坊核心取得图集。

## 2. 编译核心

已安装 .NET SDK 时不用手动寻找编译器：

```powershell
./NeighborCourtyardsFramework/Build.ps1 -GameRoot $gameRoot -HarmonyAssembly "$harmonyRoot/1.7/Assemblies/0Harmony.dll"
```

若使用独立 Roslyn，改用以下命令；不要选择 Windows 自带的旧 Framework 编译器：

```powershell
$compilerPath = 'D:/Tools/Roslyn/csc.exe'
./NeighborCourtyardsFramework/Build.ps1 -GameRoot $gameRoot -HarmonyAssembly "$harmonyRoot/1.7/Assemblies/0Harmony.dll" -CompilerPath $compilerPath
```

成功时显示 `Built core and family assemblies`，并打印构建记录位置。`NeighborCourtyardsFramework/Mod/Assemblies/` 应出现 `NeighborCourtyards.Core.dll` 与 `NeighborCourtyards.Family.dll`。出现错误就先解决，不要继续复制上一次留下的 DLL。

脚本自动发现程序集目录；发现错误时传 `-ManagedPath '<包含 Assembly-CSharp.dll 的目录>'`。日志位于 `NeighborCourtyardsFramework/Validation/Build/`。重开终端后需要重新设置路径变量。

## 3. 生成原版外观示例

```powershell
./NeighborCourtyardsFramework/New-Neighbor.ps1 -OutputDirectory './Local/MyNeighbor' -PackageId 'yourname.myneighbor' -Prefix MYN -Name '我的邻居' -Author '你的名字'
```

成功时显示 `Created 我的邻居`，生成 `Local/MyNeighbor/About/About.xml` 与 `Defs/Neighbors.xml`。输出目录必须尚不存在；重复练习时换新目录和前缀，或直接编辑已生成文件。`Local/` 不进入公共仓库，发布自己的种族时另建仓库。

先认识三个词：`packageId` 是整个模组的唯一标识；`defName` 是一条游戏配置的唯一标识；`label` 是显示文字。命令中的 Name 只修改模组列表标题，游戏内角色和灯的标签仍需改 XML。

第一次只做一个可见改动：打开 `Local/MyNeighbor/Defs/Neighbors.xml`，找到 `<defName>MYN_WelcomeLamp</defName>`，把后面的 `<label>示例迎新灯</label>` 改为 `<label>我的迎新灯</label>`，以 UTF-8 保存。保留其他引用，暂时不要改 defName。

## 4. 第一次游戏验收

使用单独的游戏文件副本与测试存档，不覆盖正在玩的核心。复制后目录应如下，About 直接位于模组目录中，不多套一层 Mod：

```text
<测试游戏>/Mods/
  NeighborCourtyardsCore/   ← 源码 NeighborCourtyardsFramework/Mod 的内容
    About/About.xml
    Assemblies/...
    Textures/...
  MyNeighbor/              ← Local/MyNeighbor 的内容
    About/About.xml
    Defs/Neighbors.xml
```

另安装 Harmony 并按其要求排序；核心在其依赖之后，MyNeighbor 在核心之后。同一配置只加载一个 `local.neighborcourtyards.core`。

新建测试地图，在邻里建筑目录找到「我的迎新灯」。放置灯、一张可达原版空床、灯 8 格内的一份未禁止简易餐，保持安全和适宜温度。灯和床需属于玩家，开启迎新并把这个迎新点人口上限设为 1。

默认有效等待上限两天，实际时刻随机；条件受阻时暂停计时。练习可把 MYN_Species 内的 arrivalTicks 改为 15000，重启游戏并用新建迎新点测试；这是四分之一游戏日，发布前按玩法恢复或调整。

成功标志：角色从地图边缘走入，消耗一份餐并领取床位；手账显示姓名与家务岗位。补充餐食，观察吃饭、睡觉，在 Home 区域提供可清理污物；最后保存重载，姓名、岗位与床位仍在。没有人到来时先选中灯查看阻塞原因。

## 5. 再做毛茸茸外观

这一步需要自有或已获授权的三列两行透明角色图集。公开源码没有附带图集；作者本地试玩包虽有原型素材，其美术没有自动纳入 MIT。第一步用原版模板，无需先画图。

```powershell
./NeighborCourtyardsFuzzy/Prepare-Content.ps1 -AtlasPath 'D:/MyAssets/FuzzyNeighbor/Atlas.png'
./NeighborCourtyardsFuzzy/Build.ps1 -GameRoot $gameRoot
```

独立 Roslyn 路线在编译命令后加 `-CompilerPath $compilerPath`。成功后，绒绒族 Mod 下应有 `Textures/FuzzyNeighbor/Atlas.png` 和 `Assemblies/NeighborCourtyards.Fuzzy.dll`。将该 Mod 的内容安装到测试游戏 `Mods/NeighborCourtyardsFuzzy/`，在核心后加载，用「绒绒迎新灯」重复验收。

独立发布自己的种族时需改包 ID、Def 前缀、命名空间与程序集名，详见[外观开发](FuzzyRendering.md)。仅修改贴图或 XML 无需重新编译 C#，但要重启游戏确认新内容已加载。

## 6. 提交前检查

```powershell
./Tools/Check-Repository.ps1
git status --short
```

仓库检查通过不等于游戏功能通过。按[测试与排错](Testing.md)完成对应功能验证，再按[Git 上传指南](Publishing.md)提交。
