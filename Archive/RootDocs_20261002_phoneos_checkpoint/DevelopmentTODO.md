# VirtualPartner 当前 TODO

更新时间：2026-06-10

本文记录当前活跃 TODO。当前主线是 `LlmRelay` prompt 工程、StagePlan 2.0 质量优化与流式 StagePlan 执行体验；Stage 3 AgentLoop 不在当前活跃开发路径中。

## 当前状态

- [x] 第一阶段 Runtime 原型完成。
- [x] 第二阶段 Momotalk / TTS / ASR / StagePlan 2.0 可体验链路完成。
- [x] Windows Standalone 最小应用菜单与退出能力完成。
- [x] Stage 3 AgentRun / Tool Call 探索已迁出当前活跃基线并归档。
- [x] 根目录原文档已备份到 `Archive/RootDocs_20260525_prompt_pivot/`。
- [x] LLM Streaming StagePlan 执行链路完成并通过人工验收。
- [x] 校园户外背景接入完成并通过人工验收。
- [x] 场景边界描边第一轮完成，当前效果已人工确认可接受。
- [x] 镜头控制第一轮完成：右下角入口、独占控制模式、旋转/平移/缩放、退出与重置。
- [x] Runtime Debug 面板改为默认隐藏，通过独立圆形按钮打开/关闭。

## 当前活跃主线：Prompt Engineering

目标：在不继续推进 AgentLoop 的前提下，提升 `LlmRelay` one-shot StagePlan 的动作质量、稳定性和可控性。

### 已完成改动

- [x] `examples.md` 改为唯一默认注入的示例文件。
- [x] `examples.md` 只保留参数动作、预设动作、位移动作三类格式参考。
- [x] 示例明确标注为格式参考，禁止按内容原样生成。
- [x] `bone-pose-examples.md` 保留为参考资产，但不再默认注入 `LlmRelay` prompt。
- [x] `stageplan-rules.md` 精简 StagePlan 2.0 核心规则。
- [x] `parameter-bones.md` 精简参数骨骼方法论，减少重复说明。
- [x] `preset-actions.md` 强化 preset animation 的边界：可用但不能替代参数动作。
- [x] `locomotion.md` 精简位移/朝向说明，并移除损坏文本。
- [x] `LlmRelay` 的 Runtime Generated Capabilities 去掉全量 Base Pose Local Axis Directions。
- [x] Runtime Generated Capabilities 保留运行时必要能力摘要。
- [x] Momotalk 增加独立 `Clear Memory` 入口。

### 待人工验收

- [ ] 普通问候不会照抄示例，不会生成过长或不合语境的动作。
- [ ] “跳舞 / 表演 / 做一段动作”类请求优先生成参数动作，而不是只调用 greeting / scissors / preset 充数。
- [ ] StagePlan JSON 稳定通过 validator。
- [ ] speech、expression、bonePose、animation、facing、locomotion 的组合没有明显互相打断。
- [ ] TTS/ASR、聊天记录、长期记忆、Clear Memory、Clear Chat History 行为正常。
- [ ] 日志能够支持定位输入、耗时、LLM 输出、校验结果和播放结果。

## 已完成阶段：LLM Streaming StagePlan

目标：保持 StagePlan 2.0 schema 不变，让 LLM 通过 OpenAI-compatible SSE 输出完整 StagePlan 时，运行时可在解析到完整 stage 后提前追加播放，缩短长动作请求的等待时间。

### 已完成改动

- [x] 在 `LlmRelay` 增加默认启用的 OpenAI-compatible SSE streaming 请求路径。
- [x] 在 `LlmRelay` 增加 Inspector 可调首播 stage 缓冲数，默认 2。
- [x] 在 `LlmRelay` 增加 Inspector 可调等待下一段时保持末姿势的开关。
- [x] 增量解析 `choices[].delta.content`，从完整 StagePlan 的 `stages` 数组中提取完整 stage。
- [x] 每个流式 stage 在追加播放前先包装为单段 StagePlan 并通过 validator。
- [x] 在 `StagePlanPlayer` 增加同一 owner/requestId 下的流式开始、追加、完成 API。
- [x] `StagePlanPlayer` 支持播放到队列尾但 LLM 流未结束时等待下一段，不触发 `StagePlanFinished`。
- [x] 流式结束且所有已追加 stages 播完后，只触发一次 `StagePlanFinished`。
- [x] 流式失败时停止当前 LLM StagePlan，并通过 Momotalk 错误路径暴露失败。
- [x] 保留 Inspector 关闭流式后的 one-shot 完整 JSON 路径。

### 已通过验收

