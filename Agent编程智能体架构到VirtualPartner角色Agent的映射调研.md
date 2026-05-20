# Agent 编程智能体架构到 VirtualPartner 角色 Agent 的映射调研

版本：v0.1  
日期：2026-05-19  
对象：VirtualPartner / Toki / StagePlan 2.0 / Streaming Direct BonePose Agent 方向

---

## 0. 结论摘要

本次阅读的 `ClaudeCode-origin.zip` 并不适合被当作“可直接复刻的官方 Claude Code 源码”。压缩包内 README 明确说明它是从 sourcemap 反向恢复并重组的 Claude Code 2.1.88 项目，不是官方上游仓库，且可能包含兼容层、stub 和不完整恢复内容。因此，本报告只把它作为 **Agent Harness 架构研究材料**，不建议逐字复刻实现，也不建议依赖其中任何非公开/不稳定细节。

但从核心链路看，它非常有参考价值。它展示了一种典型的编程 Agent 治理结构：

```text
LLM Query / Agent Loop
-> Tool Use Streaming
-> Tool Registry / Tool Schema
-> Tool Execution Pipeline
-> Permission / Validation / Hooks
-> Tool Result 回写模型上下文
-> LLM 继续下一轮决策
```

这与 VirtualPartner 当前要做的方向高度同构：

```text
编程 Agent：LLM 规划、读文件、写代码、跑测试、看结果、继续修改
角色 Agent：LLM 规划、查询身体能力、编写动作参数、提交演出片段、观察 Runtime、继续编排
```

因此，VirtualPartner 第三阶段不应被定义为“动作工具化”这么窄，而应定义为：

> **面向虚拟角色身体控制的 Streaming Direct BonePose Agent Harness。**

也就是说，LLM 不再一次性返回完整 StagePlan，而是在一次 AgentRun 中持续调用角色身体工具，阶段性提交 speech / expression / bonePose / facing / locomotion 等演出片段；LLM 仍直接填写骨骼参数，Runtime 负责校验、排队、执行、观察和中断治理。

---

## 1. 阅读范围与可信度说明

### 1.1 源码来源说明

压缩包根目录 README 写明：

- 项目是 `Claude Code 2.1.88 Recovered`；
- 它是由 `cli.js.map` 反向恢复并重组的 npm 项目；
- 不是官方上游源码仓库；
- 由于反向 sourcemap 恢复不完整，项目包含兼容层、生成 shim 和 stub；
- 适合研究、调试和恢复工作，但不保证与官方发布包完全一致。

因此，本报告只抽取 **架构模式**，不做以下事情：

- 不逐字复制实现；
- 不把该仓库视为官方 Claude Code 设计文档；
- 不依赖其中可能属于恢复误差的细节；
- 不建议在 VirtualPartner 中直接移植源码。

### 1.2 精读范围

本次阅读重点不在 UI、兼容 shim、外围命令等细枝末节，而是围绕 Agent Harness 的核心路径精读：

| 关注点 | 重点文件 |
|---|---|
| Tool 抽象 | `src/Tool.ts` |
| Tool 执行管线 | `src/services/tools/toolExecution.ts` |
| 流式工具执行 | `src/services/tools/StreamingToolExecutor.ts` |
| Agent 主循环 / Query loop | `src/query.ts` |
| 工具编排与并发分组 | `src/services/tools/toolOrchestration.ts` |
| 工具注册与装配 | `src/tools.ts` |
| 工具搜索 / 延迟暴露 | `src/tools/ToolSearchTool/ToolSearchTool.ts` |
| 子 Agent / Task | `src/tools/AgentTool/runAgent.ts`, `src/tools/AgentTool/AgentTool.tsx` |
| Plan / Todo 机制 | `src/tools/EnterPlanModeTool/*`, `src/tools/ExitPlanModeTool/*`, `src/tools/TodoWriteTool/*` |
| Hooks / 权限扩展 | `src/services/tools/toolHooks.ts` |

报告结论建立在这些核心路径上。

---

## 2. Claude Code 类 Agent Harness 的核心结构

从源码结构看，它不是“LLM + 一组函数”这么简单，而是一套围绕 LLM 工具调用建立的治理框架。

可以抽象成：

```text
Model Stream
  ↓
Tool Use Detection
  ↓
Tool Registry Lookup
  ↓
Input Schema Parse
  ↓
Tool-specific Validation
  ↓
PreToolUse Hooks
  ↓
Permission Decision
  ↓
Tool Call / Progress
  ↓
Tool Result Mapping
  ↓
PostToolUse Hooks
  ↓
Tool Result 回写给模型
  ↓
下一轮 Model Stream
```

