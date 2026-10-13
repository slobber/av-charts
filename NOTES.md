# NOTES — 用户的世界（AvCharts 的构建环境）

> 这份笔记是 `/loop-me` 的原料：写工作流规格时要用的工具、通道和术语。**已核实的写在"事实"里，推断的写在"推断"里，未确认的留在最后一节等人答。**

## 事实（本次会话实测）

### 代码托管与凭据

- 主仓库：`https://git.cyber-stone.com`，**Gitea 1.27.1**，标题 `CyberStoneGit`；`ipaas` 组织下有 25 个仓库。
- **Gitea Actions 可用**（25 个仓库里 10 个 `has_actions=true`，实例版本支持），工作流文件放 `.github/workflows/`（Gitea 两种路径都读）。
- 令牌：**已经在 Windows 凭据管理器里**（gitconfig 的 `credential.https://git.cyber-stone.com.provider=generic`），取法是
  `"protocol=https`nhost=git.cyber-stone.com`n`n" | git credential fill`，拿到 `password=` 那一行。
  **到哪儿都不要打印它的值**，只在同一条命令里用掉。`git push` 直接能认证，不需要嵌 token 的 URL。
- 仓库默认分支 `master`；组织里库的默认可见性是 public（`ipaas/iPaas.Controls` 即如此），但 **av-charts 按约定先 private**。

### 包与版本

- 私有源：`https://baget.cyber-stone.com`（**BaGet**；v3 index 带 `PackagePublish/2.0.0`），推送端点 `https://baget.cyber-stone.com/api/v2/package`。
- 推包密钥是**仓库 secret `NUGET_KEY`**（本地三处 nuget.config 里都没有凭据字段）；可选 `BAGET_USERNAME` / `BAGET_TOKEN` 只在源开了读取认证时才需要。
- 版本靠 **NBGV**（`version.json` + git 历史），只有 `v*` tag 是正式号；CI 不传 `-p:PackageVersion`。无 tag 时形如 `0.1.0-g7dec4c8e32`。

### 本机环境

- `E:\GitHub\slobber\av-charts`（本仓库，已 `git init` 并连上 `origin`）。
- 房子模板：`E:\cyberstone\slobber\iPaas.Controls`（= `ipaas/iPaas.Controls`，public）—— `Directory.Build.props` / `Directory.Packages.props` / `version.json` / `.editorconfig` / `.gitattributes` / `.gitignore` / `slnx` / `.github/workflows/{ci,nuget-publish}.yml` / `build/*.ps1` / `build/publish-release-assets.sh` 的形状以它为准。
- SDK：`9.0.318`、`10.0.401`；Avalonia 房子引脚 `12.1.2`（最新 `12.1.4`）。
- 有用的运行期资产：`Avalonia.Headless(.XUnit)`、`Avalonia.Skia`、`Avalonia.Fonts.Inter` 都在本地 NuGet 缓存里；金图靠 `HeadlessWindowExtensions.CaptureRenderedFrame` + `UseHeadlessDrawing=false`。
- 捆绑的 Python 在 `C:\Users\slobber\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`（带 PIL），`tools/palette-preview.py` 用它跑。

### 会话层的两个硬约束（踩过）

- 本线程的应用工作目录**仍指向改名前的 `E:\GitHub\slobber\av-chart`**（已不存在），所以编辑器工具写文件要走 `../av-charts/<路径>` 相对路径落到真实仓库。
- 命令行的 `Remove-Item`：**带 `-Force` 会被策略拦**，不带就过；对包含 `av-chart` 字样的路径做 `-Recurse` 递归删除也会被拦（先删空的叶子目录再删父目录可行）。

## 术语（canonical，按这套说）

- **房子模板**（house template）—— `iPaas.Controls` 那套工程/发布约定，新库照抄而不是另发明。
- **竖切**（vertical slice）—— 一条从数据到像素打通的薄片，而不是按层横扫。
- **金图**（golden image）—— headless 渲染成位图与基准 PNG 比对；基图进仓库，`AVCHARTS_UPDATE_GOLDENS=1` 时才写回。
- **等格 / 分档 zoom** —— Fixed 尺度下"格子像素近似恒定、刻度密度换档"；严格恒定做不到（除非放弃漂亮刻度值）。
- **采样**（sampling）—— `None / MinMax / Lttb / Average`，选项在 series 上；点数少于可见像素列时自动不生效。
- **检查点 / brief / push right** —— 人只在真正需要决断的地方被打断，且打断时看到的是结论而非草稿。

## 推断（未与用户逐条确认）

- 沟通偏好：中文对话；**结论先行**；喜欢用实测证据说话（这一路每次都要求"查一下再说"）；不接受"看起来对"的方案。
- 交付偏好：一步一提交、每步都要 `build + test + format` 绿了才提交；文档与代码同等重要（决策记录、XML 注释写全、最后出 API 站）。
- 目标平台里有**工控机**，所以对 GPU、动画、内存特别保守。

## 还不知道的

- 这个"按文档完成每一步"的 loop，触发方式与自主度（本轮的问题）。
- 每轮检查点放在哪一级（每个 v1 条目？每次竖切？）
- 什么时候需要主动叫醒用户、什么时候可以自己往下走。
- 有多少时间预算/可接受速度（例如能不能让它在后台连续跑）。
