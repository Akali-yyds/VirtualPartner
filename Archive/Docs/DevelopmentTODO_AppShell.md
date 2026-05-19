# VirtualPartner Standalone Basic App Shell 阶段性开发 TODO

本文档是 Standalone Basic App Shell 的阶段跟踪清单，来源于 `virtual_partner_standalone_basic_app_shell_需求文档.md`，但本轮范围已按实际发布体验收缩为 **菜单 + 退出**。

本阶段承接第二阶段已完成的 Runtime、Momotalk、StagePlan、TTS、ASR、Memory 与统一 Runtime Debug 基础，但不继续扩展角色表现、LLM、语音或记忆能力。目标是在 Windows Standalone 导出版本中补齐最基础的应用外壳能力：用户可以打开应用菜单，并从应用内部正常退出。

开发原则以 `ReadFirst.md` 为准：简单、高效、直接；高内聚、低耦合；先讨论明确，再开发；只做当前阶段需要的内容。

## 执行节奏

- [x] 进入某个阶段前，先讨论并明确该阶段的具体目标、边界和验收方式。
- [x] 明确后再开发，不提前实现后续阶段内容。
- [x] TODO 只记录阶段级任务和建议模块，不提前锁死未讨论过的类、字段、接口或路径细节。
- [x] 开发完成后，通过 Unity Editor Play、Standalone 导出版本和必要的 Inspector / Console 检查进行手动验收。
- [x] 验收通过后，勾选该阶段完成，再进入下一阶段。

## 本阶段总原则

- [x] App Shell 只作为应用级 UI 与生命周期管理外壳，不接管 Momotalk / StagePlan / TTS / ASR / Memory 主链路。
- [x] App Menu 不是 Runtime Debug，也不是 Momotalk 聊天窗口。
- [x] App Menu 本轮只提供继续与退出能力，不做空的设置入口。
- [x] 退出流程只调用现有 Runtime 已暴露的 Stop / Cancel / Shutdown 能力，不为退出重写各模块内部逻辑。
- [x] 本阶段不新增 LLM action 类型，不重构角色动作 Runtime。

## 本轮明确暂缓 / 不做内容

- [x] 暂缓 Settings Panel。
- [x] 暂缓设置保存与重置。
- [x] 暂缓窗口 / 全屏 / 分辨率切换，先使用 Unity 导出的默认全屏行为。
- [x] 暂缓 `F11` 全屏切换。
- [x] 暂缓 Master / TTS / UI 音量控制，先通过 Windows 系统音量控制。
- [x] 不做完整启动状态检查页。
- [x] 不做完整 Runtime 日志系统或日志查看器。
- [x] 不做后台运行策略、低功耗模式、最小化到托盘或系统托盘图标。
- [x] 不做透明桌宠窗口、窗口置顶、无边框拖拽桌面宠物模式。
- [x] 不做完整 About / 帮助页面。
- [x] 不做 Release / Debug 双模式打包系统。
- [x] 不做高级画质设置或多语言设置。
- [x] 不重构 Momotalk、StagePlan、TTS、ASR 或 Memory。
- [x] 不修改 TTS / ASR 本地服务协议。
- [x] 不新增角色动作、表情或骨骼控制能力。

## 阶段 2.16.1：App Shell Bootstrap 与 Esc 菜单

**阶段目标**

建立最小 App Shell 入口，让 Play 和 Windows Standalone 中都可以通过 `Esc` 打开 / 关闭应用菜单。

**前置条件**

- [x] 已阅读 `virtual_partner_standalone_basic_app_shell_需求文档.md`。
- [x] 已确认当前目标是补齐 Standalone 最小应用外壳，不扩展 Runtime 表现能力。
- [x] 已确认 App Menu 打开时不强制暂停角色 Runtime、Momotalk、TTS、ASR 或 StagePlan。

**开发任务**

- [x] 新增独立 App Shell 入口：`AppShellBootstrap`。
- [x] 新增 App Menu UI 管理入口：`AppMenuUIManager`。
- [x] 新增基础 App Menu Canvas / Panel。
- [x] 支持 `Esc` 打开 / 关闭 App Menu。
- [x] App Menu 本轮只包含 `继续` 和 `退出` 两个入口。
- [x] 处理基础 UI 焦点冲突，避免用户在文本输入框中输入时误触菜单。
- [x] 保持 App Shell 独立，不混进 Momotalk / StagePlan / Character 核心目录。

**手动验收标准**

