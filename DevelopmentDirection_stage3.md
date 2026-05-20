# VirtualPartner 全局开发方向文档

更新时间：2026-05-19  
当前阶段：由一次性 LLM StagePlan 生成，转向 Streaming Direct BonePose Agent Harness

本文档记录 VirtualPartner 当前节点之后的主开发方向、架构边界和后续阶段开发时必须遵守的系统原则。它不是详细实现计划；具体模块拆分、接口字段、验收标准和开发步骤，应在正式进入阶段开发时通过 plan 模式单独定稿。

开发协作规则仍以 `ReadFirst.md` 为准。具体进展状态见 `DevelopmentTODO.md`。

---

## 1. 当前定位

VirtualPartner 当前已经完成第一阶段 Runtime 原型、第二阶段正式交互体验和 Standalone App Shell MVP。

当前主角色为 Toki / CH0187。当前系统已经具备：

- Momotalk 文本聊天入口。
- ASR 文本输入链路。
- TTS 角色语音输出链路。
- StagePlan 2.0 结构化动作执行链路。
- speech / expression / bonePose / animation / facing / locomotion 执行能力。
- 嘴型、基础表情、长期记忆、Runtime Debug。
- Windows Standalone 最小应用菜单与退出能力。

当前主体验链路为：

```text
用户文本或 ASR 文本
-> MomotalkConversationController
-> LlmRelay
-> LLM 一次性返回完整 StagePlan 2.0 JSON
-> StagePlanValidator 校验
-> StagePlanPlayer 按 stage 执行
-> speech / expression / bonePose / animation / facing / locomotion
-> TTS / mouth / expression / ActionCoordinator / Root / Locomotion
-> MemoryJudge 在有效对话完成后判断是否写入长期记忆
```

当前问题不在于 StagePlan 2.0 格式无法执行，而在于：

```text
LLM 仍然以“一次性生成完整 StagePlan”的方式工作。
```

这会导致：

- 角色反应缺少阶段性过程。
- 动作和语言容易被压缩到最短合法结果。
- LLM 容易模仿 prompt 示例，而不是主动设计动作。
- 用户看不到类似 Claude Code / Codex / Cursor 那种“边行动、边观察、边继续”的 Agent 行为。
- 复杂表演类请求无法自然表现出思考、选择、执行、观察、收尾的过程。

因此，下一阶段主线不是推翻 StagePlan，而是将上游 LLM 调用方式升级为 Agent Harness。

---

## 2. 第三阶段主方向：Streaming Direct BonePose Agent Harness

第三阶段目标定义为：

> 将当前一次性 LLM StagePlan 生成升级为 Streaming Direct BonePose Agent Harness。Agent 不再一次性返回完整演出，而是在一次 AgentRun 中根据用户输入、上下文、Runtime 状态和工具返回结果，阶段性查询角色能力、阶段性生成 speech / expression / bonePose / facing / locomotion 等动作参数，并通过 StagePlanSegment 队列逐步提交执行。LLM 保留对参数骨骼动作的直接生成权，Runtime 只负责工具返回、合法性校验、队列执行、观察反馈和安全中断。

核心类比：

```text
Claude Code / Codex / Cursor:
用户目标
-> Agent 规划
-> 读文件 / 查结构 / 改代码 / 跑测试
-> 观察结果
-> 继续修改
-> 完成任务

VirtualPartner:
用户目标
-> Agent 规划角色行为
-> 查询角色能力 / 查询骨骼轴向 / 编写动作参数 / 提交演出片段
-> 观察 Runtime 执行结果
-> 继续追加动作
-> 收尾并等待用户反馈
```

本阶段的核心不是“动作工具化”本身，而是：

```text
角色行为 Agent 化。
```

也就是说，LLM 不只是输出 JSON，而是作为角色大脑，持续调用工具治理角色行为。

---

## 3. 核心原则

### 3.1 StagePlan 2.0 继续作为 Runtime 执行 IR

StagePlan 2.0 不废弃、不替换。

它仍然是 Runtime 最终执行格式，继续遵守：

- `stages` 按数组顺序执行。
- stage 内 action 同时启动。
- 当前 stage 的 blocking action 完成后进入下一 stage。
- 每个 stage 最多一个 speech。
- Runtime 按数组顺序生成 stageIndex。
- 禁止 `timeline`、`start`、`end`、`stageId` 等旧字段。

