# VirtualPartner Standalone Basic App Shell 阶段性开发 TODO

本文档是 Standalone Basic App Shell 的阶段跟踪清单，来源于 `virtual_partner_standalone_basic_app_shell_需求文档.md`。

本阶段承接第二阶段已完成的 Runtime、Momotalk、StagePlan、TTS、ASR、Memory 与统一 Runtime Debug 基础，但不继续扩展角色表现、LLM、语音或记忆能力。目标是在 Windows Standalone 导出版本中补齐正常桌面应用所需的基础应用外壳能力。

开发原则以 `ReadFirst.md` 为准：简单、高效、直接；高内聚、低耦合；先讨论明确，再开发；只做当前阶段需要的内容。

## 执行节奏

- [ ] 进入某个阶段前，先讨论并明确该阶段的具体目标、边界和验收方式。
- [ ] 明确后再开发，不提前实现后续阶段内容。
- [ ] TODO 只记录阶段级任务和建议模块，不提前锁死未讨论过的类、字段、接口或路径细节。
- [ ] 开发完成后，通过 Unity Editor Play、Standalone 导出版本和必要的 Inspector / Console 检查进行手动验收。
- [ ] 验收通过后，勾选该阶段完成，再进入下一阶段。

## 本阶段总原则

- [ ] App Shell 只作为应用级 UI 与生命周期管理外壳，不接管 Momotalk / StagePlan / TTS / ASR / Memory 主链路。
- [ ] App Menu 不是 Runtime Debug，也不是 Momotalk 聊天窗口。
- [ ] 退出流程只调用现有 Runtime 已暴露的 Stop / Cancel / Shutdown 能力，不为退出重写各模块内部逻辑。
- [ ] Settings 只保存普通用户偏好，不显示或编辑 API key。
- [ ] 设置重置只恢复 App Shell 设置，不清空 LLM / TTS / ASR / Memory 数据。
- [ ] 本阶段不新增 LLM action 类型，不重构角色动作 Runtime。

## 本阶段明确不做内容

- [ ] 不做完整启动状态检查页。
- [ ] 不做完整 Runtime 日志系统或日志查看器。
- [ ] 不做后台运行策略、低功耗模式、最小化到托盘或系统托盘图标。
- [ ] 不做透明桌宠窗口、窗口置顶、无边框拖拽桌面宠物模式。
- [ ] 不做完整 About / 帮助页面。
- [ ] 不做 Release / Debug 双模式打包系统。
- [ ] 不做高级画质设置或多语言设置。
- [ ] 不重构 Momotalk、StagePlan、TTS、ASR 或 Memory。
- [ ] 不修改 TTS / ASR 本地服务协议。
- [ ] 不新增角色动作、表情或骨骼控制能力。

## 阶段 2.16.1：App Shell Bootstrap 与 Esc 菜单

**阶段目标**

建立最小 App Shell 入口，让 Play 和 Windows Standalone 中都可以通过 `Esc` 打开 / 关闭应用菜单。

**前置条件**

- [ ] 已阅读 `virtual_partner_standalone_basic_app_shell_需求文档.md`。
- [ ] 已确认当前目标是补齐 Standalone 基础应用外壳，不扩展 Runtime 表现能力。
- [ ] 已确认 App Menu 打开时不强制暂停角色 Runtime、Momotalk、TTS、ASR 或 StagePlan。

**开发任务**

- [ ] 新增独立 App Shell 入口，建议模块为 `AppShellBootstrap`，具体命名可在阶段讨论时确认。
- [ ] 新增 App Menu UI 管理入口，建议模块为 `AppMenuUIManager`。
- [ ] 新增基础 App Menu Canvas / Panel。
- [ ] 支持 `Esc` 打开 / 关闭 App Menu。
- [ ] App Menu 至少包含 `Continue`、`Settings`、`Quit` 三个入口。
- [ ] 处理基础 UI 焦点冲突，避免用户在文本输入框中输入时误触菜单。
- [ ] 保持 App Shell 独立，不混进 Momotalk / StagePlan / Character 核心目录。

**手动验收标准**

