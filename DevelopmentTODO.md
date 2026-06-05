# VirtualPartner 当前 TODO

更新时间：2026-06-05

本文记录当前活跃 TODO。当前主线是 `LlmRelay` prompt 工程、StagePlan 2.0 质量优化与流式 StagePlan 执行体验；Stage 3 AgentLoop 不在当前活跃开发路径中。

## 当前状态

- [x] 第一阶段 Runtime 原型完成。
- [x] 第二阶段 Momotalk / TTS / ASR / StagePlan 2.0 可体验链路完成。
- [x] Windows Standalone 最小应用菜单与退出能力完成。
- [x] Stage 3 AgentRun / Tool Call 探索已迁出当前活跃基线并归档。
- [x] 根目录原文档已备份到 `Archive/RootDocs_20260525_prompt_pivot/`。
- [x] LLM Streaming StagePlan 执行链路完成并通过人工验收。
- [x] 校园户外背景接入完成并通过人工验收。

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