- [x] Unity Console 无 C# 编译错误。
- [x] Inspector 关闭流式后，旧 one-shot `LlmRelay -> StagePlanPlayer` 路径仍可播放。
- [x] 简单聊天可通过流式路径显示 speech / expression，聊天记录行为正常。
- [x] “跳个舞吧”不等待完整 JSON 结束即可开始播放。
- [x] 首播缓冲 stage 数设置为 1、2、3 时，等待时间和段间空档符合预期。
- [x] 队列暂时播空但 LLM 还在输出时，角色保持末姿势等待下一段。
- [x] LLM stream / SSE / JSON / stage validation 失败时，立即停止当前播放并显示错误。
- [x] Memory 只在整轮流式 StagePlan 完成后判断，不在每个 stage 后提前写入。

## 已完成阶段：校园户外背景接入

目标：生成一张原创明亮校园庭院背景图，并接入 `VirtualPartner/Assets/Scenes/SampleScene.unity` 作为 `Main Camera` 后方的 3D 世界背景平面。保留现有房间模型、角色、Momotalk、LLM、StagePlan、TTS/ASR、Memory 链路不变。

### 已完成改动

- [x] 生成原创 21:9 校园庭院背景图，不使用官方素材。
- [x] 将最终图片保存到 `VirtualPartner/Assets/VirtualPartner/Art/Backgrounds/campus_courtyard_bg_21x9.png`。
- [x] 在 Unity 中导入图片并设置为普通 sRGB Texture。
- [x] 创建背景材质 `M_CampusCourtyardBackground.mat`，使用不受场景灯光影响的背景专用 Shader。
- [x] 为背景材质添加可调 `Blur Radius` 模糊参数，默认轻度虚化以降低视觉抢焦。
- [x] 在 `SampleScene.unity` 中创建或更新 `SceneBackground_CampusCourtyard` 后景平面。
- [x] 背景对象不参与碰撞、不影响 `RoomMoveArea`、障碍区域或角色控制。

### 已通过验收

- [x] Game 视图中默认 Skybox/蓝色背景被校园庭院背景替代。
- [x] 背景明亮、干净、户外校园感明确。
- [x] 调整 `M_CampusCourtyardBackground` 材质的 `Blur Radius` 时，`0` 为清晰、增大后背景自然虚化。
- [x] 背景不包含角色、文字、logo、校徽或可识别官方 IP 元素。
- [x] 当前房间模型和 Toki 仍正常显示，背景不遮挡角色、不穿到前景。
- [x] 21:9 视图覆盖完整，16:9 视图裁切自然。
- [x] Play Mode 下角色 idle、移动区域、障碍区域、Momotalk UI 正常。
- [x] 简单 Momotalk 消息仍能走 `LlmRelay -> StagePlanPlayer` 播放链路。

## 已完成阶段：场景边界描边与镜头控制第一轮

目标：在不改动 Momotalk、LLM、StagePlan、TTS/ASR、Memory 主链路的前提下，完成固定背景与可移动场景相机的结构整理，增加稳定的场景外轮廓描边，并提供第一轮用户可用的镜头控制模式。

### 已完成改动

- [x] 建立 `BackgroundCamera + SceneCamera` 结构：背景保持固定，场景相机负责房间、角色、描边和后续镜头移动。
- [x] `SceneCamera` 保留 `MainCamera` tag，并接入 `CinemachineBrain`，避免破坏现有 `Camera.main` 逻辑。
- [x] 新增 `VirtualSceneCameraController`，对外提供 `Orbit`、`Zoom`、`Pan`、`Focus`、`ResetView`，不感知 UI。
- [x] 新增 `VirtualSceneCameraInputDriver`，只在镜头模式启用时读取 PC 鼠标输入。
- [x] 滚轮缩放步长、最近缩放半径、最远缩放半径暴露到同一个输入组件，便于 Inspector 手动调整。
- [x] 新增独立 `SceneCameraControlCanvas`，右下角相机入口进入镜头控制模式；进入后显示 `Exit` 与 `Reset`。
- [x] 相机入口图标已旋转 180 度，避免视觉方向错误。
- [x] 镜头控制模式与 Momotalk 入口互斥，进入镜头模式时隐藏/禁用 Momotalk 入口，退出后恢复。
- [x] Runtime Debug 面板新增 `visible` 状态，Play Mode 默认不绘制。
- [x] 新增 `RuntimeDebugPanelToggleButton`，通过右下角独立按钮打开/关闭 Debug 面板。
- [x] 场景描边改为房间整体 mask silhouette 思路，只描场景最外轮廓，不对床、桌子、角色或房间内部边缘做逐对象描边。
- [x] 描边颜色、宽度、透明度、柔和度保留为可调参数。

### 已通过验证

