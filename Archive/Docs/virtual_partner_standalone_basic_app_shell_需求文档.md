# VirtualPartner / Toki Standalone 基础应用外壳需求文档

> 文档状态：阶段需求草案  
> 建议阶段名：Stage 2.16 / Standalone Basic App Shell  
> 目标角色：Toki / CH0187  
> 文档用途：将导出后作为独立 Unity 桌面应用所需的最基础功能整理为开发 Agent 可执行的阶段需求。本文档只覆盖当前必要内容，不扩展到完整产品化、日志系统、启动诊断、托盘、透明桌宠窗口等后续方向。

---

## 0. 文档定位

本文档用于承接 VirtualPartner 第二阶段已经完成的 Runtime、Momotalk、StagePlan、TTS、ASR、Memory 与统一 Runtime Debug 基础，在不推翻现有系统的前提下，为 Windows Standalone 导出版本补齐最基础的应用外壳能力。

本阶段的核心定位是：

> 让 VirtualPartner 在导出为 Unity Standalone 后，具备正常桌面应用应有的菜单、退出、基础设置保存、窗口显示控制和音量控制能力。

当前发现的问题是：

```text
项目在 Unity Editor Play 模式下可以正常测试，
但导出 Standalone 后缺少用户可见的手动退出入口，
只能通过任务栏右键等系统方式关闭。
```

因此本阶段不继续扩展角色动作能力、LLM 能力、TTS/ASR 能力或长期记忆能力，而是在现有 Runtime 外层补一个轻量的 App Shell。

---

## 1. 继承基础

当前项目已经具备以下基础：

- StagePlan 2.0 已作为第二阶段主格式。
- Momotalk 已作为正式文本交互入口。
- TTS 已接入本地服务包装器，并支持真实服务与降级模式。
- ASR 已接入本地服务，并支持识别结果填入输入框或自动发送。
- Memory 已接入 Markdown 长期记忆链路。
- Runtime Debug 已整合 LLM、StagePlan、TTS、ASR、Memory、Momotalk、Character、Expr/Mouth 等观察入口。
- 第一阶段已具备退出 Play / 组件 Disable 时停止 LLM pending request、FSM、timeline、debug owner、locomotion 和 Idle 的清理思路。

本阶段继续继承项目已有开发原则：

```text
简单、高效、直接。
高内聚、低耦合。
只做当前阶段需要的内容。
只改目标直接相关的文件。
不重复实现已有 Runtime 逻辑。
不把 Debug 面板当作正式产品 UI。
开发完成后按阶段验收并记录。
```

---

## 2. 本阶段总体目标

本阶段建议命名为：

> **Standalone Basic App Shell**

本阶段目标：

```text
导出 Windows Standalone 后，
用户可以通过应用内 UI 正常打开菜单、修改基础设置、切换窗口模式、调节音量，并通过明确的退出按钮关闭程序。
```

核心体验目标：

1. 不再需要通过任务栏右键或系统强制方式关闭程序。
2. `Esc` 可以打开 / 关闭应用菜单。
3. 应用菜单提供继续、设置、退出等基础入口。
4. 基础设置可以保存，重启后仍然生效。
5. 窗口 / 全屏 / 分辨率可以通过设置控制。
6. 主音量、TTS 音量、UI 音量可以通过设置控制。
7. 退出时尽量调用现有 Runtime 停止流程，不让 LLM、StagePlan、TTS、ASR、FSM 等模块处于未清理状态。

---

## 3. 本阶段明确不做内容

以下内容不作为本阶段目标，后续需要时单独开阶段：

1. 不做完整启动状态检查页。
2. 不做完整 Runtime 日志系统。
3. 不做错误日志文件查看器。
4. 不做后台运行策略 / 低功耗模式。
5. 不做最小化到托盘。
6. 不做系统托盘图标。
7. 不做透明桌宠窗口。
8. 不做窗口置顶。
9. 不做无边框拖拽桌面宠物模式。
10. 不做完整 About / 帮助页面。
11. 不做 Release / Debug 双模式打包系统。
12. 不做高级画质设置。
13. 不做多语言设置。
14. 不重构 Momotalk。
15. 不重构 StagePlan。
16. 不修改 TTS / ASR 本地服务协议。
17. 不改变 Memory 写入逻辑。
18. 不新增 LLM action 类型。
19. 不新增角色动作、表情、骨骼控制能力。

本阶段只补应用外壳最小闭环。

---

# Part A：应用菜单 App Menu

## A.1 功能定位

