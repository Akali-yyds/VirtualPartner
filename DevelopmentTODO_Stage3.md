# Stage 3 Agent 探索 TODO 状态

更新时间：2026-05-25

原 Stage 3 TODO 已归档到：

```text
Archive/RootDocs_20260525_prompt_pivot/DevelopmentTODO_Stage3.md
```

## 当前状态

Stage 3 AgentRun / ToolRegistry / ToolExecutor / AgentLoop / Streaming Tool Call 开发暂停。它不是当前活跃路线，也不作为 Momotalk 正式入口。

当前活跃路线见：

- [DevelopmentDirection.md](./DevelopmentDirection.md)
- [DevelopmentTODO.md](./DevelopmentTODO.md)

## 已完成但不在当前基线中的探索

以下能力曾作为 Stage 3 探索实现或讨论过，但已经从当前活跃项目基线迁出：

- AgentRun 生命周期。
- SegmentQueue 片段提交。
- Observation / Interrupt。
- CharacterToolRegistry / CharacterToolExecutor。
- query / validate / commit / finish / cancel 工具。
- MotionCritic / QualityCritic V0。
- Runtime Debug Agent 页。
- Agent prompt builder。
- Momotalk -> AgentRun 正式入口试验。
- 非流式与 streaming tool-call AgentLoop 试验。

这些内容可作为研究资料，不作为当前 TODO。

## 未验收事项

- [ ] Stage 3.11 未单独验收。
- [ ] Stage 3.12 未验收。
- [ ] AgentLoop 体验未达到预期，不继续按原计划推进。

## 当前暂停原因

- AgentLoop 没有稳定带来更好的动作质量。
- “流式工具调用”没有等价于“流式动作响应”。
- 模型仍可能延迟提交可见 Segment。
- 模型可能选择无关 preset animation 充数。
- 协议、prompt、工具职责和播放粒度都需要重新设计。

## 当前替代任务

当前工作转入 `LlmRelay` prompt 工程：

- [x] 精简默认示例注入。
- [x] 保留单一 `examples.md` 作为格式参考。
- [x] 停止默认注入 `bone-pose-examples.md`。
- [x] 精简 Runtime Generated Capabilities。
- [x] 增加 Clear Memory。
- [ ] 对提示词效果进行 Play Mode 人工验收。
- [ ] 继续根据实际日志调整 prompt。

## 重启条件

若未来重新启动 Stage 3 Agent，需要先生成新的 plan，且至少明确：

- 可见输出粒度。
- 多 Segment 生成策略。
- 响应速度目标。
- 参数动作优先级。
- 协议错误处理方式。
- 日志和人工验收方式。

在新 plan 生成并确认前，不继续勾选旧 Stage 3 TODO。