这套结构最重要的不是“工具很多”，而是：

1. 工具有明确的 schema；
2. 工具有只读/写入/破坏性/并发安全等属性；
3. 工具有权限检查；
4. 工具执行结果会重新进入 LLM 上下文；
5. 工具执行可以在模型输出流中提前开始；
6. Agent 不是一次性回答，而是 `plan -> act -> observe -> repair/continue` 循环。

这正是 VirtualPartner 当前欠缺的部分。当前 VirtualPartner 已经有 StagePlan 2.0 执行链路，角色能被结构化 JSON 驱动；但它仍是“一次性 LLM 生成完整 StagePlan”的模式。第三阶段应当补的是 Agent Harness，而不是单纯增加更多 prompt 示例。

---

## 3. Tool 不是函数，而是治理单元

### 3.1 Claude Code Tool 的结构特征

`src/Tool.ts` 中的 `Tool` 并不是一个简单函数。它包含大量治理字段，核心包括：

```text
name / aliases / searchHint
call(...)
description / prompt
inputSchema / outputSchema
validateInput
checkPermissions
isReadOnly
isConcurrencySafe
isDestructive
interruptBehavior
isOpenWorld
requiresUserInteraction
shouldDefer / alwaysLoad
renderToolUseMessage / renderToolUseProgressMessage / renderToolResultMessage
mapToolResultToToolResultBlockParam
```

这说明在 Agent Harness 中，工具承担的是 **模型与外部世界之间的受控接口**。工具既要告诉模型“我能做什么”，又要告诉系统“我是否安全、能否并发、是否会改变状态、是否能被中断”。

这对 VirtualPartner 非常关键。

如果我们只做：

```csharp
SetBonePose(args)
```

那它只是函数。

真正应该做的是：

```text
Tool: commit_stage_segment
- name: commit_stage_segment
- description: 追加一段角色演出片段到当前 AgentRun 队列
- inputSchema: StagePlanSegment schema
- validateInput: StagePlan 语法校验、动作字段校验、骨骼范围校验
- checkPermissions: 当前是否允许追加、是否存在 active run、是否被用户打断
- isReadOnly: false
- isConcurrencySafe: false
- isDestructive: false，但会改变 Runtime 状态
- interruptBehavior: cancel 或 block，取决于播放阶段
- call: 提交到 StagePlanSegmentQueue
- result: 返回 segmentId、队列剩余时长、播放状态、warning
- render/debug: 显示在 Runtime Debug 的 Agent 页
```

这才是“角色身体工具”。

### 3.2 对 VirtualPartner 的启发

VirtualPartner 的工具不应该是固定动作：

```text
play_dance()
play_greet()
play_wave()
```

这会退化成有限状态机。

工具应该是角色控制能力：

```text
query_runtime_state()
query_action_capabilities()
query_bone_capabilities(...)
validate_stage_segment(...)
commit_stage_segment(...)
observe_runtime(...)
finish_agent_run(...)
cancel_agent_run(...)
```

LLM 仍然负责填写：

```text
speech 文本
expression 名称
bonePose 骨骼参数
facing 目标
locomotion 时长
stage 结构
下一步是否继续
```

这与编程 Agent 的关系一致：

```text
edit_file 工具不会替 LLM 写代码；代码内容由 LLM 填。
commit_stage_segment 工具也不替 LLM 编动作；动作参数由 LLM 填。
```

---

## 4. ToolUseContext：工具执行需要丰富上下文

Claude Code 的 `ToolUseContext` 很大，里面包含：

```text
options / tools / model / mcpClients / resources
abortController
getToolPermissionContext
readFileState
setToolPermissionContext
getAppState / setAppState
addNotification / appendSystemMessage
onChangeAPIKey
messages
agentId / agentType
contextReplacementState
loadedSkills
queryTrackingContext
requestPrompt
```

这说明工具执行不是孤立的。每个工具都要知道当前会话、上下文、权限、取消信号、状态更新、消息历史等。

VirtualPartner 对应需要一个 `CharacterToolUseContext`：

```text
CharacterToolUseContext
- characterId
- CharacterRuntimeContext
- current AgentRunId
- StagePlanSegmentQueue
- StagePlanValidator
- MotionCritic
- RuntimeObservationProvider
- MotionHistory
- Memory / ChatHistory 摘要
- CancellationToken
- DebugEventSink
- CurrentToolPermissionContext
- AvailableActionCapabilities
- UserInterruptState
```

