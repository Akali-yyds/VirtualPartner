# VirtualPartner 第三阶段阶段性开发 TODO

更新时间：2026-05-19

本文档是 VirtualPartner 第三阶段开发的长期跟踪清单，来源于 `DevelopmentDirection_stage3.md`、`Agent编程智能体架构到VirtualPartner角色Agent的映射调研.md` 和本轮阶段初始化讨论。

开发原则以 `ReadFirst.md` 为准：先讨论明确，再开发；简单、高效、直接；高内聚、低耦合；只做当前阶段需要的内容。

本文档只记录阶段级目标、边界、开发任务和验收标准，不提前写死具体类名、字段、接口或路径细节。正式进入每个阶段前，仍需要单独讨论并生成该阶段的实施计划。

## 执行节奏

- [ ] 进入某个阶段前，先讨论并明确该阶段的具体目标、边界和验收方式。
- [ ] 用户明确要求生成 plan 后，再生成可执行计划。
- [ ] plan 确认后再开发，不提前实现后续阶段内容。
- [ ] 开发完成后，通过 Play Mode、Standalone、Inspector、日志、Runtime Debug、必要的自动化测试和 Unity MCP 辅助检查进行验收。
- [ ] 用户确认验收通过后，再勾选对应阶段完成。
- [ ] 第三阶段整体通过后，再将最终状态并入根目录 `DevelopmentTODO.md` 和 `README.md`。

## 第三阶段总目标

第三阶段目标是将当前一次性 LLM 完整 StagePlan 生成链路升级为单一 AgentRun 主链路。

目标主体验链路：

```text
用户文本或 ASR 文本
-> MomotalkConversationController
-> AgentRun
-> Agent 查询能力 / 提交片段 / 观察 Runtime / 继续编排
-> 多个 StagePlanSegment
-> StagePlanValidator / StagePlanPlayer
-> speech / expression / bonePose / animation / facing / locomotion
-> TTS / mouth / expression / ActionCoordinator / Root / Locomotion
-> 正常 finished 的正式 AgentRun 触发 MemoryJudge
```

第三阶段不是推翻 Runtime，而是在现有 Runtime 上游建立角色行为 Agent Harness。

## 第三阶段总原则

- [ ] `StagePlan 2.0` 继续作为 Runtime 执行 IR，不引入新的 Runtime 主格式。
- [ ] `StagePlanSegment` 是 Agent 层提交单位，不替代 `StagePlan 2.0`。
- [ ] Segment 内部仍使用 `StagePlan 2.0` 的 `stages/actions`，最终仍走现有 `StagePlanValidator / StagePlanPlayer`。
- [ ] LLM 仍保留直接生成 `bonePose` 参数的能力，不把角色动作退化为固定动作库。
- [ ] 工具是治理边界，不是 `play_dance()`、`play_wave()` 这类固定动作模板。
- [ ] 读类工具可以并发，写入类工具必须串行。
- [ ] `validate_stage_segment` 和 MotionCritic / QualityCritic 默认只返回错误、警告和建议，不主动改写 `bonePose` 参数。
- [ ] 结构错误、非法 action、非法骨骼或非法参数由 validator 阻止提交。
- [ ] 表现质量问题默认只作为 warning / suggestion 返回给 Agent，由 Agent 下一轮修正。
- [ ] 最终 Momotalk 正式入口统一进入 AgentRun，不保留 one-shot StagePlan 作为正式兜底链路。
- [ ] 旧 `LlmRelay -> 完整 StagePlan` 是待迁出的旧业务链路。
- [ ] 旧 LlmRelay 中底层 HTTP / API 调用能力可以后续下沉为 LLM client，供 AgentLoop 使用。
- [ ] Prompt 瘦身不是减少角色能力，而是将全量骨骼、轴向和示例从初始 prompt 移出，改为 Agent 按需查询。
- [ ] 只有正常 finished 的正式 AgentRun 才触发 MemoryJudge。
- [ ] cancelled / failed / debug mock run 默认不写入长期记忆。
- [ ] Unity MCP 仅作为开发和验收辅助工具，不作为 VirtualPartner 产品架构依赖。