第三阶段新增的是 StagePlan 上游的 Agent Harness，而不是新的 Runtime 主格式。

### 3.2 LLM 保留 bonePose 参数生成权

本项目的核心特色是 LLM 直接控制参数动作。

后续不采用“LLM 只选择预设动作、本地算法生成骨骼参数”的主路径。

允许存在预设动画、动作示例、动作辅助规则，但常规参数动作仍应遵循：

```text
LLM 规划动作
-> LLM 查询相关骨骼能力
-> LLM 自己填写 bonePose 骨骼旋转参数
-> Runtime 校验并执行
```

本阶段不将角色动作退化为：

```text
用户意图 -> 固定动作模板 -> 播放
```

也不将动作主控制权转移给纯算法 MotionComposer。

### 3.3 工具是治理边界，不是固定动作库

工具不应设计为：

```text
play_dance()
play_greet()
play_wave()
```

这会把角色行为变成有限状态机。

工具应设计为角色身体和 Runtime 的受控接口：

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

LLM 通过工具查询信息、提交动作、观察结果，但动作内容和参数仍由 LLM 决定。

### 3.4 固定执行规则，不固定演出流程

Runtime 需要固定执行规则：

- segment 必须通过校验。
- 写入 Runtime 队列的工具必须串行。
- 用户新输入可以打断当前 AgentRun。
- 队列空时需要 fallback。
- 非法动作不能进入执行队列。
- LLM 不能绕过 StagePlanPlayer / ActionCoordinator 直接控制 Unity Transform。

但具体演出流程应交给 LLM 决定。

例如用户说“跳个舞”，Agent 可以选择：

- 立即开始跳。
- 先思考跳什么。
- 先害羞回应。
- 先卖关子。
- 跳一段短舞。
- 分两段跳。
- 最后谢幕。
- 最后反问用户评价。

这些流程内容不应写死为唯一模板。

### 3.5 采用 stage-level streaming，不做 frame-level streaming

本阶段不做逐帧实时 LLM 控制。

推荐采用：

```text
stage-level streaming
```

即 Agent 每次提交 1 到 3 个 stage 的 StagePlanSegment。Runtime 执行当前 segment 时，Agent 可以继续查询、规划、生成并追加后续 segment。

避免以下问题：

- LLM 延迟导致动作卡顿。
- TTS 与动作无法稳定同步。
- 队列过短导致角色频繁停顿。
- 工具调用频率过高导致系统复杂度失控。

---

## 4. 新主链路

第三阶段目标主链路为：

```text
用户文本或 ASR 文本
-> MomotalkConversationController
-> AgentRunManager 创建 AgentRun
-> AgentLoop 调用 LLM
-> LLM 根据上下文选择工具
-> ToolExecutor 执行查询 / 校验 / 提交 / 观察
-> StagePlanSegmentQueue 接收可执行片段
-> StagePlanValidator 校验 segment 内 stages/actions
-> StagePlanPlayer 播放 segment
-> RuntimeObservation 返回执行状态
-> AgentLoop 根据观察结果继续追加 / 修正 / 收尾
-> AgentRun 完成
-> MemoryJudge 在有效对话完成后判断是否写入长期记忆
```

与当前链路的关系：

```text
当前：
LlmRelay -> 完整 StagePlan -> StagePlanPlayer

目标：
AgentRunManager / AgentLoop -> 多个 StagePlanSegment -> StagePlanSegmentQueue -> StagePlanPlayer
```

StagePlanPlayer、ActionCoordinator、TTS、ASR、Expression、Mouth、Root、Locomotion 等底层 Runtime 能力继续复用。

---

## 5. AgentRun 生命周期

AgentRun 表示一次用户请求触发的一轮角色行为任务。

建议生命周期：

```text
Idle
-> Starting
-> Running
-> WaitingForToolResult
-> PlayingQueuedSegments
-> Continuing
-> Finishing
-> Completed
```

异常或中断状态：

```text
CancelledByUser
Failed
TimedOut
FallbackCompleted
```

基本规则：

- 同一角色同一时间只允许一个主交互 AgentRun。
- 新用户输入默认打断当前 AgentRun，除非后续明确支持并行多任务。
- AgentRun 内可以多次提交 StagePlanSegment。
- AgentRun 完成后，角色回到可交互等待状态，FSM 可在空闲期重新接管。
- AgentRun 的关键状态应进入 Runtime Debug。