- [x] 进入 Play 后，按 `Esc` 可以打开 App Menu。
- [x] App Menu 打开后，点击 `继续` 可以关闭菜单。
- [x] App Menu 打开后，再次按 `Esc` 可以关闭菜单。
- [x] 菜单在 1280x720 下可正常显示和点击。
- [x] 菜单不影响角色 Idle / FSM / Momotalk 的基础运行。
- [x] 菜单与退出确认在同一版完成；本轮未新增设置保存、分辨率或音量功能。

**验收记录**

- 用户已确认 Stage 2.16 App Shell MVP 菜单验收通过。
- 已新增程序化 App Menu，支持 `Esc` 打开 / 关闭、`继续` 返回，以及输入框焦点下首次 `Esc` 只取消输入焦点。

**完成状态**

- [x] 阶段完成。

## 阶段 2.16.2：退出功能

**阶段目标**

让 Windows Standalone 导出后可以通过应用内 UI 正常退出程序，并让同一流程在 Editor Play 中可测试。

**前置条件**

- [x] 阶段 2.16.1 已完成。
- [x] App Menu 已具备 `退出` 入口。
- [x] 已确认本阶段只做最小退出清理，不重写各 Runtime 模块内部停止逻辑。

**开发任务**

- [x] 新增应用生命周期控制入口：`AppLifecycleController`。
- [x] `退出` 入口调用退出请求流程。
- [x] 新增简单退出确认框。
- [x] 支持取消退出并返回 App Menu。
- [x] 确认退出时阻止重复点击导致重复退出请求。
- [x] 退出前尽量调用现有 LLM / StagePlan / TTS / ASR / FSM / locomotion 停止或取消接口。
- [x] Editor 中确认退出应停止 Play；Standalone 中确认退出应调用 `Application.Quit()`。
- [x] 本轮不支持 `Ctrl + Q` 请求退出。

**手动验收标准**

- [x] 点击 `退出` 后出现确认框。
- [x] 点击取消后返回 App Menu，不退出程序。
- [x] Editor Play 中确认退出可以停止 Play。
- [x] Windows Standalone 中确认退出可以关闭程序。
- [x] 重复点击退出不会导致异常。
- [x] 退出不需要通过任务栏右键或系统强制方式完成。

**验收记录**

- 用户已确认 Stage 2.16 App Shell MVP 退出功能验收通过。
- 已新增 `ShutdownRuntimeForQuit()`，退出前复用现有 Runtime 清理链路。

**完成状态**

- [x] 阶段完成。

## 阶段 2.16.3：收尾与归档

**阶段目标**

完成 App Shell MVP 整体验收，记录实现范围、已知限制和后续候选方向。

**前置条件**

- [x] 阶段 2.16.1 至 2.16.2 已完成。
- [x] Editor Play 中 App Shell 主流程已通过手动验收。
- [x] Windows Standalone 导出版本已完成最终验收。

**开发任务**

- [x] 检查 App Shell 不影响 Momotalk 文本聊天。
- [x] 检查 App Shell 不影响 StagePlan 播放。
- [x] 检查 App Shell 不影响 TTS / ASR / Memory 现有主流程。
- [x] 检查 Windows Standalone 中应用内退出可用。
- [x] 记录本阶段已知限制和后续候选方向。
- [x] 本轮仅更新 App Shell TODO，不新增独立归档文档。
- [x] 验收通过后再更新本 TODO 的完成勾选。

**手动验收标准**

- [x] Windows Standalone 导出后，可以通过应用内 UI 正常退出。
- [x] 不再需要通过任务栏右键关闭程序。
- [x] `Esc` 可以打开 / 关闭 App Menu。
- [x] App Menu 包含 `继续` 和 `退出`。
- [x] 点击 `退出` 后可以确认退出。
- [x] Editor Play 中点击 `退出` 可以停止 Play。
- [x] Standalone 中点击 `退出` 可以关闭程序。
- [x] 退出前会尽量调用已有 Runtime 停止接口。
- [x] 本阶段不影响 Momotalk、StagePlan、TTS、ASR、Memory。
- [x] 本阶段不新增 LLM action 类型。
- [x] 本阶段不重构角色动作 Runtime。
- [x] 本阶段不提供 Settings、分辨率切换或音量控制。

**验收记录**

- 用户已确认 Stage 2.16 App Shell MVP 整体验收通过。
- 已知限制：本轮只提供菜单与退出；Settings、分辨率 / 全屏切换、`F11`、音量控制继续暂缓。

**完成状态**

- [x] 阶段完成。