## 本阶段暂不做

- [ ] 不废弃 `StagePlan 2.0`。
- [ ] 不重写 `StagePlanPlayer`、`ActionCoordinator`、TTS、ASR、Mouth、Expression、Root 或 Locomotion。
- [ ] 不做 frame-level LLM 骨骼控制。
- [ ] 不把参数动作主路径替换为纯算法 MotionComposer。
- [ ] 不把角色行为变成固定动作模板库。
- [ ] 不做多角色同时在场调度。
- [ ] 不做角色间主动对话或 Momotalk 群聊。
- [ ] 不把外部 MCP 化作为 V0 前置条件。
- [ ] 不在普通用户 UI 中展示工具日志、内部规划或 Debug 信息。

## 阶段 3.0：第三阶段基线与路线图初始化

**阶段目标**

确认第三阶段目标、边界、开发节奏和阶段级 TODO，为后续 AgentRun 主链路开发建立清晰基线。

**前置条件**

- [ ] 第一阶段 Runtime 原型已完成并通过验收。
- [ ] 第二阶段正式交互体验已完成并通过验收。
- [ ] Standalone App Shell MVP 已完成并通过验收。
- [ ] 已阅读 `ReadFirst.md`。
- [ ] 已阅读 `DevelopmentDirection_stage3.md`。
- [ ] 已阅读 Agent 架构映射调研文档。

**开发任务**

- [ ] 建立第三阶段阶段性开发 TODO 文档。
- [ ] 明确第三阶段最终主链路为 AgentRun。
- [ ] 明确 StagePlanSegment 与 StagePlan 2.0 的关系。
- [ ] 明确旧 one-shot LlmRelay 链路的迁出方向。
- [ ] 明确后续每个阶段开始前都先单独讨论。
- [ ] 明确 TODO 只记录阶段级任务，不提前写死实现细节。

**手动验收标准**

- [ ] 根目录存在 `DevelopmentTODO_Stage3.md`。
- [ ] 文档明确第三阶段总目标和总原则。
- [ ] 文档明确 StagePlanSegment 不是新的 Runtime IR。
- [ ] 文档明确旧 one-shot LlmRelay 不作为最终正式兜底链路。
- [ ] 文档未修改根目录 `DevelopmentTODO.md` 或 `README.md`。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.1：本地模拟 AgentRun 生命周期

**阶段目标**

在不接真实 LLM 的情况下，建立可本地模拟的一次用户请求生命周期，验证 AgentRun 的开始、运行、完成、失败和取消状态。

**前置条件**

- [ ] 阶段 3.0 已完成。
- [ ] 已确认本阶段只做本地模拟，不接真实 LLM tool call。

**开发任务**

- [ ] 建立 AgentRun 的最小生命周期模型。
- [ ] 支持同一角色同一时间只有一个主交互 AgentRun。
- [ ] 支持本地 mock run 的 start / finish / fail / cancel。
- [ ] 支持新用户输入或 Debug 操作取消当前 run。
- [ ] 保持 FSM 在 AgentRun 期间不主动插入动作。
- [ ] 为后续工具调用、Segment 队列和 Debug 观察预留状态入口。

**手动验收标准**

- [ ] Play Mode 中可创建本地 mock AgentRun。
- [ ] 可观察当前 run id、状态和目标角色。
- [ ] run 正常完成后角色回到可交互等待状态。
- [ ] run 被取消后不继续追加后续动作。
- [ ] debug mock run 不触发长期记忆写入。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.2：StagePlanSegmentQueue 分段提交与顺序播放

**阶段目标**

建立 Agent 层 StagePlanSegment 队列，让本地 mock run 可以分多次提交 StagePlan 2.0 片段，并按顺序交给现有 Runtime 播放。

**前置条件**

- [ ] 阶段 3.1 已完成。
- [ ] 已确认 Segment 内部仍使用 StagePlan 2.0 的 stages/actions。

**开发任务**

