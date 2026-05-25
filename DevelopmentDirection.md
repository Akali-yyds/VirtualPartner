# VirtualPartner 当前开发方向

更新时间：2026-05-25

本文记录当前活跃开发方向。历史 AgentRun / ToolRegistry / AgentLoop 探索已经归档，不再作为当前主链路。

## 1. 当前产品链路

当前 Momotalk 正式入口使用稳定的 one-shot StagePlan 链路：

```text
用户文本或 ASR 文本
-> MomotalkConversationController
-> LlmRelay
-> LLM 生成完整 StagePlan 2.0 JSON
-> StagePlanValidator 校验
-> StagePlanPlayer 按 stage 执行
-> speech / expression / bonePose / animation / facing / locomotion
```

这条链路继续承担当前可体验版本的角色反馈。后续优化优先围绕 prompt、StagePlan 质量、动作表现和回归稳定性展开。

## 2. 当前问题判断

Stage 3 AgentLoop 探索验证了一个事实：仅把角色控制改造成 tool-call loop，并不能自动带来更好的动作体验。如果 prompt、工具职责、提交粒度和模型行为没有被准确治理，结果可能比 one-shot `LlmRelay` 更慢、更保守，也更难人工判断问题来源。

因此当前方向不是继续堆 Agent 工具链，而是先把现有 `LlmRelay` prompt 工程做好：

- 减少示例锚定，避免模型照抄 greeting / preset / short动作。
- 保留 StagePlan 2.0 的稳定契约，让 JSON 更容易通过校验。
- 强化参数动作生成，让 `bonePose` 不被预设动作替代。
- 压缩重复提示词，降低模型误解和上下文噪声。
- 用日志和实际播放效果评估动作质量，而不是只以编译或工具调用成功为验收标准。

## 3. Prompt 组成边界

`LlmRelay` 当前 prompt 由以下几类内容组成：

- 固定 StagePlan 规则：来自 `stageplan-rules.md`。
- 参数骨骼方法论：来自 `parameter-bones.md`。
- 预设动作边界：来自 `preset-actions.md`。
- 位移与朝向规则：来自 `locomotion.md`。
- 格式示例：来自 `examples.md`，只作为格式参考。
- Runtime Generated Capabilities：由运行时生成的能力摘要。
- Long Term Memory：由 `memorySystem.BuildPromptContext(characterId)` 在有内容时注入。

当前明确不默认注入：

- `bone-pose-examples.md` 的完整已验证示例。
- 全量可复制长动作答案。
- Stage 3 AgentLoop 工具说明。
- Debug / Tool Registry / 文件系统配置。

## 4. Runtime Generated Capabilities 的定位

Runtime Generated Capabilities 是运行时能力真源，用来告诉模型“这个角色此刻实际支持什么”。它不是动作示例，也不应该变成另一份冗长教程。

当前保留内容：

- 可控 semantic bones 摘要。
- primary direction single-axis effects 摘要。
- preset animations 列表。
- locomotion modes 列表。
- expressions 列表。

当前已移除或避免默认展开：

- 全量 Base Pose Local Axis Directions。
- 大量可复制 bonePose 组合。
- 与静态提示词重复的长篇解释。

## 5. 记忆与聊天

Momotalk 聊天记录和长期记忆是两套不同状态：

- Clear Chat History：清空当前聊天记录。
- Clear Memory：清空当前角色长期记忆文件，不清空聊天记录。

长期记忆仍只服务当前 `LlmRelay` prompt 注入，不在本阶段重新设计 MemoryJudge。

## 6. Agent 探索状态

Stage 3 AgentRun / ToolRegistry / AgentLoop 的代码和文档已经迁出当前活跃基线。它们是研究资产，不是当前产品路径。

后续如果重新启动 Agent 化，需要先重新定义：

- 什么是角色控制 Agent 的最小可见输出单位。
- commit 粒度如何保证及时响应和多段演出。
- tool-call 协议错误如何严格暴露而不是兜底掩盖。
- 如何避免模型用 preset 动作代替参数动作。
- 如何让日志清楚记录输入、耗时、工具、LLM 返回和播放结果。

在这些问题解决前，当前项目以 `LlmRelay` prompt 工程为主线。
