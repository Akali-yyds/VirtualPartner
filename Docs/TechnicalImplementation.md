# VirtualPartner 技术实现文档

更新时间：2026-10-10

本文根据当前工作区代码、Unity 场景、配置文件、进度记录和外部目录的只读检查整理。文档描述的是当前实现状态，不把实验功能或历史验收结果当作生产能力。

## 1. 系统定位

VirtualPartner 是一个 Windows 桌面虚拟陪伴系统。Unity 客户端负责虚拟手机、角色、场景、动作执行和状态展示；LLM 负责把用户输入转换成结构化行为计划；本地 TTS/ASR 服务负责语音输出和输入；外部 ModelRepairTool 负责将角色模型整理成可在目标 Unity/URP 工程中独立使用的资源。

当前默认角色是 Toki，角色 ID 为 `CH0187`。当前首个 Build Scene 是 `VirtualPartner/Assets/Scenes/PhoneOS.unity`；`SampleScene.unity` 保留用于旧链路和场景回归，`PhoneOS_VisualReview.unity` 是视觉验收场景，`MotionLab.unity` 是独立动作实验场景。

## 2. 总体架构

```text
用户文本 / 麦克风
        │
        ├─ PhoneLiveMomotalk
        │       └─ MomotalkConversationController
        │              ├─ ChatHistory / RequestLifecycle
        │              ├─ MemorySystem
        │              └─ LlmRelay
        │                      └─ OpenAI-compatible Chat Completions / SSE
        │                              └─ StagePlan 2.0 JSON
        │                                      ├─ StagePlanValidator
        │                                      └─ StagePlanPlayer
        │                                              ├─ TtsManager → TTS Wrapper → GPT-SoVITS
        │                                              ├─ Expression / Mouth
        │                                              ├─ bonePose / spatialPose / poseReset
        │                                              ├─ animation
        │                                              ├─ facing / locomotion
        │                                              └─ ActionCoordinator → 角色骨架
        │
        └─ PhoneLiveSettings / PhoneLiveCamera / PhoneLiveDebug

外部资产链：ModelRepairTool → FinishedRepairedModel → VirtualPartner/Assets/Character
发布链：Launcher → GPT-SoVITS、TTS、ASR → VirtualPartner.exe
```

核心设计是“业务表现层与运行时服务解耦”。手机界面只调用 `PhoneLiveRuntime` 暴露的现有运行时接口，不复制 LLM、历史、Memory、TTS 或 ASR 状态机。旧 IMGUI Debug 面板仍作为底层诊断实现，手机 Debug App 负责把它们映射成日常入口。

## 3. Unity 启动与运行时装配

`VirtualPartnerStage1Bootstrap` 是角色运行时的装配根。启动时依次完成：

1. 解析角色、骨骼、动作、TTS、ASR、Memory、FSM、Debug 和手机引用。
2. 配置 `IdleBaseProvider`、`AvatarPoseApplier`、`ActionCoordinator`、移动/朝向执行器、口型/表情和 `TtsManager`。
3. 配置 `StagePlanPlayer`，再配置 `AutonomousBehaviorScheduler` 和 `LlmRelay`。
4. 创建或绑定 `MomotalkConversationController`，注册到 `PhoneLiveRuntime`；如果没有手机运行时，则绑定旧的 `MomotalkUIManager`。
5. 捕获角色骨骼初始旋转，创建并绑定 `SpatialMotionRuntime`，启动角色注册和 Idle。

每帧由 Bootstrap 驱动 Idle、TTS、ASR、StagePlan、LLM、FSM、SpatialMotion 和 `ActionCoordinator`。退出时会停止请求、StagePlan、TTS、ASR、FSM、移动和 Debug 所有权，并注销角色。

多角色扩展使用 `CharacterRuntimeBinder → CharacterRuntimeContext → CharacterRegistry`。PhoneOS 联系人列表从注册表读取角色、头像、历史和运行时上下文，不直接依赖某个固定 Toki 对象。

## 4. PhoneOS 手机层

手机壳由 `PhonePresentationShell`、`PhoneAppHost`、`PhoneOSController`、`PhoneAppRegistry` 和 `PhoneOSStyle` 组成：