- [ ] 支持本地 mock run 追加一个或多个 StagePlanSegment。
- [ ] 支持 Segment 按提交顺序播放。
- [ ] Segment 提交前复用现有 StagePlan 校验规则。
- [ ] Segment 播放仍进入现有 StagePlanPlayer 链路。
- [ ] 跟踪当前 segment、当前 stage 和队列剩余状态。
- [ ] 取消 run 时清空未播放 Segment。

**手动验收标准**

- [ ] Debug 可提交至少两个本地 Segment。
- [ ] 第一个 Segment 播放结束后自动播放下一个 Segment。
- [ ] Segment 中 speech / expression / bonePose 可转化为角色可见反馈。
- [ ] 非法 Segment 不进入队列。
- [ ] 取消 run 后未播放 Segment 不再执行。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.3：Runtime Observation 与取消 / 中断闭环

**阶段目标**

让 AgentRun 可以观察 Runtime 执行状态，并在用户打断、播放异常或队列清空时形成明确结果。

**前置条件**

- [ ] 阶段 3.2 已完成。
- [ ] 已确认本阶段仍以本地模拟为主。

**开发任务**

- [ ] 建立当前 run 的 RuntimeObservation 摘要。
- [ ] 观察当前 Segment、当前 stage、播放中 / 队列空 / 已完成等状态。
- [ ] 观察 speech / TTS / locomotion / validation warning / runtime error 的关键结果。
- [ ] 支持用户新输入或 Debug 操作中断当前 AgentRun。
- [ ] 中断时停止当前交互 run，并释放未完成队列。
- [ ] 明确 cancelled / failed / timed out 的状态差异。

**手动验收标准**

- [ ] Debug 可查看当前 run 的 observation 摘要。
- [ ] Segment 播放完成后 observation 能反映完成状态。
- [ ] 用户或 Debug 打断后 run 状态变为 cancelled。
- [ ] cancelled run 不触发 MemoryJudge。
- [ ] Runtime warning / error 能进入 Debug 观察信息。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.4：本地 Character Tool Registry / Tool Executor

**阶段目标**

建立本地角色工具注册与执行框架，用工具治理 Agent 与 Runtime 的交互边界。

**前置条件**

- [ ] 阶段 3.3 已完成。
- [ ] 已确认工具是治理边界，不是固定动作库。

**开发任务**

- [ ] 建立本地工具注册入口。
- [ ] 建立工具执行上下文，包含目标角色、当前 run、队列、校验、观察和 Debug 输出。
- [ ] 区分只读工具和写入工具。
- [ ] 支持只读工具并发安全，写入工具串行执行。
- [ ] 支持工具输入校验、执行结果和错误结果。
- [ ] 支持工具调用记录进入 Runtime Debug。

**手动验收标准**

- [ ] Debug 可看到本地工具注册状态。
- [ ] Debug 可触发本地只读工具并看到结果。
- [ ] Debug 可触发本地写入工具并看到队列变化。
- [ ] 写入工具不会绕过 StagePlanValidator 或 StagePlanPlayer。
- [ ] 工具失败时返回清晰错误，而不是静默失败。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.5：只读角色能力查询工具

**阶段目标**

实现 Agent 所需的基础只读工具，让角色能力、Runtime 状态和局部骨骼能力可以按需查询。

**前置条件**

- [ ] 阶段 3.4 已完成。
- [ ] 已确认不在初始 prompt 中继续全量注入骨骼、轴向和示例。

**开发任务**

- [ ] 支持查询当前 Runtime 状态。
- [ ] 支持查询当前角色可用 action 类型和简要规则。
- [ ] 支持按需查询局部骨骼能力。
- [ ] 支持按意图返回相关骨骼的轴向、范围和注意事项摘要。
- [ ] 支持查询 expression、preset animation、locomotion 的可用能力摘要。
- [ ] 避免一次性返回全部骨骼能力导致上下文过长。

**手动验收标准**

