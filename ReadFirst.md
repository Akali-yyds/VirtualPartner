# Read First

更新时间：2026-05-25

本文是 VirtualPartner 协作开发规则。开始任何阶段前先读这里。

## 当前基线

当前活跃主链路是：

```text
Momotalk -> LlmRelay -> StagePlan 2.0 -> StagePlanValidator -> StagePlanPlayer
```

Stage 3 AgentRun / ToolRegistry / AgentLoop 是已归档探索，不是当前默认开发方向。不要在没有重新讨论和计划的情况下把 Momotalk 正式入口迁回 AgentLoop。

## 工作方式

- 先明确目标和边界，再写计划，再实现。
- 如果用户进入 plan 模式，在用户明确要求生成 plan 前，不生成 plan。
- 不为了“验收通过”牺牲实际体验。动作质量、响应速度、可理解性比勾选清单更重要。
- 遇到错误要暴露和定位，不用兜底规则掩盖问题。
- 不回退用户已有修改，除非用户明确要求。

## 当前实现原则

- StagePlan 2.0 是当前 Runtime 执行格式。
- 参数 `bonePose` 是核心能力，不能被 preset animation 长期替代。
- preset animation 可以作为辅助，但不能用无关预设动作冒充用户请求。
- 示例 prompt 只作为格式参考，不作为动作答案。
- Runtime Generated Capabilities 是运行时能力真源，应保持紧凑。
- Memory 和 Chat History 分开治理。

## 修改文档规则

- 根目录文档描述当前活跃基线，不把暂停或归档探索写成当前主线。
- 历史内容进入 `Archive/`。
- 如果更新前需要保留原文档，先复制到带日期和目的的备份目录。

## 验收规则

每次实现完成后，至少说明：

- 改了哪些文件。
- 当前链路是否改变。
- 是否影响 Momotalk、LlmRelay、StagePlan、Memory、TTS/ASR。
- 做过哪些检查。
- 还有哪些需要用户 Play Mode 手动确认。