- `PhoneAppRegistry` 保存 App 定义和 prefab；当前主要 App 是 Momotalk、Settings、Camera、Debug。
- `PhoneAppHost` 管理单前台 App、缓存实例、App 生命周期、Back/Home/Recent、页面链接和最近任务缩略图。
- `PhonePreviewApp` 负责应用内页面、模态弹层、暂停/恢复；`PhoneTransitionCoordinator` 负责打开、退出和任务切换动画。
- `PhoneLiveRuntime` 持有 Conversation、Relay、ASR 和 Shell 的组合边界，并保持后台运行。隐藏手机不会停止 LLM、StagePlan、TTS、ASR 或 FSM。
- Settings 复用 `LlmRelay.CreateConfigDraft/ReloadConfig/StartConfigTest/SaveConfig`，不建立第二套 API Key 存储。显示设置使用 `PhoneSettingsStore` / `PhoneSettingsData`。
- Camera 复用 `VirtualSceneCameraController` 的平移、旋转、缩放和重置接口；PhoneOS 场景关闭旧的鼠标镜头输入，避免输入重复。
- Debug App 调用公开的服务操作，覆盖 LLM、StagePlan、TTS、ASR、Memory、FSM、Root、Bone、Expression/Mouth 等诊断入口，并将长内容放进只读文档页。

`PhoneLiveMomotalk` 的职责包括联系人生成、历史消息渲染、pending/typing 状态、草稿持久化、未读、聊天/Memory 清理、语音输入和真实发送。ASR 会绑定 Unity session token 和角色 ID；导航或关闭页面先取消归属，晚到的结果不会写入其他角色、页面或自动发送。

## 5. 对话、历史和 Memory

`MomotalkConversationController` 是唯一的对话业务拥有者，负责：

- 读取和保存聊天记录；
- 注册用户输入、请求、StagePlan 播放和错误状态；
- 订阅 `LlmRelay`、`StagePlanPlayer`、TTS 和 ASR 事件；
- 处理新请求替换旧 pending 请求、后台回复和未读；
- 在完整一轮结束后将用户文本与角色 speech 交给 Memory 系统。

`MomotalkHistoryStore` 使用 `UserData/ChatHistory/<characterId>.json`，最多保留 200 条消息，默认加载最近 50 条。消息记录包含 sender、text、UTC 时间、request/turn ID、stage/action 索引和 status。

`MemorySystem` 与聊天记录分开。`MemoryJudgeClient` 对完整对话轮次做结构化判断，`MarkdownMemoryStore` 将结果按类别写入 `UserData/Memory/<characterId>/` 下的 Markdown 文件，支持 `user_profile`、`preferences`、`project_notes`、`relationship_notes`、`important_events` 和 `conversation_summaries`。注入 prompt 时优先读取 `core` 和 `high` 重要度，并受最大字符数限制。清空聊天不会清空 Memory，清空 Memory 也不会删除聊天。

## 6. LLM 与 StagePlan 2.0

`LlmRelay` 从 Git 忽略的 `UserSettings/VirtualPartnerLlmConfig.json` 读取 API Key、模型、Chat Completions URL、JSON 输出开关、developer role 支持和交互超时。PhoneOS Settings 只通过 Relay 的 draft API 修改配置。

Prompt 由以下内容组合：角色设定、StagePlan 规则、参数骨骼规则、预设动作规则、位移/朝向规则、格式示例、具名手势、运行时能力摘要和长期 Memory。运行时能力由 `LlmPromptCapabilityBuilder` 根据实际骨骼、表达、预设动作、移动和空间保持状态生成；示例只用于协议格式说明。

StagePlan 数据结构为：

```json
{
  "schemaVersion": "2.0",
  "type": "stage_plan",
  "metadata": { "intent": "...", "mood": "..." },
  "stages": [
    { "actions": [
      { "type": "speech", "text": "...", "emotion": "...", "speed": 1.0 },
      { "type": "expression", "name": "...", "duration": 0.5 },
      { "type": "bonePose", "bones": [] },
      { "type": "spatialPose", "tracks": [], "completion": {} },
      { "type": "poseReset", "target": "all" },
      { "type": "animation", "name": "..." },
      { "type": "facing", "target": "..." },
      { "type": "locomotion", "mode": "..." }
    ]}
  ]
}
```

`StagePlanValidator` 在播放前验证 schema、stage/action 数量、语音内容、表达式、注册骨骼、预设动画、朝向、移动和空间轨迹。`spatialPose` 不能与同一 stage 的 direct `bonePose`、`animation`、`facing` 或 `locomotion` 混用，需要分 stage 排序；每个 stage 最多一个 speech action。