- [ ] Debug 可查询当前 Runtime 状态。
- [ ] Debug 可查询当前支持的 action 类型。
- [ ] Debug 可只查询手臂 / 头部 / 胸部等局部骨骼能力。
- [ ] 查询结果足以让后续 Agent 生成合法 bonePose 参数。
- [ ] 查询结果不会默认塞入全部骨骼和全部示例。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.6：校验与写入工具

**阶段目标**

实现 Agent 提交演出片段所需的校验和写入工具，形成 validate / commit / observe / finish / cancel 的本地工具闭环。

**前置条件**

- [ ] 阶段 3.5 已完成。
- [ ] 已确认写入类工具必须串行执行。

**开发任务**

- [ ] 支持预检 StagePlanSegment 是否可提交。
- [ ] 支持将通过校验的 Segment 追加到当前 AgentRun 队列。
- [ ] 支持观察当前 run 和队列状态。
- [ ] 支持正常结束当前 AgentRun。
- [ ] 支持取消当前 AgentRun。
- [ ] 工具结果返回错误、警告、建议和队列状态。

**手动验收标准**

- [ ] 合法 Segment 可通过工具提交并播放。
- [ ] 非法 Segment 被拒绝并返回明确错误。
- [ ] commit 前后 Debug 能看到队列状态变化。
- [ ] finish 后 run 进入 completed / finished 类状态。
- [ ] cancel 后 run 停止且不继续播放未完成 Segment。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.7：MotionCritic / QualityCritic warning-only 检查

**阶段目标**

建立动作质量检查能力，帮助 Agent 识别动作太简单、过度重复或缺少演出结构的问题，但不主动改写动作参数。

**前置条件**

- [ ] 阶段 3.6 已完成。
- [ ] 已确认 Critic 默认只返回 warning / suggestion。

**开发任务**

- [ ] 检查动作是否只有单个静态 pose。
- [ ] 检查是否缺少起势、主动作或收尾。
- [ ] 检查是否缺少头部、胸部、手腕等辅助配合。
- [ ] 检查是否过度接近示例动作。
- [ ] 检查是否与最近动作过度重复。
- [ ] 将 Critic 结果合并到 validate / commit 的 warning 和 suggestion 中。

**手动验收标准**

- [ ] 简单静态动作能产生质量 warning。
- [ ] 缺少收尾的表演片段能产生建议。
- [ ] 质量 warning 不会主动改写 bonePose 参数。
- [ ] 质量 warning 默认不阻止结构合法的 Segment 提交。
- [ ] Debug 可查看最近 Critic 结果。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.8：Runtime Debug Agent 页贯穿可观察性

**阶段目标**

将 AgentRun、工具调用、Segment 队列、Observation、warning 和 error 纳入统一 Runtime Debug。

**前置条件**

- [ ] 阶段 3.7 已完成。
- [ ] 已确认 Runtime Debug 是开发工具，不是正式产品 UI。

**开发任务**

- [ ] 新增或扩展 Runtime Debug 中的 Agent 观察入口。
- [ ] 显示当前 AgentRun id、状态、目标角色和生命周期。
- [ ] 显示最近工具调用列表和结果。
- [ ] 显示当前 Segment 队列、当前播放 Segment 和当前 stage。
- [ ] 显示 RuntimeObservation 摘要。
- [ ] 显示 validation、MotionCritic、runtime warning 和 error。
- [ ] 提供本地 mock run 测试入口。

**手动验收标准**

- [ ] Debug 可完整观察 AgentRun / Tool / Segment / Warning / Error。
- [ ] Debug 可触发本地 mock run。
- [ ] Debug 可观察 Segment 队列变化。
- [ ] Debug 可观察取消和完成状态。
- [ ] Debug 信息不进入普通 Momotalk 用户界面。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.9：Prompt 瘦身与按需能力查询

**阶段目标**

将当前初始 prompt 中的全量骨骼、轴向和示例移出，改为 Agent 通过工具按需查询能力，降低上下文噪声和示例锚定。

**前置条件**

- [ ] 阶段 3.8 已完成。
- [ ] 已确认 Prompt 瘦身不是减少角色能力。