---

## 6. StagePlanSegmentQueue

StagePlanSegmentQueue 是第三阶段新增的核心 Runtime 上游队列。

职责：

- 接收 Agent 提交的 StagePlanSegment。
- 维护 segment 播放顺序。
- 跟踪当前 segment、当前 stage 和剩余可播放时长。
- 保证写入类工具串行执行。
- 在队列将空时通知 Agent 或触发 fallback。
- 支持取消当前 AgentRun 时清空未播放 segment。
- 向 Runtime Debug 暴露队列状态。

StagePlanSegment 不替代 StagePlan 2.0。它只是 StagePlan 2.0 的分段提交包装。

建议内部结构可以包含：

```json
{
  "runId": "...",
  "segmentId": "...",
  "appendMode": "append",
  "stages": [
    { "actions": [] }
  ]
}
```

`stages/actions` 仍使用 StagePlan 2.0 的 action 规则。

正式字段命名与具体结构留到 plan 模式定稿。

---

## 7. Character Tool Interface

第三阶段应建立角色 Agent 工具接口。工具分为只读工具、校验工具和写入工具。

### 7.1 只读工具

#### query_runtime_state

查询当前角色和 Runtime 状态，例如：

- 当前是否存在 active AgentRun。
- 当前是否正在播放 segment。
- 队列剩余时长。
- 当前 expression / speech / TTS 状态。
- FSM 是否被交互打断。
- Root 朝向、locomotion 状态。

#### query_action_capabilities

查询当前角色支持的 action 类型：

- speech
- expression
- bonePose
- animation
- facing
- locomotion

#### query_bone_capabilities

按需查询局部骨骼能力。

不再默认把全部骨骼轴向、范围和示例一次性塞进 prompt。

例如用户请求打招呼时，只查询：

```text
UpperArm / Forearm / Hand / Chest / Head
```

用户请求走动时，查询：

```text
facing / locomotion / Root 状态
```

用户请求跳舞时，再查询更大范围的上身、头部、下肢和可用 preset animation。

#### query_recent_motion_history

查询最近类似动作摘要，用于避免重复。

示例：

- 最近 greeting 使用了哪只手。
- 最近 dance 是否已经使用过类似节奏。
- 最近是否频繁使用同一组 Head / Chest 倾斜。

#### observe_runtime

查询当前 AgentRun 执行状态：

- 当前 segment 是否播放中。
- 当前 stage 是否完成。
- 队列剩余时长。
- 是否有 warning / error。
- TTS 是否完成或失败。
- locomotion 是否被 MovementConstraint 阻挡。

### 7.2 校验工具

#### validate_stage_segment

校验 StagePlanSegment 是否可以提交执行。

至少包括：

- JSON 字段合法性。
- StagePlan 2.0 action 规则。
- 每 stage 最多一个 speech。
- action 类型白名单。
- expression 白名单。
- animation 白名单。
- bone / side / axis / range 合法性。
- locomotion / facing 参数合法性。

后续可扩展 MotionCritic：

- 动作是否太简单。
- 是否只有单个静态 pose。
- 是否过度接近 verified example。
- 是否缺少起势、主动作、收尾。
- 是否缺少头部、胸部、手腕等辅助动作。
- 是否与最近动作重复。

校验工具可以返回 warning，但不应擅自改写 LLM 的动作参数。动作修正应交回 LLM 完成。

### 7.3 写入工具

#### commit_stage_segment

将一段 StagePlanSegment 追加到当前 AgentRun 队列。

规则：

- 必须先通过 validate_stage_segment 或内部自动校验。
- 写入操作必须串行。
- 如果当前 AgentRun 已取消，提交失败。
- 返回 segmentId、预计播放时长、队列状态和 warning。

#### cancel_agent_run

取消当前 AgentRun。

用于：

- 用户发送新输入。
- 用户手动打断。
- 工具调用或 Runtime 状态异常。

#### finish_agent_run

声明本次 AgentRun 完成。

可触发：

- 角色回到等待状态。
- 允许 FSM 在空闲期重新接管。
- 将有效对话交给 MemoryJudge 判断是否写入长期记忆。