这可以避免工具成为全局静态函数，也能支持未来多角色预留。

---

## 5. Tool Execution Pipeline：从模型调用到结果回写

`src/services/tools/toolExecution.ts` 展示了一个完整工具执行管线。

核心步骤可以抽象为：

```text
1. 根据 tool_use.name 查找工具
2. 用 inputSchema 解析输入
3. 调用 tool.validateInput
4. 运行 PreToolUse hooks
5. 进行 permission decision
6. 调用 tool.call
7. 接收 progress
8. map result 为 tool_result block
9. 处理过大结果
10. 运行 PostToolUse hooks
11. 返回 tool_result 给 LLM
```

这点特别适合迁移到 VirtualPartner。

### 5.1 VirtualPartner 的 `commit_stage_segment` 执行管线

建议设计为：

```text
LLM tool_use: commit_stage_segment
  ↓
Schema parse：是否是合法 StagePlanSegment 结构
  ↓
StagePlanValidator：StagePlan 2.0 action 合法性
  ↓
BoneCapabilityValidator：骨骼名、side、轴、范围合法性
  ↓
MotionCritic：是否太简单、是否过度复制示例、是否缺少收尾
  ↓
PreCommit hooks：当前 run 是否可追加、是否被打断、队列是否过长
  ↓
Permission / Runtime gate：FSM 是否被用户交互打断、角色是否可控
  ↓
Queue enqueue：加入 StagePlanSegmentQueue
  ↓
Runtime scheduling：如果队列空则立即启动
  ↓
Tool result：返回 segmentId、estimatedDuration、queueRemainingSeconds、warnings
  ↓
PostCommit hooks：记录 motion history、Debug event、必要的模型提示
```

这样 LLM 有动作控制权，但 Runtime 有治理权。

### 5.2 错误不应该只是失败，而应该成为 Agent 的修正输入

编程 Agent 的工具失败后，会把错误结果返回给模型，让模型继续修复。VirtualPartner 也应该这样。

例如：

```json
{
  "ok": false,
  "errorType": "bone_out_of_range",
  "message": "UpperArm.R.y=118 exceeds allowed range -90..90.",
  "repairHint": "Reduce UpperArm.R.y to 60..85 for raised arm gestures."
}
```

LLM 可以据此重新提交 segment，而不是让用户只看到失败。

---

## 6. StreamingToolExecutor：边输出边执行工具

`src/services/tools/StreamingToolExecutor.ts` 是最贴近用户预期的部分。它的特点包括：

1. 模型输出过程中检测到 tool_use 后即可开始执行；
2. 工具状态包括 queued / executing / completed / yielded；
3. 并发安全工具可以并行；非并发安全工具必须独占；
4. progress 可以提前 yield；
5. 中断时只取消允许 cancel 的工具；
6. 结果仍按工具调用顺序回写，避免上下文错乱。

这正是 VirtualPartner 想做的“阶段性输出 + 阶段性工具调用”。

### 6.1 映射到 VirtualPartner

建议实现：

```text
StreamingStageToolExecutor
```

它负责：

```text
- 监听 LLM streaming tool_use
- 一旦收到 commit_stage_segment，立即校验并提交 Runtime 队列
- 读类工具允许并发执行
- 写类工具串行执行
- 返回 tool_result 给 Agent
- 将执行进度写入 Runtime Debug
- 用户新输入时触发 cancel / interrupt
```

工具并发分类：

```text
可并发工具：
- query_runtime_state
- query_action_capabilities
- query_bone_capabilities
- query_recent_motion_history
- validate_stage_segment

必须串行工具：
- commit_stage_segment
- cancel_agent_run
- finish_agent_run
```

这条规则非常重要。因为角色动作队列和骨骼控制权是有状态资源，不能让多个写工具同时乱改。

---

## 7. Query Loop：Agent 的本体不是一次回答，而是循环

`src/query.ts` 展示的主循环大致是：

```text
callModel stream
  ↓
遇到 assistant tool_use
  ↓
StreamingToolExecutor.addTool
  ↓
继续接收模型输出
  ↓
持续收集已完成 tool_result
  ↓
模型流结束后 drain 剩余工具结果
  ↓
如果有 tool_use，则把 assistant 消息 + tool_result 加入 messages
  ↓
再次调用模型
  ↓
直到没有 tool_use 或达到停止条件
```