- [ ] 进入 Play 后，按 `Esc` 可以打开 App Menu。
- [ ] App Menu 打开后，点击 `Continue` 可以关闭菜单。
- [ ] App Menu 打开后，再次按 `Esc` 可以关闭菜单。
- [ ] 菜单在 1280x720 下可正常显示和点击。
- [ ] 菜单不影响角色 Idle / FSM / Momotalk 的基础运行。
- [ ] 本阶段不新增退出、设置保存、分辨率或音量功能。

**验收记录**

- 待验收。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.16.2：退出功能

**阶段目标**

让 Windows Standalone 导出后可以通过应用内 UI 正常退出程序，并让同一流程在 Editor Play 中可测试。

**前置条件**

- [ ] 阶段 2.16.1 已完成。
- [ ] App Menu 已具备 `Quit` 入口。
- [ ] 已确认本阶段只做最小退出清理，不重写各 Runtime 模块内部停止逻辑。

**开发任务**

- [ ] 新增应用生命周期控制入口，建议模块为 `AppLifecycleController`。
- [ ] `Quit` 入口调用退出请求流程。
- [ ] 新增简单退出确认框，建议模块为 `QuitConfirmDialog`。
- [ ] 支持取消退出并返回 App Menu。
- [ ] 确认退出时阻止重复点击导致重复退出请求。
- [ ] 退出前保存当前 App Shell 设置。
- [ ] 退出前尽量调用现有 LLM / StagePlan / TTS / ASR / FSM / locomotion 停止或取消接口。
- [ ] Editor 中确认退出应停止 Play；Standalone 中确认退出应调用 `Application.Quit()`。
- [ ] 可选支持 `Ctrl + Q` 请求退出。

**手动验收标准**

- [ ] 点击 `Quit` 后出现确认框。
- [ ] 点击取消后返回 App Menu，不退出程序。
- [ ] Editor Play 中确认退出可以停止 Play。
- [ ] Windows Standalone 中确认退出可以关闭程序。
- [ ] 重复点击退出不会导致异常。
- [ ] 退出不需要通过任务栏右键或系统强制方式完成。

**验收记录**

- 待验收。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.16.3：基础设置保存

**阶段目标**

建立 App Shell 的最小设置保存与恢复能力，让普通用户偏好在重启后保留。

**前置条件**

- [ ] 阶段 2.16.2 已完成。
- [ ] App Menu 已具备 `Settings` 入口。
- [ ] 已确认本阶段不显示、不编辑 API key。

**开发任务**

- [ ] 新增设置读写入口，建议模块为 `AppSettingsService`。
- [ ] 新增 Settings Panel 控制入口，建议模块为 `SettingsPanelController`。
- [ ] 保存 / 读取显示设置：窗口模式、分辨率。
- [ ] 保存 / 读取音量设置：Master、TTS、UI。
- [ ] 保存 / 读取交互设置：ASR 自动发送开关。
- [ ] 设置面板展示当前设置值。
- [ ] `Back` 返回 App Menu，不关闭整个菜单。
- [ ] `Reset` 恢复 App Shell 默认设置，但不清空 LLM / TTS / ASR / Memory 配置。
- [ ] 启动时加载设置并交给后续 applier 使用；若对应 applier 尚未完成，可先保留数据入口。

**手动验收标准**

- [ ] 打开 Settings Panel 可以看到当前设置。
- [ ] 修改设置后能保存。
- [ ] 关闭并重新打开应用后，设置仍然保留。
- [ ] `Reset` 只恢复 App Shell 设置。
- [ ] 本阶段不修改 LLM 配置文件，不暴露 API key。

**验收记录**

- 待验收。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.16.4：窗口 / 全屏 / 分辨率

**阶段目标**

支持基础显示模式控制，让 Standalone 用户可以切换窗口化 / 全屏并选择常用分辨率。

**前置条件**

- [ ] 阶段 2.16.3 已完成。
- [ ] App Shell 设置已能保存和读取显示字段。
- [ ] Settings Panel 已具备显示设置区域。

**开发任务**

- [ ] 新增显示设置应用入口，建议模块为 `DisplaySettingsApplier`。
- [ ] 启动时应用已保存的窗口模式和分辨率。
- [ ] Settings Panel 增加 Fullscreen toggle。
- [ ] Settings Panel 增加 Resolution dropdown。
- [ ] 支持固定分辨率列表：1280x720、1600x900、1920x1080。
- [ ] 分辨率通过 `Apply` 后生效并保存。
- [ ] 支持 `F11` 切换窗口化 / 全屏，并保存设置。
- [ ] 避免在文本输入框聚焦时误触 `F11`，若处理成本较高，可先只在非输入焦点时生效。