---

## 8. 工具治理原则

参考编程 Agent 的工具治理方式，VirtualPartner 的工具也需要具备治理元信息。

每个工具至少应明确：

```text
name
description
inputSchema
outputSchema
readOnly / write
concurrencySafe
canInterrupt
validateInput
permissionCheck
runtimeEffect
debugRender
```

工具不是普通函数，而是 LLM 与 Runtime 之间的治理边界。

### 8.1 并发规则

可并发：

```text
query_runtime_state
query_action_capabilities
query_bone_capabilities
query_recent_motion_history
observe_runtime
validate_stage_segment
```

必须串行：

```text
commit_stage_segment
cancel_agent_run
finish_agent_run
```

### 8.2 权限规则

LLM 可以：

- 查询角色能力。
- 查询 Runtime 状态。
- 生成 speech 文本。
- 生成 bonePose 参数。
- 提交 StagePlanSegment。
- 根据观察结果继续追加或收尾。

LLM 不可以：

- 直接写 Unity Transform。
- 绕过 StagePlanValidator。
- 绕过 ActionCoordinator。
- 直接操作真实骨骼路径。
- 修改 Runtime Debug 或文件系统配置。
- 直接启动/关闭 TTS、ASR 本地服务。

---

## 9. Prompt 与上下文方向

第三阶段应减少大批量 prompt 注入。

当前 prompt 一次性注入大量规则、骨骼、轴向、示例，容易导致：

- 上下文过长。
- LLM 关注示例而不是动作设计。
- 输出过度保守。
- 示例动作被误当作标准答案。

新的方向是：

```text
全局 prompt 只保留稳定规则和工具使用规范。
局部能力通过工具按需查询。
动作示例作为参考，不作为默认答案。
复杂动作先通过 Agent 规划，再查询相关骨骼能力，再生成 bonePose。
```

示例的定位应从：

```text
照着这个动作写
```

改成：

```text
这个示例说明某个方向和范围是安全的，但不要复制为默认动作。
```

---

## 10. 表演体验方向

第三阶段的体验目标是让角色从“路径走通”升级到“演出编排”。

对于复杂表演类请求，例如：

```text
跳个舞
```

理想行为不是固定流程，但应允许 Agent 自主组织类似以下结构：

```text
即时反应
-> 思考 / 查询 / 准备
-> 情绪变化
-> 宣布选择
-> 分段表演
-> 观察执行状态
-> 继续补后续动作
-> 收尾 / 谢幕
-> 反问用户反馈
```

这些流程不是硬编码模板，而是 Agent 可选择的行为模式。

具体动作仍应由 LLM 根据上下文生成，例如：

- 选择哪只手。
- 手臂抬到什么幅度。
- 是否加入 Head / Chest 配合。
- 是否使用 locomotion。
- 语言是俏皮、害羞、认真还是兴奋。
- 表演持续几段。
- 是否需要收尾反问。

---

## 11. Runtime 核心边界继续保留

### ActionCoordinator

ActionCoordinator 仍是骨骼控制权核心。

任何来自 Agent 的 bonePose 最终仍必须通过现有骨骼控制权链路执行。

不允许新增 Agent 路径绕过 ActionCoordinator 写骨骼。

### AvatarPoseApplier

AvatarPoseApplier 仍是最终 Transform 写入出口。

Agent 不接触真实 Transform。

### Root / Locomotion

Root 朝向和位移仍由 RootOrientationController、LocomotionActionExecutor、MovementConstraintController 负责。

Agent 只能通过 facing / locomotion action 表达意图。

### FSM

用户交互和 AgentRun 优先级高于 FSM。

AgentRun 期间 FSM 不主动插入动作。

AgentRun 完成并进入空闲期后，FSM 可重新接管。

---

## 12. Momotalk 边界

Momotalk 仍是正式交互入口。

Momotalk 负责：

- 用户文本输入。
- ASR 识别结果进入输入框或自动发送。
- 用户气泡、角色气泡、typing 状态。
- 聊天历史。
- 未读提示。

Momotalk 不直接控制骨骼、Root、表情或语音播放。

第三阶段后，Momotalk 不再直接依赖“一次性 LlmRelay 返回完整 StagePlan”，而是把正式用户消息交给 AgentRunManager。