App Menu 是导出后用户可见的基础应用菜单。

它不是 Runtime Debug，也不是 Momotalk 聊天窗口。它只负责提供应用级操作入口：

- 返回应用。
- 打开设置。
- 退出程序。
- 可选：打开 / 关闭 Runtime Debug。

App Menu 不直接控制角色骨骼、不直接执行 StagePlan、不直接写 Memory、不直接调用 LLM。

## A.2 打开方式

建议支持：

```text
Esc：打开 / 关闭 App Menu
设置按钮：从 UI 中打开 App Menu，可选
```

规则：

1. App Menu 关闭时，按 `Esc` 打开。
2. App Menu 打开时，按 `Esc` 关闭。
3. App Menu 打开时，角色 Runtime 不暂停。
4. App Menu 打开时，Momotalk、TTS、ASR、StagePlan 是否继续运行，默认保持现状，不强行中断。
5. 如果当前输入框正在输入文本，`Esc` 的行为需要避免误触，优先让输入框失焦或关闭菜单，具体实现由 Agent 根据 Unity UI 焦点情况处理。

## A.3 菜单内容

本阶段最小菜单：

```text
AppMenuPanel
├── ContinueButton       // 继续 / 返回
├── SettingsButton       // 设置
└── QuitButton           // 退出
```

可选但建议保留：

```text
DeveloperSection
└── RuntimeDebugToggle   // 打开 / 关闭 Runtime Debug
```

Runtime Debug Toggle 是开发便利项，不作为正式用户功能。若实现成本较高，可以暂时不做。

## A.4 UI 形态建议

菜单建议采用全屏半透明遮罩 + 中央面板：

```text
AppMenuCanvas
└── DimOverlay
    └── AppMenuPanel
        ├── Title: VirtualPartner
        ├── ContinueButton
        ├── SettingsButton
        └── QuitButton
```

基础要求：

- 能在 1280×720 下正常显示。
- 不遮挡到无法操作的程度。
- 按钮尺寸适合鼠标点击。
- 样式保持简洁，不需要复杂动画。

## A.5 建议模块

```text
AppMenuUIManager
  打开 / 关闭 App Menu
  处理 Esc 输入
  切换 SettingsPanel
  调用 AppLifecycleController.RequestQuit()

AppMenuView
  绑定按钮
  显示菜单面板
  显示设置面板入口
```

---

# Part B：退出 / 关闭功能

## B.1 功能定位

退出功能是本阶段最高优先级。

目标：

```text
导出 Windows Standalone 后，用户可以通过应用内按钮正常关闭程序。
```

## B.2 退出入口

必须支持：

```text
App Menu → QuitButton → 退出程序
```

建议支持：

```text
Ctrl + Q → 请求退出
```

不建议本阶段做：

```text
Esc 直接退出
```

`Esc` 应先打开 App Menu，避免误触。

## B.3 退出确认

本阶段建议做一个简单确认框。

```text
QuitConfirmDialog
├── Text: 确定要退出 VirtualPartner 吗？
├── CancelButton
└── ConfirmQuitButton
```

规则：

1. 点击 QuitButton 后不立刻退出，先显示确认框。
2. 点击 Cancel 返回 App Menu。
3. 点击 ConfirmQuit 执行退出流程。
4. 如果实现确认框成本较高，可以先直接退出，但优先建议保留确认框。

## B.4 最小退出清理流程

本阶段不做复杂的完整优雅关闭系统，但退出前应尽量调用已有停止 / 保存逻辑。

建议流程：

```text
RequestQuit()
↓
阻止重复点击退出
↓
保存 AppSettings
↓
停止当前 LLM pending request（如果现有接口可用）
↓
停止当前 LLM / StagePlan 播放（如果现有接口可用）
↓
停止当前 TTS 播放（如果现有接口可用）
↓
停止 ASR listening / recording（如果现有接口可用）
↓
停止 FSM / locomotion（如果现有接口可用）
↓
触发 Application.Quit()
```

重要边界：

- 不为退出功能重写 LLM / StagePlan / TTS / ASR 的内部停止逻辑。
- 只调用现有模块已经暴露的 Stop / Cancel / Shutdown 接口。
- 如果某个模块没有可用接口，本阶段只记录 TODO，不强行改内部结构。
- 退出流程不能卡死等待本地服务响应。

## B.5 Editor 与 Standalone 区分

Unity Editor 中 `Application.Quit()` 不会真正退出 Editor。

建议封装：

```text
AppLifecycleController.QuitApplication()
```