默认启用 SSE 流式路径。`StreamingSseParser` 拆分 `data:` 事件，`StagePlanIncrementalParser` 从尚未完整结束的 JSON 中提取已经闭合的 `stages` 对象。Relay 在达到首播缓冲数（当前默认 2）后，把每个 stage 单独交给 Player；流式队列暂时为空时可保持末姿势，流结束且所有 stage 播完后只完成一次请求。JSON、SSE、stage validation 或播放失败会进入 Momotalk 错误消息，不把普通文本静默伪装成 speech。

## 7. 角色动作执行

`StagePlanPlayer` 是动作调度器，按 owner/requestId 管理 stage 和 action 生命周期，发出 speech 开始、身体动作失败和 StagePlan 完成事件。`ActionCoordinator` 对 Debug、StagePlan、SpatialMotion、Preset、Locomotion 等来源做骨骼所有权仲裁，避免 Idle、调试操作和正式动作同时写同一关节。

已有动作类型：

- `speech`：调用 TTS，真实音频开始时再显示角色 speech；没有真实音频时按既有 fallback 状态完成。
- `expression` / Mouth：由 `ExpressionActionExecutor`、`MouthTextureController` 和 `SpeechMouthDriver` 控制表情、嘴型和语音口型。
- `bonePose`：使用 `BoneMapProfile` 的语义骨骼和局部旋转约束。
- `animation`：使用角色 `PresetAnimationProfile`，作为辅助表现能力。
- `facing` / `locomotion`：分别由 `RootOrientationController` 和 `LocomotionActionExecutor` 执行，受 `MovementConstraintController`、`RoomMoveArea`、`ObstacleArea` 限制。

## 8. 空间动作与等待姿态

空间动作是当前新增的正式执行能力，但仍处于动作阶段验收中。核心文件是 `SpatialMotionData`、`SpatialRigProfile`、`SpatialPoseBuffer` 和 `SpatialMotionRuntime`。

- 轨迹按 `leftArm`、`rightArm`、`support`、`torso`、`head` 分组，每条轨迹由有序 key、body/head/chest/shoulder anchor 和 completion（idle/hold/timed/restore）组成。
- 手臂使用双骨链 IK；头、躯干和骨盆可使用直接骨骼目标；手掌/手指方向可选用末端朝向约束。
- `SpatialPoseBuffer` 在脱离场景的 FK 缓冲中预检，不直接修改场景；检查可达距离、肘/膝 hint、姿态误差、脚支撑、躯干/头部净空和标定关节范围。
- 正式空间保持、局部接管、恢复和 `poseReset` 由控制组管理。请求等待期间由 `BeginThinking` 启动角色专用低优先级思考姿态；首个正式 stage 或请求结束时释放。思考姿态不能覆盖已预约的正式控制组。
- `TokiSpatialRig.asset` 保存当前 Toki 的方向、骨链长度、关节范围、净空和 thinking tracks。其他角色不能直接复用这一标定。
- `PerformanceLatency` 分别记录提交、首 token、首完整 stage、播放调度、首次思考写入、首次空间骨骼写入和语音开始等阶段。

当前限制是逐轴关节标定、双臂最终姿态碰撞、掌心旋转连续性、脚底支撑和动作自然度仍需人工及专项回归；球形/胶囊净空检查不能等同完整物理碰撞或平衡证明。

## 9. MotionLab 独立动作实验

`MotionLab.unity` 和 `Tools/MotionLab` 是与日常聊天隔离的可行性实验，不包含 `MomotalkConversationController`、`LlmRelay`、History 或 Memory。实验场景复制角色并独立配置 Coordinator、IK、Idle、TTS 和相机。

实验路线有三类：

1. Semantic：将英文自然语言转换成受限语义目标，再由 `MotionLabSemanticPlanner` 使用 Toki 标定的身体坐标生成 `spatialPose`。
2. MoMask：通过外部 HumanML3D 模型生成动作片段，再转换为固定 v1/HumanML22 clip。
3. DiP：使用动作前缀做短段连续生成，探测 5/10 step 的实时性与连续性。