角色气泡应来自 Agent 提交的 speech action，而不是来自未执行的内部规划文本。

内部工具调用、动作规划和调试信息应进入 Runtime Debug，而不是直接显示给普通用户。

---

## 13. Debug 方向

第三阶段新增系统必须接入统一 Runtime Debug。

建议新增 Agent 页或扩展 LLM / StagePlan 页，显示：

- 当前 AgentRun id。
- AgentRun 状态。
- 最近工具调用列表。
- 当前 StagePlanSegmentQueue。
- 当前播放 segment / stage。
- 队列剩余时长。
- validate warning / error。
- RuntimeObservation。
- 最近 motion history 摘要。
- 是否被用户打断。

Debug 面板仍是开发工具，不是正式产品设置 UI。

---

## 14. 本阶段暂不做

第三阶段方向确认阶段暂不做以下内容：

- 不废弃 StagePlan 2.0。
- 不重写 StagePlanPlayer。
- 不重写 ActionCoordinator。
- 不引入 frame-level LLM 控制。
- 不把所有动作改成固定模板。
- 不把参数动作主路径替换为 MotionComposer 纯算法映射。
- 不做多角色同时在场调度。
- 不做角色间主动对话或群聊。
- 不重构 TTS / ASR 发布模式。
- 不把 MCP 作为必须前置条件。

MCP 化可以作为未来方向，但 V0 应优先完成本地 Agent Tool Registry 和 AgentRun 闭环。

---

## 15. 建议 V0 范围

后续进入正式计划时，建议 V0 只做最小闭环：

```text
Momotalk 用户输入
-> AgentRunManager 创建 run
-> LLM 调用本地角色工具
-> query_bone_capabilities 按需返回局部骨骼能力
-> commit_stage_segment 追加 1 到 N 段 StagePlanSegment
-> StagePlanSegmentQueue 顺序播放
-> observe_runtime 返回执行状态
-> Agent 继续追加或 finish
-> Debug 可观察全过程
```

V0 支持请求类型可先限定为：

- 普通聊天。
- greeting 类简单动作。
- dance / performance 类表演动作。

V0 成功标准：

- 角色能在复杂请求下先即时反应，而不是等待完整 JSON。
- Agent 能分多次提交动作片段。
- LLM 仍直接填写 bonePose 参数。
- 工具结果能回写给 Agent 并影响后续动作。
- Runtime Debug 能看见工具调用、队列和执行状态。
- 新用户输入能取消当前 AgentRun。
- 原有一次性 StagePlan 路径可作为 fallback 保留。

---

## 16. 后续进入 plan 模式时需要定稿的问题

正式开发前需要单独讨论并定稿：

1. AgentRunManager 与现有 LlmRelay 的关系。
2. StagePlanSegment 的精确数据结构。
3. ToolRegistry / ToolExecutor 的 C# 接口。
4. 工具调用结果如何注入下一轮 LLM 上下文。
5. commit_stage_segment 是否自动调用 validate_stage_segment。
6. Segment 队列剩余时长如何估算。
7. 队列快空时 fallback 行为。
8. 用户打断时当前 speech / TTS / bonePose 如何停止。
9. MotionCritic V0 是否只做 warning，还是允许阻止提交。
10. Debug 面板展示格式。
11. 普通聊天是否仍允许一次性 StagePlan fallback。
12. Prompt 如何从全量能力注入改成工具查询式能力注入。

---

## 17. 总结

VirtualPartner 当前已经完成“角色能被结构化动作驱动”的阶段。

下一阶段要解决的是：

```text
LLM 能不能像 Agent 一样治理角色行为。
```

因此，新的主方向是：

```text
从一次性 LLM StagePlan 生成
升级为 Streaming Direct BonePose Agent Harness。
```

这个方向保留当前 Runtime 成果，保留 StagePlan 2.0，保留 LLM 对参数骨骼动作的直接生成权，同时引入类似 Claude Code / Codex / Cursor 的 Agent Harness 思路：

```text
plan
-> tool call
-> observe
-> repair / continue
-> staged execution
```

最终目标不是让 Toki 播放更多预设动作，而是让 Toki 能像一个角色 Agent 一样，根据用户输入、上下文和 Runtime 状态，阶段性组织语言、表情、动作和反馈，形成更自然、更有差异化、更接近虚拟陪伴角色的交互体验。