行为：

```text
#if UNITY_EDITOR
  UnityEditor.EditorApplication.isPlaying = false;
#else
  Application.Quit();
#endif
```

这样同一个按钮在 Editor Play 模式和导出版本中都可测试。

## B.6 建议模块

```text
AppLifecycleController
  RequestQuit()
  CancelQuit()
  ConfirmQuit()
  SaveBeforeQuit()
  StopRuntimeBeforeQuit()
  QuitApplication()
```

AppLifecycleController 只负责应用生命周期，不负责 UI 细节。UI 通过 AppMenuUIManager 调用它。

---

# Part C：基础设置 Settings

## C.1 功能定位

SettingsPanel 用于配置导出应用最基础的用户偏好。

本阶段只做必要设置：

1. 显示模式。
2. 分辨率。
3. 主音量。
4. TTS 音量。
5. UI 音量。
6. ASR 自动发送开关，若现有配置已有则接入。

不做完整图形质量设置、不做服务地址编辑器、不做 API key 编辑器。

## C.2 设置入口

```text
App Menu → SettingsButton → SettingsPanel
```

SettingsPanel 可以在 AppMenuCanvas 内切换显示，也可以作为 App Menu 的子页面。

建议结构：

```text
SettingsPanel
├── DisplaySection
│   ├── FullscreenToggle
│   └── ResolutionDropdown
│
├── AudioSection
│   ├── MasterVolumeSlider
│   ├── TtsVolumeSlider
│   └── UiVolumeSlider
│
├── InteractionSection
│   └── AsrAutoSendToggle
│
└── Footer
    ├── ApplyButton
    ├── ResetButton
    └── BackButton
```

## C.3 设置保存方式

建议采用：

```text
AppSettingsService
```

保存内容可以使用 `PlayerPrefs` 或项目已有本地 JSON 配置方式。

建议规则：

```text
普通用户偏好：PlayerPrefs 或 UserSettings/AppSettings.json
敏感信息：继续使用现有 UserSettings/VirtualPartnerLlmConfig.json，不纳入本阶段设置面板
```

本阶段不要在设置面板中显示或编辑 API key。

## C.4 建议保存字段

```json
{
  "display": {
    "fullscreen": false,
    "resolutionWidth": 1280,
    "resolutionHeight": 720
  },
  "audio": {
    "masterVolume": 1.0,
    "ttsVolume": 1.0,
    "uiVolume": 1.0
  },
  "interaction": {
    "asrAutoSend": false
  },
  "developer": {
    "showRuntimeDebugOnStart": false
  }
}
```

说明：

- `showRuntimeDebugOnStart` 可选。
- 如果当前已有 Debug 显示配置，则优先复用。
- 如果 ASR 自动发送已有配置，则只做 UI 绑定，不新增第二套来源。

## C.5 设置加载时机

建议流程：

```text
AppBootstrap / AppShellBootstrap Awake
↓
AppSettingsService.Load()
↓
DisplaySettingsApplier.Apply()
↓
AudioSettingsApplier.Apply()
↓
InteractionSettingsApplier.Apply()
```

不要等用户打开设置面板时才应用保存的设置。

## C.6 设置应用规则

- 修改 Toggle / Slider 后可以立即应用。
- 分辨率可以点击 Apply 后应用。
- BackButton 返回 App Menu，不关闭整个菜单。
- ResetButton 恢复默认设置，但不清空 LLM/TTS/ASR/Memory 配置。

## C.7 默认值

建议默认值：

```text
fullscreen = false
resolution = 1280 × 720
masterVolume = 1.0
ttsVolume = 1.0
uiVolume = 1.0
asrAutoSend = false
showRuntimeDebugOnStart = false
```

---

# Part D：窗口 / 分辨率 / 全屏控制

## D.1 功能定位

导出 Standalone 后应提供基本显示控制，避免用户只能接受默认窗口行为。

## D.2 必须支持

```text
窗口化 / 全屏切换
基础分辨率选择
保存上次显示设置
启动时自动应用
```

## D.3 建议分辨率列表

先固定常用分辨率即可，不做复杂显示器检测：

```text
1280 × 720
1600 × 900
1920 × 1080
```

默认：

```text
1280 × 720
Windowed
```

## D.4 快捷键

建议支持：

```text
F11：切换全屏 / 窗口化
```

规则：

1. F11 切换后保存设置。
2. F11 不打开 SettingsPanel。
3. 如果实现焦点冲突处理成本较高，可以先只在非输入框焦点时生效。