这说明 Agent 不是一次性完成任务，而是在“工具结果回写”之后继续下一轮推理。

### 7.1 VirtualPartner 的 Agent Loop

建议抽象为：

```text
Start AgentRun(userMessage)
  ↓
Model turn 1
  ↓
可能立即调用 commit_stage_segment：角色立刻回应/思考
  ↓
Tool result: queued / playing
  ↓
Model turn 2
  ↓
query_bone_capabilities / query_runtime_state
  ↓
Tool result: capability / runtime status
  ↓
Model turn 3
  ↓
commit_stage_segment：提交第一段具体动作
  ↓
observe_runtime
  ↓
Model turn 4
  ↓
继续或收尾
```

这和用户期望的 Claude Code / Codex / Cursor 式体验一致：

```text
用户提出目标
-> Agent 规划
-> Agent 调工具
-> 工具结果进入上下文
-> Agent 继续决策
```

区别只是任务对象从“代码库”变成了“角色身体”。

---

## 8. Tool Registry 与 Tool Pool：不是所有能力都应一次性暴露

`src/tools.ts` 中有集中工具注册、工具池装配、权限过滤、MCP 工具合并、稳定排序和 ToolSearch 相关逻辑。

关键启发：

```text
模型每轮看到的工具集是被装配出来的，不是所有工具永远全量暴露。
```

这对 VirtualPartner 当前 prompt 过长非常重要。

### 8.1 当前 VirtualPartner 的问题

当前 Prompt 中一次性注入了：

- StagePlan 全部规则；
- bonePose 字段规则；
- 参数骨骼轴向规则；
- 验证过的动作示例；
- 预设动画表；
- locomotion 规则；
- 长期记忆；
- Runtime Generated Capabilities 全量骨骼、轴向、范围、effects。

这会导致两个问题：

1. 上下文噪声过大；
2. 验证示例容易成为 LLM 的动作锚点。

例如“抬手示例”原本只是帮助 LLM 理解范围，但在一次性 StagePlan 生成模式下，它会变成最安全、最容易复制的默认答案。

### 8.2 建议：能力懒加载

参考 ToolSearch / deferred tool 机制，VirtualPartner 可以改成：

```text
初始只暴露核心工具：
- query_runtime_state
- query_action_capabilities
- query_bone_capabilities
- commit_stage_segment
- observe_runtime
- finish_agent_run

当 LLM 需要控制手臂：
-> query_bone_capabilities(["UpperArm", "Forearm", "Hand"])

当 LLM 需要跳舞：
-> query_bone_capabilities(["Chest", "Head", "UpperArm", "Forearm", "Thigh", "Foot"])

当 LLM 需要移动：
-> query_locomotion_capabilities()
```

这样可以保留 LLM 对骨骼参数的直接控制，又避免全量 prompt 把它压成“示例复制机”。

---

## 9. ToolSearch：延迟工具发现与局部能力查询

`ToolSearchTool` 的模式是：

```text
模型不知道或暂时看不到所有工具
-> 调用 ToolSearch 查询相关工具
-> 系统返回相关工具描述
-> 模型再调用具体工具
```

VirtualPartner 中可以不直接照搬 `ToolSearch` 名称，但思想非常重要。

建议将“动作能力搜索”作为 Agent 的核心工具之一：

```text
query_action_capabilities(intent)
query_bone_capabilities(bones, visualIntent)
query_expression_capabilities()
query_locomotion_capabilities()
query_preset_animation_capabilities(intent)
```

例如：

```text
用户：跳个舞
LLM：我需要上半身摆动、头部配合、可能少量腿部动作。
-> query_bone_capabilities(["Chest", "Head", "UpperArm", "Forearm", "Hand", "Thigh", "Foot"], visualIntent="latin-like small dance")
```

工具返回局部骨骼轴向、范围和注意事项。LLM 再用这些信息编写具体 bonePose。

---

## 10. Plan Mode / Todo：复杂任务先规划，但不必打断沉浸

Claude Code 中存在 Plan Mode、ExitPlanMode、TodoWrite 等工具。它们说明复杂任务不是直接写代码，而是先建立计划、必要时等待确认，再进入执行。

VirtualPartner 不应完全照搬“用户确认计划”的编程体验。因为虚拟角色陪伴要求沉浸感，用户说“跳个舞”时，角色如果说“请批准我的舞蹈计划”会很怪。