**开发任务**

- [ ] 梳理当前 prompt 中的全量能力注入内容。
- [ ] 保留稳定规则、角色设定、StagePlan 基础约束和工具使用规范。
- [ ] 将全量骨骼、轴向、范围、effects 和大量示例从初始 prompt 中移出。
- [ ] 通过能力查询工具按需提供局部骨骼能力。
- [ ] 调整示例定位，避免示例被当成默认动作答案。
- [ ] Debug 可查看最终 prompt 或关键 prompt 摘要。

**手动验收标准**

- [ ] 初始 prompt 不再默认包含全部骨骼和全部轴向说明。
- [ ] Agent 可通过工具查询完成特定动作所需的局部能力。
- [ ] 角色能力没有因 prompt 瘦身而减少。
- [ ] 骨骼示例不再作为默认照抄答案出现。
- [ ] Debug 可辅助确认 prompt 组成和能力查询结果。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.10：Momotalk 正式入口接入 AgentRun

**阶段目标**

让 Momotalk 用户输入进入 AgentRun 主链路，用户能看到角色通过 speech / expression / bonePose 展示思考、回应、执行和收尾。

**前置条件**

- [ ] 阶段 3.9 已完成。
- [ ] 已确认最终正式入口不保留 one-shot StagePlan 兜底。

**开发任务**

- [ ] 将 Momotalk 正式用户消息接入 AgentRun。
- [ ] 用户消息触发当前角色的主交互 AgentRun。
- [ ] 新用户消息默认打断当前未完成 AgentRun。
- [ ] AgentRun 中的 speech action 同步为角色聊天气泡。
- [ ] typing / 未读 / 聊天历史继续围绕正式 AgentRun speech 工作。
- [ ] 普通用户不看到工具日志或内部规划。

**手动验收标准**

- [ ] 通过 Momotalk 发送普通短聊天能进入 AgentRun。
- [ ] 通过 Momotalk 请求简单动作能进入 AgentRun。
- [ ] 角色可见地表现思考、回应、动作和收尾。
- [ ] 新输入能打断正在执行的 AgentRun。
- [ ] 旧 one-shot LlmRelay 不再作为 Momotalk 正式主入口。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.11：真实 LLM 非流式多轮 tool call 闭环

**阶段目标**

接入真实 LLM 多轮工具调用，让模型通过工具结果继续决策，先完成非流式 AgentLoop 闭环。

**前置条件**

- [ ] 阶段 3.10 已完成。
- [ ] 已确认具体 LLM API / 协议细节在本阶段开始前单独讨论。

**开发任务**

- [ ] 建立 LLM 与本地 Character Tool Registry 的多轮调用桥接。
- [ ] 支持模型请求只读工具并接收结果。
- [ ] 支持模型提交 StagePlanSegment 并接收 commit 结果。
- [ ] 工具结果回写下一轮模型上下文。
- [ ] 支持最大轮数、超时、失败和用户打断治理。
- [ ] 底层 HTTP / API 调用能力可从旧 LlmRelay 中下沉复用。

**手动验收标准**

- [ ] 真实 LLM 可查询能力后再提交动作片段。
- [ ] 工具失败结果能回写给模型并影响下一轮输出。
- [ ] 简单动作请求可通过多轮工具调用完成。
- [ ] 表演请求可由多个 Segment 组成。
- [ ] 用户打断能停止当前真实 AgentRun。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.12：Streaming commit 后期阶段

**阶段目标**

在真实 LLM AgentLoop 基础上引入 streaming commit，让可执行 Segment 能在模型生成过程中尽早进入队列，减少复杂请求等待感。

**前置条件**

- [ ] 阶段 3.11 已完成。
- [ ] 已确认 streaming commit 是后期独立阶段。

**开发任务**

- [ ] 支持模型流式输出中识别可执行工具调用。
- [ ] 允许通过校验的 commit 尽早进入 Segment 队列。
- [ ] 保持只读工具并发、写入工具串行。
- [ ] 保证工具结果按稳定顺序回写上下文。
- [ ] 处理 streaming 失败、用户打断和未完成工具调用。
- [ ] Debug 可观察 streaming 工具状态。