## D.5 建议模块

```text
DisplaySettingsApplier
  ApplyResolution(width, height, fullscreen)
  ToggleFullscreen()
  LoadFromSettings()
  SaveToSettings()
```

---

# Part E：音量控制

## E.1 功能定位

当前项目已接入 TTS，后续也可能存在 UI 音效、角色动作音效或 BGM，因此需要最小音量设置。

本阶段只做基础音量，不做复杂音频混音管理器重构。

## E.2 音频通道建议

建议按以下逻辑区分：

```text
Master
  ├── TTS / Voice
  └── UI
```

如果项目中已有 AudioMixer，优先接入 AudioMixer。

如果当前没有 AudioMixer，可以先做最小实现：

- MasterVolume 影响全局 AudioListener.volume 或统一音量参数。
- TtsVolume 影响 TTS 使用的 AudioSource.volume。
- UiVolume 预留给 UI 音效 AudioSource。

## E.3 SettingsPanel 控件

```text
MasterVolumeSlider: 0.0 - 1.0
TtsVolumeSlider: 0.0 - 1.0
UiVolumeSlider: 0.0 - 1.0
```

规则：

1. Slider 改变后立即生效。
2. Slider 改变后保存设置。
3. TTS 正在播放时修改 TTS 音量应立即影响当前播放音频，若实现成本较高则至少影响下一次播放。

## E.4 建议模块

```text
AudioSettingsApplier
  ApplyMasterVolume(float value)
  ApplyTtsVolume(float value)
  ApplyUiVolume(float value)
  LoadFromSettings()
  SaveToSettings()
```

---

# Part F：与现有 Runtime 的关系

## F.1 不重构 Runtime 主链路

本阶段不改变：

```text
Momotalk
↓
LlmRelay
↓
StagePlanValidator
↓
StagePlanPlayer
↓
ActionCoordinator / Root / TTS / ASR / Memory
```

App Shell 只是外层应用级 UI 与生命周期管理。

## F.2 Runtime Debug 关系

Runtime Debug 仍然是开发调试工具，不作为正式设置 UI。

本阶段可以做：

```text
App Menu 中提供 Runtime Debug 显示 / 隐藏开关
```

但不做：

```text
把 Runtime Debug 改造成正式设置面板
把 App Settings 塞进 Runtime Debug 作为唯一入口
```

## F.3 Momotalk 关系

App Menu 和 Momotalk 是两个不同 UI 层级：

```text
Momotalk：角色聊天入口
App Menu：应用级设置和退出入口
```

规则：

1. 打开 App Menu 不强制关闭 Momotalk。
2. 打开 SettingsPanel 不清空 Momotalk 输入。
3. 点击退出前允许 Momotalk 当前状态自然停止，不需要保存页面状态以外的复杂信息。

## F.4 TTS / ASR 关系

本阶段只接入音量和退出停止：

- TTS 音量控制。
- 退出时停止 TTS 播放，若已有接口。
- ASR 自动发送开关，若已有配置。
- 退出时取消 ASR listening，若已有接口。

不做：

- TTS 服务启动状态页。
- ASR 服务启动状态页。
- 服务地址配置 UI。
- 本地 Python 服务重启按钮。

## F.5 Memory 关系

本阶段不改 Memory 逻辑。

退出时如果已有 Memory flush / save 接口，可以调用；如果没有，不为本阶段重构 Memory。

---

# Part G：建议目录与文件

建议新增目录：

```text
VirtualPartner/Assets/VirtualPartner/Runtime/AppShell/
```

建议新增脚本：

```text
AppShellBootstrap.cs
AppMenuUIManager.cs
AppLifecycleController.cs
AppSettingsService.cs
DisplaySettingsApplier.cs
AudioSettingsApplier.cs
SettingsPanelController.cs
QuitConfirmDialog.cs
```

建议新增 Prefab：

```text
VirtualPartner/Assets/VirtualPartner/Prefabs/AppShell/AppMenuCanvas.prefab
VirtualPartner/Assets/VirtualPartner/Prefabs/AppShell/SettingsPanel.prefab
VirtualPartner/Assets/VirtualPartner/Prefabs/AppShell/QuitConfirmDialog.prefab
```

如果项目现有 UI Prefab 目录不同，Agent 可以按现有目录结构放置，但应保持 AppShell 独立，不混进 StagePlan / Character / Momotalk 核心目录。

---

# Part H：建议开发顺序

## Phase 2.16.1：App Shell Bootstrap 与 Esc 菜单