但它可以借鉴内部结构：

```text
简单请求：直接 speech + 小动作
复杂表演：进入 Performance AgentRun
长动作：建立 internal performance steps
执行中：逐段提交，逐段观察
结束时：finish_agent_run
```

### 10.1 内部计划，不必全部展示给用户

可以让 LLM 内部决定：

```text
- 是否先思考
- 是否先回应
- 要跳什么风格
- 要不要中途说话
- 要分几段
- 什么时候收尾
```

但用户只看到角色行为，不看到完整内部计划。Debug 面板可以显示 AgentRun plan / tool calls / segment queue，便于开发验收。

---

## 11. 子 Agent 与上下文隔离

`AgentTool/runAgent.ts` 展示了子 Agent 的机制：

- 每个子 Agent 有单独 agentId；
- 可以限制 allowedTools；
- 可以有独立 transcript；
- 可以异步/后台运行；
- 可以拥有独立 context；
- cleanup/finally 负责清理资源；
- 中断或工具结果不完整时会过滤不完整 tool calls。

VirtualPartner V0 不建议一开始做多子 Agent。否则复杂度会上升很快。

但它给出两个重要原则：

1. **AgentRun 必须有生命周期和清理逻辑。**  
   用户中途新输入时，当前 AgentRun 要能 cancel，队列要能清理，骨骼控制要能释放回 Idle。

2. **Agent 工具权限应按 run 限制。**  
   普通聊天 Agent 不应该能调用长演出工具；调试 Agent 可以调用更详细的骨骼查询；正式角色 Agent 只能调用白名单工具。

未来如果需要，可以引入轻量子 Agent：

```text
DialoguePlannerAgent
MotionPlannerAgent
MotionCriticAgent
```

但 V0 建议先单 Agent loop。

---

## 12. Hooks：把治理能力挂到工具前后

Claude Code 的 hooks 系统支持：

```text
PreToolUse
PostToolUse
PostToolUseFailure
```

并且 hooks 可以影响权限、追加上下文、阻止继续、修改输入。

VirtualPartner 可以不做完整插件系统，但应该实现内部 hook 概念。

### 12.1 建议的内部 Hooks

```text
PreCommitStageSegment
- 检查 StagePlanSegment schema
- 检查每个 action 是否支持
- 检查 bonePose 骨骼名、side、轴、范围
- 检查是否和当前 Runtime 状态冲突
- 检查队列长度是否过长

PostCommitStageSegment
- 记录 segmentId
- 更新 motion history
- 更新 Debug 面板
- 返回 queueRemainingSeconds

PostToolFailure
- 把失败原因和修复建议返回给 LLM

PreFinishAgentRun
- 检查是否已有收尾动作
- 检查是否需要反问用户
```

这可以让 Agent 更像编程 Agent：失败不是终点，而是下一轮修复输入。

---

## 13. 从编程 Agent 到角色 Agent 的核心映射

| 编程 Agent | VirtualPartner 角色 Agent |
|---|---|
| 代码库 | 角色身体 / 场景 / Momotalk / Runtime |
| read_file | query_bone_capabilities / query_runtime_state |
| grep/search | query_action_capabilities / query_recent_motion_history |
| edit_file | commit_stage_segment |
| bash/run_tests | observe_runtime / validate_stage_segment / playback result |
| todo list | internal performance steps |
| plan mode | performance planning mode |
| permission check | StagePlanValidator + Runtime State Gate |
| test failure | validation error / playback warning / motion too simple |
| diff | segment summary / motion history delta |
| context compaction | chat summary + motion history summary |
| tool_result | RuntimeObservation |
| max turns | AgentRun max steps / max duration |
| interrupt | cancel_agent_run / release to Idle |

最关键的映射是：

```text
编程 Agent 编写代码；VirtualPartner Agent 编写动作参数。
```

因此，VirtualPartner 不应把“跳舞”封装成固定工具，而应把“提交一段动作参数”封装成工具。

---

## 14. VirtualPartner 推荐架构：Streaming Direct BonePose Agent

建议第三阶段目标架构如下：

