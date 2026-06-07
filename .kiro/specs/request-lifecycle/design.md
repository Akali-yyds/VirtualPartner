# Design Document

更新时间：2026-06-07 ｜ 对应审查项：#3 ｜ 依据：requirements.md

## Overview

引入一个进程内的**请求生命周期注册表** `ConversationRequestRegistry`，作为每个 requestId 生命周期状态的单一真源，并充当事件中枢：

- `MomotalkConversationController`（编排者）负责**写入状态转移**（创建 Pending、标记 Replaced/Failed/Canceled/Finished）。
- `MemorySystem`（消费者）**订阅状态变化**，在终态时决定入队判定或丢弃记忆负载，不再维护自己的 `Canceled/Replaced/Finished` 标志。
- `StagePlanPlayer` 在 LLM 计划开始时**上报 Playing**（信息态，尽力而为）。
- `LlmRelay` 不直接依赖注册表（保持纯中继职责）；requestId 仍由它生成并经 submit 结果回传给编排者。

设计的硬约束是**行为等价**：所有现有时序（typing 替换、错误消息、speech 上屏、记忆入队、Clear Chat 取消）逐一对应到注册表驱动的等价路径。注册表由 `VirtualPartnerStage1Bootstrap`（组合根）创建并经各组件 `Configure(...)` / `ConfigureRuntime(...)` 注入，沿用 #8 的显式注入风格，不引入新的静态全局可变状态。

## Architecture

```text
LlmRelay.SubmitWithResult ──requestId──▶ MomotalkConversationController
                                              │ 写入转移 (Register / TrySetStatus)
                                              ▼
                                   ConversationRequestRegistry  ◀── StagePlanPlayer 上报 Playing
                                              │ StatusChanged 事件
                                              ▼
                                         MemorySystem（订阅，按终态处理记忆负载）
```

- **写方**：ConversationController 是唯一的转移驱动者（已订阅 LlmRelay/StagePlanPlayer 全部事件并处理 Clear Chat）。
- **读方/订阅方**：MemorySystem 订阅 `StatusChanged`；StagePlanPlayer 仅上报 Playing；DebugPanel 只读计数。
- **所有权与装配**：注册表实例由 Bootstrap 创建并注入；生命周期与 Play 会话一致。所有访问在 Unity 主线程，无需加锁。

## Components and Interfaces

### ConversationRequestRegistry（新增，纯 C# 类）

```csharp
public sealed class ConversationRequestRegistry
{
    ConversationRequest Register(int requestId, string characterId);   // 创建 Pending
    bool TrySetStatus(int requestId, RequestStatus status);            // 受控转移
    bool TryGet(int requestId, out ConversationRequest request);
    string GetCharacterId(int requestId);
    void MarkOlderPendingReplaced(string characterId, int newestRequestId);
    void CancelCharacter(string characterId);                          // 批量 → Canceled
    event Action<ConversationRequest> StatusChanged;
    int ActiveCount { get; }
    int CountByStatus(RequestStatus status);
}
```

转移规则（受控状态机）：仅 `Pending`/`Playing` 可进入任一终态；终态不可再变（幂等 no-op + 告警）；`Replaced/Canceled` 仅作用于尚未终态化的请求（等价于当前 `if (state.Finished || state.Queued) return;`）。批量操作先收集 id 再修改，避免遍历中改字典。

### 现有组件接入点

- `MomotalkConversationController`：`SendCurrentInput` 创建请求；`ReplaceStaleTypingViews`→`TrySetStatus(Replaced)`；`HandleLlmRequestFailed`→`TrySetStatus(Failed)`；`HandleStagePlanFinished`→`TrySetStatus(Finished)`；`CancelLlmForCharacter`→`CancelCharacter`；用 `registry.GetCharacterId` 替换 `requestCharacterIds`，用状态查询替换 `clearedRequestIds`。typingViews 保留（UI 视图状态），但增删由注册表事件/查询驱动。
- `MemorySystem`：订阅 `StatusChanged`；`Finished`→按 HasSpeech 入队判定，`Failed/Canceled/Replaced`→丢弃负载；移除自身 `Canceled/Replaced/Finished` 标志；保留 `RegisterUserMessage`/`RecordSpeech` 负载与 `Queued`。
- `StagePlanPlayer`：LLM 计划开始时 `TrySetStatus(Playing)`（尽力而为，信息态）。speech 仍走既有 `SpeechActionStarted` 事件。
- `VirtualPartnerStage1Bootstrap`：创建注册表实例并注入上述三者。
- `VirtualPartnerRuntimeDebugPanel`：展示 `ActiveCount` 与各状态计数（可选增量）。

### 现有行为到新路径映射

