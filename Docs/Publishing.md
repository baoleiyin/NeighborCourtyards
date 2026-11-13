# 上传 Git 与发布

上传的仓库根目录应是同时包含本页上级 `README.md`、两个项目目录与 `Tools` 的目录。不要把整个游戏工作区或 `LocalDemo` 试玩 ZIP 当成源码仓库。

## 本地提交

下面是维护者发布流程。克隆已有仓库时已经存在 `.git` 和 `origin`；只有新建仓库时才需要初始化：

```powershell
git init -b main
```

从仓库根目录检查待上传内容：

```powershell
./Tools/Check-Repository.ps1
git add .
git diff --cached --stat
git diff --cached --check
git status --short
git commit -m "Add Neighbor Courtyards framework and furry neighbor example"
```

Git 提交需要你已配置提交者姓名与邮箱；使用自己的身份，不将个人 Git 设置写进仓库。

在 GitHub、GitLab 或其他平台创建空仓库，避免平台先生成另一套 README 或许可证。以下 URL 是示意，替换成自己创建的地址后执行：

```powershell
git remote add origin https://github.com/YOUR_ACCOUNT/NeighborCourtyards.git
git push -u origin main
```

已有 `origin` 时先用 `git remote -v` 确认，不重复添加或强推。源码仓库和远端实际地址确定后，可将 README 的克隆说明补成真实 URL。

## 应上传的内容

包含项目源码、XML、脚本、教程、MIT、素材说明、测试程序及简短历史验证结果。`.gitignore` 排除了编译 DLL、美术、测试副本、存档和长日志；已被追踪的文件不受新 ignore 规则自动移除，提交前仍需查看暂存列表。

当前示例需要的图集没有包含在公开源码里。新开发者可先使用原版外观模板，之后按[素材准备步骤](GettingStarted.md)接入自己的图集。将来若明确开放美术，需要新增具体素材许可、更新准备步骤与忽略规则，不能只把代码的 MIT 自动套到美术上。

## 发布可运行版本

运行包需要先编译核心与扩展，准备已获分发授权的素材，在目标游戏环境测试后再打包。不要把开发者机器的游戏程序集或测试 QA DLL 放入发行包。

运行 `./Tools/Package-Source.ps1` 可打完整组合源码包，包含核心、绒绒族和顶层教程，按 Git 可见文件清单排除本地产物。输出到根目录 `Dist/`，不包含 `.git`。该清单包括未提交的新文件，打包前先查看 `git status --short`。

`NeighborCourtyardsFramework/Package-Source.ps1` 是原有的单独核心打包工具，只收集核心目录内容。`NeighborCourtyardsFuzzy/Package-Local.ps1` 是含现有素材的本地试玩包工具，不能直接作为已获得全部美术授权的公开 Release。绒绒族包内的构建脚本支持项目与模组两种目录布局。

发布说明写清版本、依赖、支持与已验证的运行时、安装目录、存档兼容与已知限制。当前核心 `0.8.0-dev.2`、绒绒族 `0.2.0` 与契约版本 `2` 用来标识不同层，不要混为一个版本号。

目前只发布开发版源码，工坊核心保持原版本。核心沿用 `local.neighborcourtyards.core`；开发者不要另发同包标识的核心副本，也不要在种族扩展中捆绑核心 DLL。正式核心仍由原工坊条目统一维护；更新前需要对现有系列和旧存档完成兼容验收。