目标：能在 Play 和 Standalone 中打开 / 关闭应用菜单。

任务：

- 新增 AppShellBootstrap。
- 新增 AppMenuCanvas。
- 新增 AppMenuUIManager。
- 支持 `Esc` 打开 / 关闭菜单。
- 菜单包含 Continue、Settings、Quit 三个按钮。

验收：

```text
进入 Play 后，按 Esc 可以打开 App Menu。
App Menu 打开后，点击 Continue 或再次按 Esc 可以关闭。
菜单不影响角色 Idle / FSM / Momotalk 的基础运行。
```

## Phase 2.16.2：退出功能

目标：导出后可以通过应用内按钮退出。

任务：

- 新增 AppLifecycleController。
- QuitButton 调用 RequestQuit。
- 新增简单 QuitConfirmDialog。
- Confirm 后保存设置并调用退出。
- Editor 下退出 Play，Standalone 下 Application.Quit。

验收：

```text
Editor Play 中点击退出可以停止 Play。
Windows Standalone 中点击退出可以关闭程序。
不需要通过任务栏右键关闭。
重复点击退出不会导致异常。
```

## Phase 2.16.3：基础设置保存

目标：设置可以保存并在下次启动应用时恢复。

任务：

- 新增 AppSettingsService。
- 保存 / 读取显示设置。
- 保存 / 读取音量设置。
- 保存 / 读取 ASR 自动发送开关，若已有配置可接入。
- SettingsPanel 展示当前设置。

验收：

```text
修改设置后关闭并重新打开应用，设置仍然保留。
Reset 只恢复 App Shell 设置，不清空 LLM/TTS/ASR/Memory 数据。
```

## Phase 2.16.4：窗口 / 全屏 / 分辨率

目标：支持基础显示模式控制。

任务：

- 新增 DisplaySettingsApplier。
- SettingsPanel 增加 FullscreenToggle。
- SettingsPanel 增加 ResolutionDropdown。
- 支持 F11 切换全屏。
- 启动时应用上次显示设置。

验收：

```text
可以在设置中切换窗口化 / 全屏。
可以选择 1280×720 / 1600×900 / 1920×1080。
F11 可以切换全屏。
重启后显示设置仍然生效。
```

## Phase 2.16.5：基础音量控制

目标：支持主音量、TTS 音量、UI 音量。

任务：

- 新增 AudioSettingsApplier。
- SettingsPanel 增加 MasterVolumeSlider。
- SettingsPanel 增加 TtsVolumeSlider。
- SettingsPanel 增加 UiVolumeSlider。
- 尝试接入当前 TTS AudioSource 或已有音频管理入口。

验收：

```text
Master 音量可以影响整体声音。
TTS 音量可以影响角色语音，至少影响下一次 TTS 播放。
UI 音量有独立保存字段，若当前没有 UI 音效可先预留。
重启后音量设置仍然保留。
```

## Phase 2.16.6：收尾与归档

目标：形成阶段验收记录和后续 TODO。

任务：

- 检查 App Shell 不影响 Momotalk / StagePlan / TTS / ASR / Memory。
- 检查导出 Standalone 后退出功能可用。
- 更新阶段 TODO。
- 更新 README 或新增本阶段归档说明。

验收：

```text
本阶段全部功能在 Editor Play 和 Windows Standalone 中通过手动验收。
文档记录当前实现范围、已知限制和后续候选方向。
```

---

# Part I：最终验收标准

本阶段最终验收按以下标准执行：

1. Windows Standalone 导出后，可以通过应用内 UI 正常退出。
2. 不再需要通过任务栏右键关闭程序。
3. `Esc` 可以打开 / 关闭 App Menu。
4. App Menu 至少包含 Continue、Settings、Quit。
5. 点击 Quit 后可以确认退出。
6. Editor Play 中点击 Quit 可以停止 Play。
7. Standalone 中点击 Quit 可以关闭程序。
8. 设置面板可以修改窗口化 / 全屏。
9. 设置面板可以修改分辨率。
10. 设置面板可以修改 Master / TTS / UI 音量。
11. 设置修改后重启仍然保留。
12. `F11` 可以切换全屏，若实现。
13. 退出前会尽量调用已有 Runtime 停止接口。
14. 本阶段不影响 Momotalk 文本聊天。
15. 本阶段不影响 StagePlan 播放。
16. 本阶段不影响 TTS / ASR / Memory 现有主流程。
17. 本阶段不新增 LLM action 类型。
18. 本阶段不重构角色动作 Runtime。