**手动验收标准**

- [ ] 可以在设置中切换窗口化 / 全屏。
- [ ] 可以选择 1280x720 / 1600x900 / 1920x1080。
- [ ] 点击 `Apply` 后分辨率生效。
- [ ] `F11` 可以切换全屏 / 窗口化。
- [ ] 重启后显示设置仍然生效。

**验收记录**

- 待验收。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.16.5：基础音量控制

**阶段目标**

支持 Master、TTS、UI 三类最小音量设置，不引入复杂音频混音系统重构。

**前置条件**

- [ ] 阶段 2.16.3 已完成。
- [ ] Settings Panel 已具备音量设置区域。
- [ ] 已确认当前项目是否存在可复用 AudioMixer 或 TTS AudioSource 引用。

**开发任务**

- [ ] 新增音量设置应用入口，建议模块为 `AudioSettingsApplier`。
- [ ] Master volume 影响全局音量或项目当前统一音量入口。
- [ ] TTS volume 接入当前 TTS AudioSource 或已有 TTS 播放入口。
- [ ] UI volume 保留独立设置字段；如当前暂无 UI 音效，可先作为预留配置保存。
- [ ] Settings Panel 增加 Master volume slider。
- [ ] Settings Panel 增加 TTS volume slider。
- [ ] Settings Panel 增加 UI volume slider。
- [ ] Slider 改变后立即应用并保存。
- [ ] TTS 播放中修改 TTS 音量应尽量立即生效，若成本较高，至少影响下一次 TTS 播放。

**手动验收标准**

- [ ] Master 音量可以影响整体声音。
- [ ] TTS 音量可以影响角色语音，至少影响下一次 TTS 播放。
- [ ] UI 音量有独立保存字段。
- [ ] 重启后音量设置仍然保留。
- [ ] 本阶段不重构完整音频系统。

**验收记录**

- 待验收。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.16.6：收尾与归档

**阶段目标**

完成 App Shell 阶段整体验收，记录实现范围、已知限制和后续候选方向。

**前置条件**

- [ ] 阶段 2.16.1 至 2.16.5 已完成。
- [ ] Editor Play 中 App Shell 主流程已通过手动验收。
- [ ] 已准备 Windows Standalone 导出版本用于最终验收。

**开发任务**

- [ ] 检查 App Shell 不影响 Momotalk 文本聊天。
- [ ] 检查 App Shell 不影响 StagePlan 播放。
- [ ] 检查 App Shell 不影响 TTS / ASR / Memory 现有主流程。
- [ ] 检查 Windows Standalone 中应用内退出可用。
- [ ] 检查设置保存、窗口控制、音量控制在重启后仍然有效。
- [ ] 记录本阶段已知限制和后续候选方向。
- [ ] 按需要新增或更新 App Shell 阶段归档文档。
- [ ] 验收通过后再更新本 TODO 的完成勾选。

**手动验收标准**

- [ ] Windows Standalone 导出后，可以通过应用内 UI 正常退出。
- [ ] 不再需要通过任务栏右键关闭程序。
- [ ] `Esc` 可以打开 / 关闭 App Menu。
- [ ] App Menu 至少包含 `Continue`、`Settings`、`Quit`。
- [ ] 点击 `Quit` 后可以确认退出。
- [ ] Editor Play 中点击 `Quit` 可以停止 Play。
- [ ] Standalone 中点击 `Quit` 可以关闭程序。
- [ ] 设置面板可以修改窗口化 / 全屏。
- [ ] 设置面板可以修改分辨率。
- [ ] 设置面板可以修改 Master / TTS / UI 音量。
- [ ] 设置修改后重启仍然保留。
- [ ] `F11` 可以切换全屏。
- [ ] 退出前会尽量调用已有 Runtime 停止接口。
- [ ] 本阶段不影响 Momotalk、StagePlan、TTS、ASR、Memory。
- [ ] 本阶段不新增 LLM action 类型。
- [ ] 本阶段不重构角色动作 Runtime。

**验收记录**

- 待验收。

**完成状态**

- [ ] 阶段完成。