`MotionLabClip` 固定 20 FPS、22 个 HumanML 关节、body-right/up/forward 坐标约定和父子层级；`MotionLabRetargeter` 保持目标骨长、做 250 ms 进入 blend，并通过 `ActionCoordinator` 写入角色。工具会保留原始结果、BVH/位置变体、重定向结果、帧时间、首动作、真实 TTS、underrun、CPU/RAM/VRAM 等证据。实验结果不能直接视为日常主链路已通过，也不会修改 StagePlan schema。

外部实验环境位于 `F:/Project/MotionExperiments`，与 GPT-SoVITS Python 环境分离。当前工作区中的 MotionLab 文件仍为未提交新增内容。

## 10. TTS 链路

Unity `TtsManager` 是播放端状态机，支持 Mock、真实 wrapper、缓存、缓冲 WAV、流式 PCM、文本 fallback、3D 音频、嘴型同步和打断释放。缓存 key/path 推导在 `TtsCache`，PCM ring buffer 和 download handler 在 `TtsStreamingPcmBuffer`，WAV 写入在 `TtsWavWriter`。

主链路如下：

```text
StagePlan speech
  → TtsManager POST http://127.0.0.1:8765/tts
  → TTS wrapper 校验 voiceId / reference audio / prompt text
  → POST http://127.0.0.1:9880/tts
  → GPT-SoVITS
  → WAV 或 chunked audio/raw
  → Unity AudioClip / streaming buffer
```

包装服务提供 `GET /health` 和 `POST /tts`。默认 voice 是 `toki_default`，参考音频和 prompt 为 `voices/toki/s6_g8/CH0187_MemorialLobby_1_1.*`，流式格式为单声道 32 kHz PCM16 little-endian，默认 `streamingMode=1`。外部便携包 `F:/Project/TTS/GPT-SoVITS` 使用独立 Python runtime、Toki `s6_g8` 权重和 CPU 友好策略（默认 3 线程、BelowNormal、常驻服务）。

## 11. ASR 链路

Unity `AsrManager` 维护 Unity session token、真实/Mock provider、Fill-only/Auto-send 模式、轮询和取消。真实服务是 `VirtualPartner/LocalServices/ASR/asr_service.py`：

- HTTP：`GET /health`、`POST /asr/start`、`POST /asr/cancel`、`GET /asr/status?sessionId=...`；
- sherpa-onnx streaming Zipformer CTC 中文模型；
- Silero VAD 负责分段，默认阈值 `0.5`、最短静音 `0.8s`、最短语音 `0.25s`；
- 常驻麦克风采集线程与识别 worker 分离，避免解码阻塞采集；
- 模型/麦克风预热时返回 warming-up 状态，Unity 通过 session ownership 防止取消后的晚到结果污染当前页面。

当前服务配置默认 `127.0.0.1:8766`，模型文件位于被 Git 忽略的 `LocalServices/ASR/models`。CTC 模型不产生标点，独立标点恢复仍是暂缓项。

## 12. ModelRepairTool 资产链

外部 `F:/Project/ModelRepairTool` 是独立 Unity 编辑器工程。入口是 `Tools/Model Repair/Open Window` 和 `Tools/Model Repair/Repair Current Character`，核心实现为 `ModelRepairToolWindow.cs` 中的 `RepairContext`、`CharacterRepairExporter` 和 `RepairReport`。

修复过程大致为：

1. 从 `NeedRepairedModel/<Role>` 发现角色、Character ID、关联角色以及导出工程。
2. 创建或备份 `FinishedRepairedModel/<Role>`，建立标准输出目录。
3. 以 `<CharacterId>_Mesh.prefab`、Cafe Mesh、Halo 等核心 prefab 为根，通过 Unity `AssetDatabase.GetDependencies` 收集依赖。
4. 复制 prefab、模型、材质、贴图、动画、Addressable、Effects、Spine、Timeline、Audio 等资源，并为输出重写 GUID。
5. 从缺失资源库复制嘴型和默认 Mask 贴图，读取官方 prefab/material 的嘴型材质索引和默认 UV。
6. 生成角色专用 `CharacterMouthEventReceiver`，将组件写入主 Mesh prefab。
7. 为官方材质名生成目标工程可识别的私有 Shader，替换缺失 Shader GUID；无法恢复的效果材质使用通用 FX fallback。
8. 修复继承型皮肤缺少的材质/贴图，修补 prefab 材质槽，扫描输出并写入 `Report/RepairReport.md`。

