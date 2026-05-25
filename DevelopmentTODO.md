# VirtualPartner 当前 TODO

更新时间：2026-05-25

本文记录当前活跃 TODO。当前主线是 `LlmRelay` prompt 工程与 one-shot StagePlan 质量优化；Stage 3 AgentLoop 不在当前活跃开发路径中。

## 当前状态

- [x] 第一阶段 Runtime 原型完成。
- [x] 第二阶段 Momotalk / TTS / ASR / StagePlan 2.0 可体验链路完成。
- [x] Windows Standalone 最小应用菜单与退出能力完成。
- [x] Stage 3 AgentRun / Tool Call 探索已迁出当前活跃基线并归档。
- [x] 根目录原文档已备份到 `Archive/RootDocs_20260525_prompt_pivot/`。

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