- [x] Unity MCP 刷新后无 C# 编译错误。
- [x] `SampleScene.unity` 中存在 `SceneCameraControlCanvas`、相机入口按钮、Debug toggle 按钮、镜头输入组件和镜头控制器。
- [x] MCP 验证相机图标 Z 旋转为 180 度。
- [x] MCP 验证 Debug 面板默认 `visible=false`，Debug toggle 的 button、panel、background 引用齐全。
- [x] MCP 验证镜头缩放参数已写入场景：`wheelZoomStep=20`、`minZoomRadius=0.8`、`maxZoomRadius=24`。
- [x] 人工确认当前场景描边视觉效果可接受。

### 仍需手动关注

- [ ] 后续大幅旋转、进入房间内部、极限缩放时，继续观察描边是否只保留外轮廓。
- [ ] 后续根据手感调整 `wheelZoomStep`、`minZoomRadius`、`maxZoomRadius`。
- [ ] 后续如果要支持触屏或移动端，再新增触摸输入 Driver，不混进当前 PC 鼠标输入逻辑。

## 已完成阶段：TTS / ASR 服务优化

目标：在不改变主链路的前提下，修复 TTS/ASR 的资源与正确性问题、提升识别鲁棒性，并对 `TtsManager` 做低风险结构拆分。详细背景与暂缓项见根目录 `FutureOptimization.md`。

### 已完成改动

- [x] TTS：修复 streaming/加载/Mock `AudioClip` 原生内存泄漏（切换/停止时 `Destroy`）。
- [x] TTS：health/voice 元数据带 TTL 缓存（默认 30s，失败失效），去掉"每句话一次 `/health`"。
- [x] TTS：`AbortActiveRequest` 在打断路径显式 `Dispose` UnityWebRequest。
- [x] TTS：结构拆分阶段一——`StreamingPcmBuffer` / `StreamingPcmDownloadHandler` 提取到 `TtsStreamingPcmBuffer.cs`，WAV 写入提取到 `TtsWavWriter.cs`。
- [x] TTS：结构拆分阶段二·缓存——新增 `TtsCache.cs`（key/path 推导 + 原子写盘），缓存逻辑逐字迁移，既有缓存仍命中。
- [x] ASR：删除死代码 `run_asr_session` 与冗余 `ACTIVE_CANCEL_EVENT`，统一取消路径。
- [x] ASR：识别解码移到独立 worker 线程，不再阻塞常驻麦克风采集。
- [x] ASR：接入 silero VAD 做分段（替换裸能量阈值），清理无用累加器与 `energy_threshold` 配置。
- [x] ASR：`/asr/start` 预热期返回 425（warming up）而非 503，纳入麦克风预热状态判断。

### 已通过验收

- [x] Unity Console 无 C# 编译错误（TtsManager 及新增文件）。
- [x] Python 服务 `py_compile` 通过、`config.json` 合法、模块可导入。
- [x] Play Mode：TTS 连续发声/流式/缓存命中/打断/降级正常，原生内存不随发声累积。
- [x] Play Mode：ASR silero VAD 分段、连续识别、取消、超时、预热 425 行为正常。

### 暂缓项（见 FutureOptimization.md）

- [ ] ASR 标点恢复（CTC 模型无标点 token，需独立标点模型，暂缓）。
- [ ] TTS 结构拆分阶段二·剩余（`ITtsProvider` 策略 + `TtsAudioStreamPlayer`）：纯重构、当前只有一个真实引擎、风险高，按需触发。
- [ ] ASR VAD 阈值自适应：以实际使用反馈驱动，不提前优化。

## Prompt 质量原则

- [ ] 示例只教格式，不教固定答案。
- [ ] 参数动作是核心能力，preset animation 是辅助能力。
- [ ] 对复杂动作请求，应鼓励多 stage 编排，而不是压缩成最短合法 JSON。
- [ ] 对简单聊天，应允许短 speech + 轻表情/轻动作，不强迫复杂演出。
- [ ] Prompt 不重复注入同一类知识，避免上下文噪声。
- [ ] Runtime Generated Capabilities 只提供当前角色真实可用能力，不承担长篇教程角色。

## 暂停事项

- [ ] 暂停继续开发 Stage 3.11 / 3.12 AgentLoop。
- [ ] 暂停把 Momotalk 正式入口迁到 tool-call AgentRun。
- [ ] 暂停新增工具注册、工具历史、Prompt Debug、AgentLoop streaming 等调试 UI。
- [ ] 暂停把普通 assistant 文本兜底转换为 speech Segment。

## 后续可选方向

- [ ] 为 `LlmRelay` 增加更细的交互日志文档，一次对话一份，记录 prompt、请求、响应、耗时、validator 和播放结果。
- [ ] 针对“长舞蹈 / 表演”类需求建立专门 prompt 规则，但仍避免可复制示例锚定。
- [ ] 回归评估 Agent 方向是否值得重启，前提是先解决响应速度、动作粒度、工具协议和 preset 偷懒问题。
- [ ] 在效果稳定后，再考虑重新拆分 Prompt / Memory / Runtime Capabilities 的注入策略。
