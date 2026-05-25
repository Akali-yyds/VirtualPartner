# Stage 3 Agent 探索归档说明

更新时间：2026-05-25

本文取代原 Stage 3 方向文档在根目录中的活跃叙事。原文档已备份到：

```text
Archive/RootDocs_20260525_prompt_pivot/DevelopmentDirection_stage3.md
```

## 当前结论

Stage 3 的 AgentRun / ToolRegistry / ToolExecutor / AgentLoop / Streaming Tool Call 探索已经暂停，并从当前活跃项目基线中迁出。

它不再作为当前 Momotalk 正式入口，不再作为当前开发主线，也不应被后续开发默认继续。

当前主线已经回到：

```text
Momotalk
-> LlmRelay
-> one-shot StagePlan 2.0
-> StagePlanValidator
-> StagePlanPlayer
```

## 暂停原因

这轮 Agent 化探索没有达到预期体验：

- 用户等待时间变长。
- 流式 tool call 没有自然转化为流式动作反馈。
- 模型倾向于一次性生成或提交少量 Segment。
- 普通聊天、复杂动作和工具调用之间的职责没有治理清楚。
- 模型容易用 preset animation 代替参数 `bonePose`。
- AgentLoop 的技术复杂度增加了，但动作质量没有稳定超过 `LlmRelay`。

因此当前项目不继续以“工具链完整度”作为成功标准，而是回到实际角色体验：响应是否快、动作是否丰富、StagePlan 是否稳定、用户是否能看懂反馈。

## 保留价值

Stage 3 探索仍然有研究价值：

- 明确了角色控制 Agent 的最小输出单位必须是可执行演出，而不是普通 assistant 文本。
- 明确了兜底转换会掩盖协议错误，不应作为长期策略。
- 明确了 commit 粒度和播放时机比“是否使用 tool call”更重要。
- 明确了 Debug 日志对定位 LLM 行为非常关键。
- 明确了提示词工程仍是角色动作质量的核心。

这些结论可以作为未来重新设计 AgentLoop 的输入，但不是当前实现计划。

## 如果未来重启 Agent

未来重启前必须重新设计以下问题：

- 如何让模型尽早提交最小 speech / 动作反馈。
- 如何把复杂演出拆成多个可播放 Segment。
- 如何在等待查询或思考期间保持用户可见反馈。
- 如何要求参数动作而不是 preset 充数。
- 如何记录每一轮输入、工具、LLM 输出、耗时、播放结果。
- 如何严格暴露协议错误，不做文本兜底。

只有这些问题有明确方案后，才考虑重新进入 AgentRun / Tool Call 开发。

## 当前不再沿用

- 不沿用 3.11 / 3.12 作为待验收主线。
- 不把 LocalAgentRunDriver 作为产品行为。
- 不把 legacy LlmRelay 迁出当前主链路。
- 不把 tool registry / debug tools 注入当前 LlmRelay prompt。
- 不把 AgentLoop 的协议假设套到当前 one-shot StagePlan。