**手动验收标准**

- [ ] 表演类请求不必等待完整最终回复才开始第一段可见反馈。
- [ ] streaming commit 不破坏 Segment 播放顺序。
- [ ] 写入工具不会并发污染队列。
- [ ] 用户打断时 pending / executing 工具能正确停止或丢弃。
- [ ] Debug 可看到 queued / executing / completed / error 等工具状态。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.13：Memory 迁移与旧 one-shot LlmRelay 主链路迁出

**阶段目标**

将长期记忆触发点迁移到正式 AgentRun 完成事件，并将旧 one-shot LlmRelay 业务链路从正式主链路迁出。

**前置条件**

- [ ] 阶段 3.12 已完成。
- [ ] 已确认旧 one-shot LlmRelay 不作为最终正式兜底。

**开发任务**

- [ ] 正常 finished 的正式 AgentRun 触发 MemoryJudge。
- [ ] cancelled / failed / debug mock run 默认不写入长期记忆。
- [ ] AgentRun speech 正常进入聊天历史和记忆候选上下文。
- [ ] 旧 LlmRelay 的完整 StagePlan 业务职责从 Momotalk 正式主链路迁出。
- [ ] 可复用的底层 HTTP / API 调用能力下沉为 AgentLoop 可用能力。
- [ ] 清理 Debug 文案中将 one-shot LlmRelay 视为正式主链路的表述。

**手动验收标准**

- [ ] 正常完成的正式 AgentRun 可触发 MemoryJudge。
- [ ] cancelled AgentRun 不触发 MemoryJudge。
- [ ] failed AgentRun 不触发 MemoryJudge。
- [ ] debug mock run 不触发 MemoryJudge。
- [ ] Momotalk 正式入口不再调用 one-shot 完整 StagePlan 链路。
- [ ] 项目中不存在旧链路作为正式兜底的说明或入口。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 阶段 3.14：第三阶段总体验收与归档准备

**阶段目标**

完成第三阶段整体体验验收，确认角色行为 Agent 主链路达到可继续迭代的稳定状态，并准备并入主文档。

**前置条件**

- [ ] 阶段 3.13 已完成。
- [ ] 已完成所有阶段内验收。

**开发任务**

- [ ] 汇总第三阶段最终能力和未做内容。
- [ ] 验证普通聊天、简单动作、表演请求、打断和记忆写入。
- [ ] 验证 Runtime Debug 可完整观察 AgentRun / Tool / Segment / Warning / Error。
- [ ] 验证旧 one-shot LlmRelay 不再作为正式主链路。
- [ ] 准备将 Stage3 完成状态并入根目录总 TODO 和 README。
- [ ] 准备必要的归档说明。

**手动验收标准**

- [ ] 普通短聊天可通过 AgentRun 完成。
- [ ] “打个招呼”类请求可通过 AgentRun 形成自然 speech 和多段动作。
- [ ] “跳个舞”类请求可形成多段 Segment、过程反馈和收尾。
- [ ] 执行中的 AgentRun 可被新用户输入打断。
- [ ] 正常 finished 的正式 AgentRun 可进入长期记忆判断。
- [ ] Runtime Debug 可观察 AgentRun / Tool / Segment / Warning / Error。
- [ ] 旧 one-shot LlmRelay 不再作为正式主链路。

**验收记录**

- 待填写。

**完成状态**

- [ ] 阶段完成。

## 历史与并入规则

- [ ] 第三阶段开发期间，本文件是 Stage3 的唯一阶段进展入口。
- [ ] 每个阶段必须在用户确认验收通过后再勾选。
- [ ] 第三阶段整体通过后，再将完成态同步到根目录 `DevelopmentTODO.md`。
- [ ] 第三阶段整体通过后，再按需要更新根目录 `README.md`。
- [ ] 阶段执行过程中的详细计划、验收记录或被暂缓内容，可按需要归档到 `Archive/Docs/`。