当前 ModelRepairTool 输出中已确认有 Toki、Toki-Combat、Toki-rabbit、Asuna、Karin、Neru 等包；Toki 报告对应 `CH0187`。修复 Shader 是近似实现，不能自动等同官方运行时画面；URP 下若出现紫色或不显示，应先检查 Shader 编译和主体 pass。

## 13. 场景与渲染

PhoneOS 场景保留 `SceneCamera`（`MainCamera` tag）和固定背景相机，支持：

- `VirtualSceneCameraController`：Orbit、Zoom、Pan、Focus、ResetView；
- Phone Camera App：摇杆平移/旋转和滑块缩放；
- Cinemachine/Brain 与既有 `Camera.main` 逻辑兼容；
- `SceneBoundaryProxy` + `SceneOutlineRendererFeature`：以房间整体 mask silhouette 描绘最外轮廓；
- 校园庭院背景平面固定在背景层，不参与角色碰撞；
- `RoomMoveArea`、`ObstacleArea`、`MovementConstraintController` 约束角色位移。

## 14. 发布与本地运行

开发模式下：

1. 打开 Unity `VirtualPartner` 工程，版本为 `6000.3.12f1`。
2. 启动外部 GPT-SoVITS（`F:/Project/TTS/GPT-SoVITS/start.ps1`）和主项目的 TTS wrapper；或执行 `VirtualPartner/LocalServices/TTS/start_tts_service.bat`。
3. ASR 执行 `setup_venv.bat` 后运行 `start_asr_service.bat`。
4. 在 PhoneOS Settings → LLM connection 填写并测试 OpenAI-compatible API 配置，再显式保存。
5. 运行 `PhoneOS.unity`，从 Momotalk 发送文本或开启麦克风。

发布模式由 `Launcher/VirtualPartnerLauncher` 启动。`launcher_config.v1.json` 描述 App、GPT-SoVITS、TTS、ASR、两个 Python runtime、健康检查 URL 和启动超时；Launcher 会验证包路径和端口，启动服务并等待健康检查，服务就绪后启动 Unity App，退出时停止整个进程树。发布脚本是 `Build-RuntimePayloads.ps1` 和 `Build-V1Release.ps1`，目标是可迁移的 Windows 绿色包。

## 15. 当前版本状态与风险

已形成的生产/日常能力：

- PhoneOS 四个 App 的运行时接入、导航、最近任务、草稿、未读和视觉基础；
- 真实对话的 LLM → StagePlan → TTS → 角色执行路径已具备；
- StagePlan SSE 流式分段播放、请求替换、历史/Memory 分离；
- TTS wrapper、GPT-SoVITS、ASR wrapper 的本地服务协议；
- 场景相机、背景、外轮廓描边和移动约束；
- Toki 的骨骼、口型、表情、Idle、FSM 与运行时 Profile。

仍需明确为未完成或需人工验收：

- 外部 DeepSeek/API Key 的最终真实验收依赖本机有效配置；历史记录中曾出现 HTTP 401，不能用本地 fixture 代替外部服务结论。
- 中文输入法候选行为、真实麦克风识别准确率、镜头手感、页面细节和完整移动场景仍需实机确认。
- `spatialPose` 的逐轴关节限制、双臂/头部最终碰撞、恢复支撑、掌心连续性和动作自然度未完成全量验收。
- MotionLab 的 Semantic/MoMask/DiP 路线尚未确定哪一条接入日常对话；实验生成成功不等于角色动作语义正确。
- TTS 服务启动脚本和旧发布配置中存在固定路径/运行时目录假设，迁移机器时需要按发布包配置重新检查。

## 16. 当前工作区和验证边界

当前分支为 `codex/phoneos-dev`，最新提交为 `b8e4e3a`（2026-10-09）。本次只读检查发现：

- 已提交代码包含 PhoneOS、空间动作和文档 checkpoint；
- `SpatialMotionRuntime.cs` 有 1 个未提交修改，增加末帧几何误差测量；
- MotionLab 工具、场景、实验 Runtime/Editor 文件和 `.claude/settings.local.json` 为未提交新增；
- `Assets/_Recovery/0 (1).unity` 及其 meta 文件为未提交恢复文件；
- 本次未启动 Unity、未调用外部 LLM、未启动 TTS/ASR，也未修改任何代码；Python 命令行检查不可用，因为当前 PowerShell PATH 没有 `python` 命令。因此本文是静态实现梳理，不是本机运行验收报告。

