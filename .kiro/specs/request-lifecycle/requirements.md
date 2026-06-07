# Requirements Document

更新时间：2026-06-07 ｜ 对应审查项：#3

## Introduction

当前一次 LLM 请求（requestId）的生命周期状态被切碎到 4 个组件，各自维护并行副本：

- `LlmRelay`：requestId 生成、`requestPending`、取消（`StopPendingRequest` 自增 id 失效旧请求）。
- `MomotalkConversationController`：`typingViews`、`requestCharacterIds`、`clearedRequestIds`、`unreadCounts`。
- `MemorySystem`：`turns`（每请求的 `Canceled/Replaced/Finished/Queued` + 文本/speeches）。
- `StagePlanPlayer`：`activeRequestId`、streaming 状态。

同一语义（“被替换 / 取消 / 完成”）需要在多处分别标记（`MarkOlderRequestsReplaced`、`MarkRequestReplaced`、`ReplaceStaleTypingViews`、`clearedRequestIds.Add` 等），易出现状态不一致，新增状态分支要改多个文件。

本特性引入一个**单一真源**承载每个 requestId 的生命周期状态，其余组件订阅 / 查询它而非各自维护副本。核心约束是**对当前可体验版本保持行为等价**：不改变 Momotalk 表现、记忆判定结果、动作播放时机。

非目标（本 Spec 不做）：

- 不改 StagePlan 2.0 契约、不改提示词内容。
- 不改 MemoryJudge 判定逻辑本身（只改“何时入队 / 跳过 / 取消”的来源）。
- 不做 Momotalk UI 资产化（属 #5）。
- 不引入多角色并发会话的新能力（保持当前单活跃会话模型）。

## Glossary

- **ConversationRequest**：以 requestId 为键的请求记录，含 characterId、状态、时间戳等。
- **生命周期状态机**：`Pending`（LLM 在途）→ `Playing`（StagePlan 播放中）→ 终态之一 `Finished` / `Failed` / `Canceled` / `Replaced`。
- **单一真源（registry）**：集中存放所有 ConversationRequest 的注册表组件。

## Requirements

### Requirement 1: 单一真源注册表

**User Story:** 作为开发者，我希望所有请求状态集中在一处，这样新增或调试状态分支时只改一个地方。

#### Acceptance Criteria
1. WHEN 一个新请求被提交（`LlmRelay.SubmitWithResult` 接受） THEN 系统 SHALL 在注册表中创建一条 ConversationRequest，包含 requestId、characterId、初始状态 `Pending`。
2. THE 注册表 SHALL 提供按 requestId 查询当前状态与 characterId 的接口。
3. WHEN 请求进入任一终态 THEN 注册表 SHALL 从活跃集合移除该记录或标记为终态并允许回收，使活跃记录规模不随会话时长无限增长。
4. WHERE 组件需要请求状态 THE 组件 SHALL 查询注册表或订阅其事件，而不是维护独立的状态字典。

### Requirement 2: 状态转移与既有行为等价

**User Story:** 作为用户，我希望重构后 Momotalk 的对话、打断、取消、记忆行为与之前完全一致。

#### Acceptance Criteria
1. WHEN 用户发送新消息且存在更早的未完成请求 THEN 系统 SHALL 将更早请求置为 `Replaced`，其表现（typing 视图移除或替换为系统消息）与当前实现一致，并受 `ShowReplacedSystemMessage` 控制。
2. WHEN LLM 请求失败（`RequestFailed` 事件） THEN 对应请求 SHALL 置为 `Failed`，并产生与当前一致的系统错误消息与记忆取消。
3. WHEN StagePlan 首个 speech 事件触发 THEN 对应请求 SHALL 反映为已产出（typing 转为正式消息），且记忆记录 speech 与当前一致。
4. WHEN StagePlan 完成（`StagePlanFinished`，owner=LLM） THEN 对应请求 SHALL 置为 `Finished`，记忆按既有规则入队判定。
5. WHEN 用户对某角色执行 Clear Chat THEN 该角色所有在途或播放中的请求 SHALL 置为 `Canceled`，并停止 LLM pending 与正在播放的 StagePlan，行为与当前 `CancelLlmForCharacter` 一致。
6. THE characterId 与 requestId 的关联 SHALL 由注册表统一维护，替换 `requestCharacterIds`。

### Requirement 3: 内存与清理

**User Story:** 作为长会话用户，我希望请求状态不随会话时长无限增长。

#### Acceptance Criteria
1. WHEN 请求终态化 THEN 注册表 SHALL 界定活跃记录规模，等价或优于当前 `MemorySystem.turns` 已做的回收。
2. WHEN 组件销毁（`OnDestroy`） THEN 相关订阅 SHALL 被正确解除。

### Requirement 4: 可观测性

**User Story:** 作为开发者，我希望在调试面板看到请求生命周期状态，便于定位问题。

#### Acceptance Criteria
1. THE 系统 SHALL 暴露当前活跃请求数与各状态计数等只读信息，供 `VirtualPartnerRuntimeDebugPanel` 展示（可选增量）。
2. IF 发生非预期的状态转移（如对不存在的 requestId 操作） THEN 系统 SHALL 记录可定位的警告，而不是静默吞掉。

### Requirement 5: 渐进迁移与可回退

**User Story:** 作为维护者，我希望迁移分步进行，每步可编译、可验证、可回退。

#### Acceptance Criteria
1. THE 迁移 SHALL 分阶段进行，每阶段结束时工程可编译且主链路行为不变。
2. WHILE 迁移进行中 THE 系统 SHALL 不破坏 Momotalk、LlmRelay、StagePlan、Memory、TTS/ASR 任一现有功能。
3. THE 每个阶段 SHALL 可通过 Unity 编译验证加明确的 Play Mode 验证清单确认。