```text
MomotalkConversationController
        |
        v
VirtualPartnerAgentRunManager
        |
        v
VirtualPartnerAgentLoop
        |
        +--> CharacterToolRegistry
        |       - query_runtime_state
        |       - query_action_capabilities
        |       - query_bone_capabilities
        |       - query_recent_motion_history
        |       - validate_stage_segment
        |       - commit_stage_segment
        |       - observe_runtime
        |       - finish_agent_run
        |       - cancel_agent_run
        |
        +--> StreamingStageToolExecutor
        |       - read-only tools concurrent
        |       - write tools serial
        |       - cancellation / interruption
        |
        +--> StagePlanSegmentQueue
        |       - append segments
        |       - track current/queued/finished
        |       - fallback thinking segment
        |
        +--> StagePlanValidator
        |
        +--> MotionCritic
        |
        +--> RuntimeObservationProvider
        |
        v
StagePlanPlayer / ActionCoordinator / TTS / Expression / Locomotion
```

### 14.1 不替代 StagePlan 2.0

StagePlan 2.0 仍然是 Runtime 执行格式。

新增的是上游协议：

```text
AgentRun
  -> 多次 StagePlanSegment
  -> 每个 Segment 内部仍是 stages/actions
  -> Runtime 继续使用现有 StagePlanPlayer 和 ActionCoordinator
```

这符合当前项目边界：LLM 不直接写 Unity Transform，不接触真实骨骼路径，而是输出 Runtime 支持的结构化动作。

---

## 15. 建议工具设计

### 15.1 `query_runtime_state`

用途：查询当前角色和 Runtime 状态。

属性：

```text
readOnly: true
concurrencySafe: true
interruptBehavior: cancel
```

返回：

```json
{
  "characterId": "toki",
  "isSpeaking": true,
  "currentAgentRunId": "run_001",
  "queueRemainingSeconds": 1.2,
  "currentExpression": "thinking",
  "currentFacing": "camera",
  "canAppendSegment": true,
  "ttsState": "playing"
}
```

### 15.2 `query_action_capabilities`

用途：返回当前可用 action 类型和简要规则。

属性：只读、并发安全。

返回不应全量塞入骨骼细节，只返回：

```text
speech / expression / bonePose / animation / facing / locomotion
每类 action 的简明能力和约束
```

### 15.3 `query_bone_capabilities`

用途：按需查询局部骨骼能力。

输入：

```json
{
  "bones": ["UpperArm", "Forearm", "Hand", "Chest", "Head"],
  "visualIntent": "small friendly wave"
}
```

返回：

```text
- 可用 side
- 可用 axis
- range
- primary/effects
- 与 visualIntent 相关的简化提示
- 必要的验证示例，但强调不可照抄
```

### 15.4 `validate_stage_segment`

用途：预检一段 StagePlanSegment。

检查内容：

```text
- schema 合法
- action 字段合法
- stage 内 speech 数量合法
- bonePose 骨骼/side/axis/range 合法
- 是否包含未知字段
- 是否与当前 Runtime 状态冲突
- 是否过度接近示例
- 是否过于单薄
```

返回：

```json
{
  "valid": true,
  "warnings": [
    "Gesture has only one static bonePose stage; consider adding a follow-up pose if this should look like a wave."
  ]
}
```

### 15.5 `commit_stage_segment`

用途：追加并执行一段演出，是核心写工具。

属性：

```text
readOnly: false
concurrencySafe: false
interruptBehavior: cancel 或 block，视阶段而定
```

输入：

```json
{
  "runId": "run_001",
  "appendMode": "append",
  "segment": {
    "metadata": { "intent": "dance_thinking", "mood": "curious" },
    "stages": [
      { "actions": [ ... ] }
    ]
  }
}
```

返回：

```json
{
  "ok": true,
  "segmentId": "seg_003",
  "queued": true,
  "estimatedDuration": 1.8,
  "queueRemainingSeconds": 3.1,
  "warnings": []
}
```

### 15.6 `observe_runtime`

用途：观察当前演出状态。

返回：

```json
{
  "runId": "run_001",
  "currentSegmentId": "seg_003",
  "currentStageIndex": 1,
  "queueRemainingSeconds": 1.0,
  "lastSegmentResult": "completed",
  "warnings": [],
  "canContinue": true
}
```

### 15.7 `finish_agent_run`

用途：声明本次角色行为结束。

可做检查：

```text
- 是否已有收尾动作
- 是否需要释放骨骼
- 是否需要进入等待反馈状态
- 是否触发 MemoryJudge
```

---

## 16. AgentRun 生命周期建议

一次用户输入对应一个 AgentRun：

```text
Created
-> Started
-> FirstReactionCommitted
-> PlanningWhilePlaying
-> SegmentCommitted
-> Observing
-> Continuing
-> Closing
-> Finished
```