| 当前实现 | 新路径 |
|---|---|
| `requestCharacterIds[id]=cid` | `registry.Register(id, cid)` |
| `memorySystem.MarkOlderRequestsReplaced` | `registry.MarkOlderPendingReplaced` |
| `MarkRequestReplaced` | `registry.TrySetStatus(id, Replaced)` |
| `clearedRequestIds.Add(id)` | `registry.TrySetStatus(id, Canceled)` / 查询 `Status` |
| `memorySystem.MarkRequestCanceled` | 由 `StatusChanged(Failed/Canceled)` 驱动 |
| `memorySystem.MarkRequestFinished` | 由 `StatusChanged(Finished)` 驱动入队 |
| `GetCharacterIdForRequest` | `registry.GetCharacterId` |

## Data Models

```csharp
public enum RequestStatus
{
    Pending,    // LLM 在途
    Playing,    // StagePlan 播放中（信息态）
    Finished,   // 正常完成
    Failed,     // LLM/校验失败
    Canceled,   // 用户 Clear Chat 等主动取消
    Replaced    // 被更新的请求替换
}

public sealed class ConversationRequest
{
    public int RequestId { get; }
    public string CharacterId { get; }   // 规范化（小写 trim）
    public RequestStatus Status { get; internal set; }
    public bool IsTerminal => Status >= RequestStatus.Finished;
}
```

记忆负载（UserText、Speeches、Queued）仍留在 `MemorySystem`（属 MemoryJudge 细节，非生命周期），仅把 `Canceled/Replaced/Finished` 标志改为查询注册表。

生命周期与回收：终态请求在 `StatusChanged` 派发完成后从活跃字典移除（消费者在回调内读取），使活跃集合规模与未完成请求数相称，满足需求 3。

## Error Handling

- **非法转移**：对终态请求或不存在的 requestId 调用 `TrySetStatus` 返回 false 并 `Debug.LogWarning`（含 requestId 与目标状态），不抛异常、不破坏现有记录。
- **未注册 requestId 查询**：`TryGet` 返回 false，`GetCharacterId` 返回空串，调用方按既有空值路径处理（与当前 `requestCharacterIds.TryGetValue` 失败路径等价）。
- **注入缺失**：若注册表引用未注入，组件回退到“无注册表”降级路径并告警，但本设计要求 Bootstrap 必注入（沿用现有 ValidateReferences 风格）。
- **并发**：所有访问发生在 Unity 主线程（经 ManualUpdate/事件回调），注册表无需加锁。

## Correctness Properties

### Property 1: 单一终态

任一 requestId 最多进入一个终态；进入终态后 `Status` 不再改变（后续 `TrySetStatus` 为 no-op 并返回 false）。

**Validates: Requirements 1.3, 2.2, 2.4**

### Property 2: characterId 不变

请求创建后 `CharacterId` 在其整个生命周期内不被修改。

**Validates: Requirements 1.1, 2.6**

### Property 3: 合法转移

仅 `Pending`/`Playing` 可转入终态；任何其它转移被拒绝并告警，且不改变被操作请求的现有状态。

**Validates: Requirements 1.2, 4.2**

### Property 4: 活跃集合有界

提交 N 个请求并全部终态化后，`ActiveCount` 回到与未完成请求数相称的水平，不随历史请求总数线性增长。

**Validates: Requirements 1.3, 3.1**

### Property 5: 批量替换只影响未终态

`MarkOlderPendingReplaced(cid, newest)` 只把该角色下仍处于 `Pending`/`Playing` 的更早请求置为 `Replaced`，不改变已 `Finished`/已入队的请求，也不影响 `newest` 自身。

**Validates: Requirements 2.1**

## Testing Strategy

- **EditMode 单测**（新增，针对纯逻辑注册表）：覆盖 Property 1–5，无需 Unity 运行时。
- **编译验证**：每阶段 `refresh + force compile`，0 error。
- **Play Mode 手动清单**（每阶段）：普通对话与流式分段动作；连发打断（旧 typing 替换/移除、记忆不误写）；LLM 失败（系统错误消息 + 记忆取消）；Clear Chat（在途+播放中取消、停止 StagePlan）；多轮记忆写入/跳过结果与改造前一致；调试面板请求计数正确。

### 分阶段迁移（每阶段可编译、可验证、可回退）

- **阶段 0（脚手架）**：新增 `RequestStatus`/`ConversationRequest`/`ConversationRequestRegistry` + EditMode 单测；Bootstrap 创建并注入三组件；组件**双写**（既写注册表也保留原逻辑），注册表为被动观察者；调试面板加只读计数。验证行为零变化。
- **阶段 1（characterId 切源）**：ConversationController 改用 `registry.GetCharacterId/Register` 替换 `requestCharacterIds`。
- **阶段 2（取消/替换切源）**：用 `TrySetStatus(Replaced/Canceled)` + 查询替换 `clearedRequestIds`；`CancelLlmForCharacter` 用 `CancelCharacter`。
- **阶段 3（MemorySystem 消费化）**：订阅 `StatusChanged`，移除自身 `Canceled/Replaced/Finished` 标志，仅留负载。
- **阶段 4（Playing 上报 + 清理）**：StagePlanPlayer 上报 Playing；移除冗余 Mark* 调用与死副本字段；最终回归。

任一阶段出问题可用 git 回退到上一阶段。