异常分支：

```text
UserInterrupt
-> CancelRequested
-> QueueCleared
-> RuntimeReleased
-> Cancelled
```

### 16.1 “跳个舞”的理想执行形态

不是固定流程，而是一种可能的 Agent 决策：

```text
用户：跳个舞

AgentRun start
  ↓
commit_stage_segment：Toki 立刻看向用户，说“没问题呀，我先想想跳什么”并进入 thinking 表情/动作
  ↓
query_bone_capabilities：查询跳舞需要的局部骨骼能力
  ↓
commit_stage_segment：想到后的表情变化，Toki 变兴奋并说明舞蹈风格
  ↓
commit_stage_segment：第一段舞蹈 bonePose
  ↓
observe_runtime：队列剩余时间 / 是否播放成功
  ↓
commit_stage_segment：第二段舞蹈 bonePose
  ↓
commit_stage_segment：谢幕 + 反问用户
  ↓
finish_agent_run
```

但下一次用户说“酷一点”“害羞一点”“别想了直接跳”，LLM 可以选择完全不同流程。固定的是工具协议，不是剧情模板。

---

## 17. 为什么这能解决当前“动作和回复太简单”

当前一次性 StagePlan 模式有几个天然倾向：

```text
- 为保证 JSON 合法，输出短而保守
- 为避免超出 stage 规则，减少动作数量
- 被验证示例锚定，复制最安全的参数
- speech 被规则压缩成一句话
- 没有执行中观察，也没有继续补动作的机会
```

Streaming Direct BonePose Agent 可以改变这些条件：

1. **即时反应与复杂动作拆开。**  
   角色可以先说话/思考，复杂动作在后台继续生成。

2. **能力按需查询。**  
   不再把全量骨骼规则塞进初始 prompt，降低噪声和示例锚定。

3. **多段提交。**  
   跳舞不再必须在一个 JSON 里一次性写完，而是可以边执行边追加。

4. **工具结果回写。**  
   Runtime 结果成为下一轮 LLM 决策输入。

5. **MotionCritic 介入。**  
   系统可以提醒“这个动作太像静态举手”“缺少收尾”“和上一次太像”。

6. **LLM 仍直接写骨骼参数。**  
   角色差异化来自 LLM 对每个 segment 的参数填写，而不是预设动作库。

---

## 18. 不建议照搬的部分

### 18.1 不建议复制编程 Agent 的用户确认体验

编程 Agent 改文件前确认是合理的。虚拟角色每段动作都确认会破坏沉浸。

应改为：

```text
内部计划 + Debug 可见 + Runtime 安全边界
```

### 18.2 不建议一开始做多子 Agent

Claude Code 的 Task / AgentTool 很强，但 VirtualPartner V0 如果一开始做 DialoguePlannerAgent、MotionPlannerAgent、CriticAgent、PerformanceAgent 多层分工，会过早复杂化。

建议先做单 Agent Loop，后续再拆。

### 18.3 不建议做 frame-level streaming

不要让 LLM 每 0.1 秒生成骨骼。

应该做：

```text
stage-level streaming
```

每次提交 1～3 秒的演出片段，队列剩余时间不足时再追加。

### 18.4 不建议把动作封装成 play_xxx 工具

`play_dance()`、`play_wave()` 会退化成状态机。应封装底层能力工具，让 LLM 填参数。

---

## 19. VirtualPartner 第三阶段建议边界

### 19.1 阶段目标

将当前一次性 LLM StagePlan 生成升级为 Streaming Direct BonePose Agent。Agent 在一次用户请求中可多轮调用角色工具，阶段性查询能力、阶段性生成 speech / expression / bonePose / facing / locomotion 参数，并通过 StagePlanSegmentQueue 逐步提交执行。LLM 保留对 bonePose 参数的直接生成权；Runtime 负责工具治理、校验、排队、执行、观察和中断。

### 19.2 第一版最小实现范围

建议 V0 只做：

```text
1. AgentRunManager
2. CharacterToolRegistry
3. StreamingStageToolExecutor 或简化版工具循环
4. StagePlanSegmentQueue
5. query_runtime_state
6. query_action_capabilities
7. query_bone_capabilities
8. validate_stage_segment
9. commit_stage_segment
10. observe_runtime
11. finish_agent_run / cancel_agent_run
12. Runtime Debug Agent 页
```

暂缓：

```text
- 多子 Agent
- 外部 MCP Server
- 复杂权限 UI
- 持久化 Agent 任务系统
- 自动学习动作库
- frame-level 实时控制
```

### 19.3 验收标准

建议用三类请求验收：

#### A. 普通聊天

用户：

```text
你好
```

期望：

```text
- 可以直接或小段 AgentRun 完成
- speech 更自然
- 可附带轻微表情/头部动作
- 不必过度演出
```

#### B. 简单动作

用户：

```text
打个招呼
```

期望：

```text
- 角色先面向用户
- 有自然 speech
- 有至少两段动作变化，而不是静态举手
- 可以有头/胸/手腕配合
- 动作参数由 LLM 直接生成
```

#### C. 表演请求

用户：

```text
跳个舞
```

期望：

```text
- 角色立即有可见反应
- Agent 在执行中查询能力/提交后续片段
- 舞蹈由多个 StagePlanSegment 组成
- 至少一次 observe_runtime 或 equivalent runtime feedback
- 有收尾动作/台词
- 能反问用户感受
```

---

## 20. 与当前 VirtualPartner 架构的兼容性

当前项目已经完成：

```text
Momotalk 文本/语音入口
StagePlan 2.0
StagePlanValidator
StagePlanPlayer
speech / expression / bonePose / animation / facing / locomotion
TTS / ASR / mouth / Memory / Debug
ActionCoordinator / Root / Locomotion
```

因此，第三阶段不需要重写 Runtime。建议保留以下边界：

```text
- StagePlan 2.0 仍是 Runtime 执行格式
- LLM 仍不直接写 Unity Transform
- 所有骨骼控制仍走 ActionCoordinator
- Momotalk 不直接控制角色动作
- TTS/ASR 仍是服务层，不嵌入 Agent
- Runtime Debug 继续作为观察入口
```

新增的是上游 Agent Harness：

```text
一次性 LlmRelay
    ↓
Streaming AgentRun + Tool Loop + StagePlanSegmentQueue
```

---

## 21. 推荐开发顺序

### Step 1：定义 AgentRun 与 StagePlanSegment

先不接 LLM，做本地模拟：

```text
AgentRunManager.StartRun
CommitSegment
Observe
Finish
Cancel
```

确保队列、打断、释放逻辑稳定。

### Step 2：工具接口本地化

实现工具 registry 与 executor，但可先不支持真正 streaming：

```text
query_runtime_state
query_bone_capabilities
commit_stage_segment
observe_runtime
```

### Step 3：LLM 多轮工具调用

接入 tool-call loop：

```text
LLM -> tool_use -> tool_result -> LLM follow-up
```

先支持非流式多轮，再升级为 streaming tool execution。

### Step 4：Streaming commit

让 `commit_stage_segment` 在模型输出时即可执行，角色不等完整回复。

### Step 5：MotionCritic

加入动作复杂度/差异化评价：

```text
- 是否单 pose
- 是否过度复制示例
- 是否缺少头/胸/手细节
- 是否和最近动作重复
- 是否缺少收尾
```

### Step 6：Debug 接入

新增 Runtime Debug 页签：

```text
Agent
- current run
- tool calls
- segment queue
- current segment/stage
- observations
- warnings/errors
- cancellation state
```

---

## 22. 总结

Claude Code 类编程 Agent 的核心不是“会调用工具”，而是：

```text
LLM + 工具协议 + 工具治理 + 上下文反馈 + 多步循环 + 权限边界
```

VirtualPartner 的第三阶段应该吸收这套结构，但把任务对象从代码库换成角色身体：

```text
代码任务执行者
-> 角色行为执行者
```

对应地，VirtualPartner 不应该只是“LLM 生成动作 JSON”，而应该升级为：

```text
LLM 规划角色行为
-> 查询身体能力
-> 直接编写 bonePose 参数
-> 提交阶段性演出片段
-> 观察 Runtime 结果
-> 继续编排或收尾
```

这条路线可以同时解决当前两个核心体验问题：

1. **动作太简单**：因为 Agent 不再一次性写短 JSON，而是可逐段生成、观察、继续、修正；
2. **回复太简单**：因为 speech 可以作为演出流程的一部分分阶段出现，而不是压缩成一句确认。

更重要的是，它不会把项目退化成预设动作库。LLM 仍然是角色大脑，仍然直接填写动作参数。Runtime 只是给它提供一个安全、可观察、可中断、可持续执行的身体工具接口。

这正是 VirtualPartner 从“路径走通”升级到“角色行为 Agent”的关键一步。
