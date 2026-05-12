# VirtualPartner / Toki 第二阶段开发方向细节文档

## 0. 文档定位

本文档为 VirtualPartner / Toki 项目第二阶段的正式开发方向细节文档，用于承接第一阶段已经验收通过的 Runtime 闭环，并作为后续与开发 agent 对接的阶段性需求依据。

第二阶段的核心定位是：

> 在第一阶段“LLM 控制虚拟角色的最小闭环”基础上，推进到“具备正式交互入口、语音输入输出、基础表情反馈、长期记忆和多角色架构预留的桌面陪伴角色体验版本”。

第二阶段不推翻第一阶段已经验证的系统原则，但会将第一阶段的绝对时间轴 timeline 方案升级为更适合语音与动作同步的 StagePlan 2.0 阶段式执行模型。第一阶段 timeline 1.0 作为已验收版本进行项目外备份，不再作为第二阶段主系统的活跃格式。

---

## 1. 第一阶段继承基础

第一阶段已经完成并通过验收，核心闭环为：

```text
用户输入文本
↓
LlmRelay 请求 LLM
↓
LLM 返回 JSON timeline
↓
TimelineValidator 校验
↓
TimelinePlayer 播放
↓
ActionCoordinator 管理骨骼 owner / 抢占 / 过渡
↓
AvatarPoseApplier 写入最终骨骼 localRotation
↓
RootOrientationController / LocomotionActionExecutor 处理 Root 朝向与位移
```

第一阶段已实现能力包括：

- Idle 常驻。
- BaseRotation 自动采集。
- BoneMap。
- Debug 单骨骼控制。
- ActionCoordinator。
- 本地 JSON TimelinePlayer。
- speech。
- bonePose。
- 预设动画。
- facing。
- locomotion。
- MovementConstraint。
- FSM 自主行为。
- LlmRelay。
- 模块化 Prompt。
- 统一 Runtime Debug 面板方向。

第二阶段继续继承以下原则：

- LLM 不直接控制 Unity 对象。
- LLM 只输出 Runtime 支持的结构化 JSON。
- Runtime 严格执行明确结构，不做隐藏补丁。
- 所有新增能力都通过 Profile / Registry / 白名单显式开放。
- 不自动搜索乱绑。
- 不让 LLM 接触真实 Transform path。
- Timeline / StagePlan 播放模块不直接写骨骼。
- ActionCoordinator 仍然统一管理骨骼 owner、抢占、释放和交接过渡。
- AvatarPoseApplier 仍然是最终骨骼 Transform 写入出口。
- Root position / rotation 仍然只能由 Root 模块修改。
- FSM 不抢占 LLM 用户交互。
- 用户交互优先级仍高于 FSM。

---

## 2. 第二阶段总体目标

第二阶段建议命名为：

> **正式交互入口与语音陪伴体验增强版**

第二阶段主线范围：

1. Momotalk UI 正式交互入口。
2. StagePlan JSON 2.0 阶段式执行模型。
3. 嘴型 / 基础表情系统 V1。
4. TTS 语音克隆输出 V1。
5. ASR 语音输入 V1。
6. Markdown 长期记忆 V0。
7. 多角色 CharacterProfile 架构预留。
8. Debug 面板整合与收尾。

第二阶段核心体验目标：

> 用户可以通过正式 Momotalk UI 与 Toki 进行文本和语音交流；Toki 能使用角色音色回复、嘴巴随语音或文本开合、具备基础表情反馈、自动记录长期有效信息，并为后续多角色扩展做好结构准备。

---

## 3. 第二阶段明确不做内容

以下内容不作为第二阶段主线目标：

1. 多角色同时在场的完整调度。
2. 多角色之间主动对话。
3. 多角色群聊。
4. 家具 / 道具完整交互系统。
5. NavMesh 路径规划。
6. 复杂避障。
7. RAG / 向量数据库长期记忆。
8. 完整复杂情绪人格系统。
9. 手指自由骨骼开放给 LLM 控制。
10. 完整 Momotalk 编辑器、贴纸、图片、社交系统。
11. 商业发布级音色训练闭环。

这些内容可以作为第三阶段或后续版本候选方向。

---

# Part A：Momotalk UI

## A.1 功能定位

Momotalk UI 是第二阶段的正式交互入口。它负责展示联系人、聊天记录、输入框、语音按钮、Toki 回复内容和对话状态。

Momotalk UI 只负责展示与沟通，不直接控制角色骨骼、不直接写 Root、不绕过 StagePlan / Runtime 校验。

Momotalk UI 的职责：

- 显示右侧手机按钮。
- 显示手机式 Momotalk 面板。
- 展示联系人列表。
- 展示聊天页。
- 接收用户文本输入。
- 接收 ASR 识别结果。
- 调用 LlmRelay 提交用户消息。
- 显示用户消息。
- 显示通过 StagePlan 校验后的 speech。
- 显示 typing indicator。
- 保存和恢复聊天记录。
- 显示未读红点 / 未读数。

角色动作、表情、语音播放、嘴型驱动仍由 Runtime 对应模块执行。

## A.2 UI 打开方式

运行后，屏幕右侧显示一个小手机 / 消息按钮。

交互流程：

```text
Play
↓
右侧显示小手机按钮
↓
点击按钮
↓
9:16 手机界面从右侧滑出
↓
显示 Momotalk Loading
↓
进入联系人页或上次页面
```

手机面板设计：

- 固定 9:16 比例。
- 高度占屏幕 85%–92%。
- 从右侧滑出。
- 点击手机外区域关闭。
- 关闭手机不会中断 LLM 请求、StagePlan 播放、TTS 播放或角色行为。

建议状态：

```text
Collapsed
Opening
Loading
ContactList
Chat
Closing
```

## A.3 页面结构

建议 Prefab 结构：

```text
MomotalkCanvas
├── RightFloatingButton
│   └── PhoneIcon / MessageIcon
│
└── MomotalkRoot
    ├── OutsideCloseOverlay
    └── PhonePanel
        ├── PhoneFrame
        ├── LoadingView
        ├── ContactListView
        │   ├── Header
        │   ├── SearchBar
        │   └── ContactItem
        │
        └── ChatView
            ├── Header
            │   ├── BackButton
            │   ├── Avatar
            │   ├── Name
            │   └── Status
            │
            ├── MessageScrollView
            │   ├── UserMessageBubble
            │   ├── CharacterMessageBubble
            │   └── TypingIndicatorBubble
            │
            └── InputBar
                ├── VoiceButton
                ├── TextInput
                └── SendButton
```

## A.4 页面恢复规则

首次打开：进入联系人页。

后续打开：默认恢复上次页面。

建议 Inspector 配置：

```text
MomotalkOpenPageMode:
  AlwaysContactList
  RestoreLastPage
  AlwaysLastCharacterChat
```

默认建议：`RestoreLastPage`。

## A.5 Loading 行为

每次打开手机 UI 时显示 Loading。

建议 Inspector 配置：

```text
LoadingMode:
  EveryOpen
  FirstOpenOnly
  Never

loadingDuration: 0.8s 默认
```

Loading 可以参考 Momotalk 风格：粉色背景、MomoTalk 字样、轻微闪烁 / 缩放 / 淡入动画。

## A.6 联系人列表

第二阶段当前主要是 Toki，但联系人列表需要从当前场景中已注册的角色 RuntimeContext 自动生成，为后续多角色预留。

联系人显示条件：

```text
角色存在 CharacterProfile
且当前场景中存在已注册 CharacterRuntimeContext
→ 显示在 Momotalk 联系人列表

只有 CharacterProfile、但当前场景中没有 RuntimeContext
→ 不显示在 Momotalk 联系人列表
```

联系人项显示：

- 头像。
- 名字。
- 最后一条消息。
- 时间。
- 未读红点 / 未读数。
- 状态文字。

联系人数据来自 CharacterProfile + CharacterRuntimeContext + ChatHistory，不在 UI 中硬编码 Toki。

## A.7 聊天页消息显示规则

用户消息：

- 发送后立即显示在 UI 中。
- 写入聊天记录 JSON。

角色回复：

- 只来自通过 StagePlanValidator 校验后的 speech action。
- 不直接显示 raw LLM 文本。
- 一个 stage 最多一个 speech。
- 多个 stage 的 speech 显示为多条气泡，不合并。
- 气泡出现顺序跟随 StagePlan 阶段执行顺序。

示例：

```text
stage_1: speech A
stage_2: speech B
```

UI 行为：

```text
stage_1 执行到 speech：插入角色气泡 A
stage_1 speech 完成后进入 stage_2
stage_2 执行到 speech：插入角色气泡 B
```

## A.8 Typing Indicator

用户发送消息后，在角色回复出现前显示三点 typing indicator。

显示时机：

1. LLM 请求 pending 时。
2. StagePlan 已返回但第一句 speech 尚未出现时。
3. 两个 stage 的 speech 间隔较长时。

建议配置：

```text
showTypingBeforeFirstSpeech = true
showTypingBetweenSpeech = true
typingGapThreshold = 1.2s
```

Typing indicator 可用 Unity UI 实现，不依赖 gif：

- 一个圆角气泡。
- 三个圆点。
- 圆点按时间错开上下跳动或明暗变化。

## A.9 多次 Submit 行为

用户在 LLM pending 或 StagePlan 播放期间仍允许继续发送新消息。

行为遵循 latest request wins 逻辑：

- 新 Submit 发起新的 LLM 请求。
- 旧 pending 请求返回后应被丢弃。
- 当前旧 LLM StagePlan 不一定在发送瞬间停止。
- 新请求返回并通过校验后，用新 StagePlan 替换当前 LLM StagePlan。

## A.10 关闭与未读

关闭手机 UI 不会中断任何运行中流程。

如果 Momotalk 关闭期间角色有新 speech 进入聊天记录：

- 右侧小按钮显示红点。
- 联系人列表中对应角色显示未读数。
- 进入对应角色聊天页后清除未读。

## A.11 聊天记录保存

聊天记录需要保存，但必须和长期记忆分开。

建议路径：

```text
VirtualPartner/UserData/ChatHistory/{characterId}.json
```

长期记忆路径：

```text
VirtualPartner/UserData/Memory/{characterId}/*.md
```

聊天记录是流水账，长期记忆是筛选后的长期有效信息。

聊天页加载规则：

- 默认加载最近 50 条。
- 进入聊天页后自动滚动到最新消息。
- 新消息出现时，默认滚动到底部。

建议消息结构：

```json
{
  "messageId": "msg_20260512_001",
  "characterId": "toki",
  "sender": "user",
  "text": "今天我们继续做第二版吧。",
  "timestamp": "2026-05-12T18:30:00",
  "status": "sent"
}
```

角色消息可附加：

```json
{
  "messageId": "msg_20260512_002",
  "characterId": "toki",
  "sender": "character",
  "text": "好呀，我们继续。",
  "timestamp": "2026-05-12T18:30:05",
  "planId": "stage_plan_abc",
  "stageId": "stage_1",
  "status": "shown"
}
```

## A.12 场景 Speech Bubble 与 Momotalk 的关系

Momotalk 打开时，场景 speech bubble 行为做成可选项。

建议枚举：

```text
SceneSpeechBubbleModeWhenMomotalkOpen:
  KeepVisible
  Hide
  Dimmed
```

默认值后续可根据体验测试调整。

## A.13 Momotalk UI 建议模块

```text
MomotalkUIManager
  打开 / 关闭手机
  Loading
  页面切换
  上次页面恢复

MomotalkContactListView
  从已注册 RuntimeContext 生成联系人
  搜索过滤
  未读数
  最后一条消息

MomotalkChatView
  消息气泡
  输入框
  语音按钮
  typing indicator
  滚动到底部

MomotalkConversationController
  用户发送文本
  确定 targetCharacterId
  调用 LlmRelay
  管理 pending 状态
  写入聊天记录

MomotalkStagePlanBridge
  监听 StagePlan speech
  按 stage 执行顺序插入角色气泡
  控制 typing indicator

MomotalkHistoryStore
  保存 / 读取聊天记录 JSON
  最近 50 条消息
  更新联系人最后一条消息
```

---

# Part B：StagePlan JSON 2.0 阶段式执行模型

## B.1 升级原因

第一阶段 timeline 1.0 是绝对时间轴格式，每段必须有 start / end，Runtime 按时间点执行动作。

该方式适合无语音或短动作演出，但第二阶段接入 TTS 后，speech 的真实音频长度无法完全预测。若仍严格按绝对时间推进，可能出现：

```text
第一句语音尚未播放完
↓
下一段动作 / 下一句 speech 已经开始
↓
语义和动作错位
```

因此第二阶段将主输出格式升级为 StagePlan 2.0：

```text
一个 stage 执行完成后，再进入下一个 stage。
```

## B.2 1.0 与 2.0 的关系

第一阶段 timeline 1.0：

- 作为已验收版本备份。
- 备份后移出当前主项目运行系统。
- 不再作为第二阶段 Runtime 的活跃格式。

第二阶段 StagePlan 2.0：

- 成为当前系统唯一主格式。
- Momotalk、TTS、表情、记忆、多角色预留都围绕 2.0 适配。

这样可以避免 1.0 与 2.0 同时存在造成开发 agent 混淆。

## B.3 标准 JSON 格式

```json
{
  "schemaVersion": "2.0",
  "type": "stagePlan",
  "metadata": {
    "intent": "greeting",
    "mood": "happy"
  },
  "stages": [
    {
      "stageId": "stage_1",
      "actions": [
        {
          "type": "expression",
          "name": "smile",
          "duration": 0.3
        },
        {
          "type": "speech",
          "text": "老师，今天也来啦。",
          "emotion": "happy",
          "speed": 1.0
        }
      ]
    }
  ]
}
```

字段说明：

```text
schemaVersion:
  固定为 "2.0"。

type:
  固定为 "stagePlan"。

metadata:
  可选，仅用于 Debug、日志、记忆判断等，不作为 Runtime 强执行依据。

stages:
  阶段数组，按顺序执行。

stageId:
  可选，但建议生成，便于 Debug。

actions:
  当前 stage 内并行执行的 action 列表。
```

## B.4 Stage 执行规则

核心规则：

```text
stages 按顺序执行。
stage 内 actions 同时开始。
stage 内所有 blocking action 完成后，才进入下一 stage。
每个 stage 最多一个 speech action。
```

每个 stage 最多一个 speech 的原因：

```text
stage 内 actions 是并行开始的。
如果一个 stage 内有多个 speech，会导致多句话同时说或时序混乱。
如果需要多句回复，必须拆成多个 stage。
```

## B.5 Action 完成条件

默认 blocking action：

```text
speech:
  必须等待 TTS 音频播放完成。
  如果 TTS 不可用，则用文本估算 speaking duration。

expression:
  按 duration 进入表情。
  默认持续到当前 stage 结束。
  stage 结束后释放，回 IdleFace。

bonePose:
  duration 表示从当前姿态过渡到目标姿态的时间。
  duration 完成后视为该 action 完成。

facing:
  转向完成后视为完成。

animation:
  动画播放完成后视为完成。

locomotion:
  移动完成，或被 MovementConstraint 阻挡并停止后视为完成 / 失败结束。
```

第二阶段不建议让 LLM 显式填写 blocking 字段，先由 Runtime 按 action 类型使用默认规则。

## B.6 duration 含义

`duration` 用于控制动作过渡速度或动作持续时间。

```text
duration 越小：动作越快。
duration 越大：动作越慢。
```

适用对象：

- expression。
- bonePose。
- facing。
- animation blend。
- gesture。
- locomotion。

speech 不使用 duration 强行控制长度。speech 的真实完成时间来自：

- TTS 音频长度。
- 无 TTS 时文本估算时长。

speech 可以使用 `speed` 作为 TTS 语速参数。

## B.7 speech action

```json
{
  "type": "speech",
  "text": "老师，我们继续吧。",
  "emotion": "happy",
  "speed": 1.0,
  "voiceId": "toki_default"
}
```

规则：

- `text` 必填。
- `emotion` 可选。
- `speed` 可选。
- `voiceId` 可选。
- 不允许在 speech 内嵌 expression。
- 需要表情时，LLM 必须单独写 expression action。

默认值来自 CharacterVoiceProfile。

## B.8 expression action

```json
{
  "type": "expression",
  "name": "smile",
  "duration": 0.3
}
```

规则：

- expression 是第二阶段正式 action。
- expression 必须来自 ExpressionProfile 白名单。
- 第二阶段第一批白名单：

```text
neutral
smile
thinking
surprised
embarrassed
```

- stage 内没有 expression 时，默认使用 IdleFace。
- expression 结束后释放，回 Idle 面部状态。

## B.9 错误处理

结构性错误：整条 StagePlan 拒绝。

包括：

```text
JSON 解析失败。
缺少 schemaVersion。
schemaVersion 不是 2.0。
type 不是 stagePlan。
缺少 stages。
stages 不是数组。
stage 结构非法。
stage 内缺少 actions。
speech 缺少 text。
```

局部 action 错误：warning 跳过该 action。

包括：

```text
不存在的 expression。
不存在的 animation。
不存在的 bone。
非法 speed。
非法 emotion。
TTS 失败。
locomotion 被障碍阻挡。
```

如果某个 stage 内所有 action 都无效：跳过该 stage。

如果所有 stage 都无效：整条 StagePlan 拒绝。

## B.10 建议模块

```text
StagePlanData
  schemaVersion
  type
  metadata
  stages

StageData
  stageId
  actions

StagePlanParser
  raw JSON → StagePlanData

StagePlanValidator
  结构校验
  action 白名单校验
  局部错误 warning

StagePlanPlayer
  顺序执行 stages
  并行启动 stage actions
  等待 blocking actions 完成

StageActionExecutor
  分发 speech / expression / bonePose / animation / facing / locomotion 等 action

StagePlanDebugView
  显示当前 stage index
  显示 running actions
  显示完成状态
```

---

# Part C：嘴型 / 基础表情系统 V1

## C.1 当前模型嘴巴能力

Toki 的嘴巴不是骨骼，也不是整脸贴图替换，而是：

```text
8×8 嘴型图集
+ shader 局部采样
+ MaterialPropertyBlock 修改 _MouthTileTex_ST 的 UV offset
```

底层关键点：

- `CH0187_Body` 的第 3 个材质槽使用 `CH0187_EyeMouth.mat`。
- 材质中 `_MouthTileTex` 使用 `Character_Mouth.png`。
- `Character_Mouth.png` 被当作 8×8 嘴型图集。
- `MouthIndex` 范围为 0–63。
- Runtime 将 `MouthIndex` 转换成 UV offset。
- 通过 MaterialPropertyBlock 写入对应 renderer 的材质槽。
- 不实例化材质，不污染共享材质资产。

## C.2 嘴巴控制模块

建议封装：

```text
MouthTextureController
```

职责：

- 持有 SkinnedMeshRenderer。
- 持有 materialSlotIndex。
- 持有 8×8 嘴型图集配置。
- 根据 mouthIndex 设置 `_MouthTileTex_ST`。
- 设置 `_MouthTileEnabled`。
- 提供 SetMouthIndex / ResetMouth。

该模块只负责嘴巴 index 切换，不负责 LLM、不负责表情逻辑、不负责 speech。

## C.3 表情系统形态

表情采用 Mixed Expression System：

```text
Expression = MouthIndex + FaceBonePose
```

例如 `smile` 可以包含：

```text
嘴巴：smile_closed / smile_open_small / smile_open_mid / smile_open_large
面部骨骼：眼睛、眼眶、眉毛等轻微调整
```

LLM 不直接控制 mouthIndex，而是调用白名单表情：

```json
{
  "type": "expression",
  "name": "smile",
  "duration": 0.3
}
```

Runtime 根据 ExpressionProfile 决定 mouthIndex 和面部骨骼姿态。

## C.4 第一批表情白名单

第二阶段第一批表情：

```text
neutral
smile
thinking
surprised
embarrassed
```

后续可根据视觉确认、嘴型 index 和面部骨骼参数继续扩展。

## C.5 面部骨骼控制权

面部骨骼也必须走 ActionCoordinator 的 owner / handoff 机制。

即：

```text
ExpressionActionExecutor
↓
ActionCoordinator 申请面部骨骼 owner
↓
AvatarPoseApplier / 面部姿态出口写入
↓
expression 结束后释放
↓
平滑回 IdleFace
```

眼睛、眉毛、眼眶等骨骼不能由 Expression 系统直接写 Transform。

## C.6 表情生命周期

表情遵循 StagePlan stage 生命周期。

```text
expression action 开始
↓
进入对应表情
↓
持续当前 stage
↓
stage 结束后释放
↓
回到 IdleFace
```

不是永久保持，也不是固定回 neutral，而是回到动态 Idle 面部状态。

## C.7 Speech 嘴型覆盖规则

说话时，嘴型覆盖当前 expression 的基础嘴巴，但不覆盖上半脸表情。

例如当前 expression 是 `smile`：

```text
不说话：smile_closed
说话中：smile_open_small / smile_open_mid / smile_open_large
说完：回到 smile_closed
stage 结束：expression 释放，回 IdleFace
```

嘴型优先级：

```text
Debug Mouth Override
> Speech Mouth Driver
> Expression Base Mouth
> Idle / Default Mouth
```

## C.8 Mouth Index 对照表

具体 mouth index 后续由人工视觉确认后填入配置。

示例：

```text
neutral_closed = ?
neutral_open_small = ?
neutral_open_mid = ?
neutral_open_large = ?

smile_closed = ?
smile_open_small = ?
smile_open_mid = ?
smile_open_large = ?

surprised_open = ?
thinking_closed = ?
embarrassed_closed = ?
```

## C.9 建议模块

```text
MouthTextureController
  负责 8×8 嘴型图集 index 切换

MouthPoseProfile
  记录 closed / small / mid / large 等嘴型 index

ExpressionProfile
  记录 expressionName、baseMouth、speakingMouthSet、faceBonePose

ExpressionActionExecutor
  执行 expression action，申请面部骨骼 owner

SpeechMouthDriver
  根据 TTS 音量或文本 fallback 驱动嘴型开合

FaceBoneMapProfile
  记录眼睛、眉毛、眼眶等面部语义骨骼

ExpressionDebugPanel
  手动测试表情、嘴型 index、面部骨骼释放和回 Idle
```

---

# Part D：TTS 语音输出与语音克隆 V1

## D.1 功能定位

TTS 负责将 StagePlan 中的 speech 文本转换为角色语音，并驱动 Unity 中的 AudioSource 播放和嘴型变化。

第二阶段目标不是只接普通 TTS，而是尽量直接进入语音克隆路线，便于测试 Toki 和后续多角色音色。

## D.2 技术路线候选

优先测试：

1. GPT-SoVITS。
2. CosyVoice。
3. Fish Speech。

选择理由：

- 用户本地已有 Toki 和多角色示例音频。
- 第二阶段希望直接测试角色音色克隆。
- GPT-SoVITS 对中文 / 日文角色音色实验较友好。
- CosyVoice 和 Fish Speech 可作为更现代、多语种、情绪表达方向的备选。

## D.3 TTS 服务形态

TTS 做成本地 Python 服务。

建议目录：

```text
VirtualPartner/
  LocalServices/
    TTS/
      tts_service.py
      config.json
      requirements.txt
      voices/
      models/
```

Unity 只调用统一 TTS API，不绑定具体 TTS 项目。

## D.4 TTS 接口建议

```text
GET  /health
POST /tts
POST /tts/cancel
POST /cache/clear
```

请求示例：

```json
{
  "characterId": "toki",
  "voiceId": "toki_default",
  "text": "老师，我们继续吧。",
  "emotion": "happy",
  "speed": 1.0,
  "format": "wav"
}
```

返回示例：

```json
{
  "success": true,
  "audioPath": "VirtualPartner/UserData/TTSCache/toki/xxxx.wav",
  "duration": 2.6,
  "engine": "gpt-sovits",
  "cached": false
}
```

## D.5 TTS 与 StagePlan 的关系

StagePlan 执行 speech action 时：

```text
speech action 开始
↓
请求 TTS 或命中缓存
↓
播放音频
↓
SpeechMouthDriver 根据音频驱动嘴型
↓
音频播放完成
↓
speech action 完成
↓
如果当前 stage 所有 blocking action 完成，则进入下一 stage
```

如果 TTS 失败：

```text
speech 仍然显示文本
嘴型退回文本估算模式
stage 仍然可以继续完成
Debug 记录 TTS 错误
```

## D.6 TTS 失败降级

TTS 失败时：

- StagePlan 继续播放。
- Momotalk 气泡继续显示。
- 嘴型退回文本估算或简单开合。
- Debug 面板记录错误。

TTS 是体验增强项，不应拖垮整个 LLM 对话链路。

## D.7 嘴型驱动

有 TTS 音频：

```text
AudioSource 播放
↓
采样音量 / RMS
↓
平滑处理
↓
量化到 closed / small / mid / large
↓
MouthTextureController 切换 mouthIndex
```

无 TTS 音频：

```text
根据文本长度和估算时长做简单开合
```

第二阶段不做音素级口型。

## D.8 TTS 缓存

需要做 TTS 缓存，并支持定期清理。

建议路径：

```text
VirtualPartner/UserData/TTSCache/
  {characterId}/
    audio_xxxx.wav
    index.json
```

缓存 key 包含：

```text
characterId
voiceId
text
emotion
speed
engine
engineVersion
```

建议配置：

```text
enableTtsCache = true
maxCacheSizeMB = 1024
maxCacheAgeDays = 30
clearCacheOnStart = false
```

达到上限后按最旧访问时间清理。

## D.9 多角色语音配置

TTS 接口必须预留：

- voiceId。
- emotion。
- speed。

建议角色语音配置：

```text
CharacterVoiceProfile
  characterId
  defaultVoiceId
  ttsProvider
  defaultSpeed
  defaultEmotion
  referenceAudioPath
```

speech action 可选字段：

```json
{
  "type": "speech",
  "text": "老师，今天也来啦。",
  "voiceId": "toki_default",
  "emotion": "happy",
  "speed": 1.0
}
```

早期不要求 LLM 每次生成这些字段，默认从 CharacterVoiceProfile 读取。

---

# Part E：ASR 语音输入 V1

## E.1 功能定位

ASR 负责将用户语音输入转成文本，并接入 Momotalk 输入流程。

第二阶段 ASR 不内嵌进 Unity，而是通过本地 Python 服务实现。Unity 只负责 UI、状态展示和接口调用。

## E.2 交互模式

语音输入不是“按住说话”，而是类似 ChatGPT 的语音交流入口。

流程：

```text
点击 Momotalk 麦克风按钮
↓
进入 Voice Mode / Listening 状态
↓
ASR Python 服务开始录音 + VAD 检测
↓
用户说话
↓
检测到静音
↓
ASR 输出文本
↓
根据配置：
  A. 填入输入框，不自动发送
  B. 自动发送给 LlmRelay
```

第二阶段默认：

```text
FillInputOnly
```

即：识别结果先填入输入框，供调试和确认。

后续可切换为：

```text
AutoSendToLlm
```

## E.3 Unity 与 ASR 服务分工

Unity 负责：

- 麦克风按钮 UI。
- Voice Mode 状态展示。
- 调用 ASR 服务 start / cancel / status。
- 接收识别文本。
- 填入输入框或自动发送。

Python ASR 服务负责：

- 麦克风录音。
- VAD 检测。
- 静音判断。
- ASR 模型推理。
- 返回识别文本。

## E.4 ASR 服务形态

建议目录：

```text
VirtualPartner/
  LocalServices/
    ASR/
      asr_service.py
      config.json
      requirements.txt
      models/
```

Unity 运行时自动启动服务：

```text
AsrServiceManager
  autoStartOnPlay = true
  serviceUrl = http://127.0.0.1:xxxx
  healthCheck = /health
  startTimeout
  stopOnExit
```

## E.5 接口建议

建议使用任务式接口，避免 HTTP 长时间阻塞。

```text
GET  /health
POST /voice/start
POST /voice/cancel
GET  /voice/status?sessionId=xxx
```

`/voice/start` 返回：

```json
{
  "success": true,
  "sessionId": "asr_001"
}
```

`/voice/status` 返回：

```json
{
  "success": true,
  "status": "done",
  "text": "我们继续讨论第二版的语音输入。",
  "duration": 3.6,
  "engine": "sherpa-onnx"
}
```

状态建议：

```text
idle
listening
speaking
recognizing
done
failed
canceled
```

## E.6 识别结果行为

建议枚举：

```text
VoiceRecognitionResultMode:
  FillInputOnly
  AutoSendToLlm
```

默认：`FillInputOnly`。

## E.7 技术路线候选

结合当前需求和硬件配置，建议优先级：

1. sherpa-onnx。
2. FunASR / SenseVoice。
3. faster-whisper / whisper.cpp。

当前硬件配置大致为：

```text
CPU: i7 9代 2.6GHz
GPU: GTX 1650
```

因此第二阶段 ASR 不宜优先选择过重模型。目标是：

- 中文识别够准。
- 短句延迟低。
- 本地部署成本可接受。
- 方便 Unity 通过服务调用。
- 后续可以替换 Provider。

建议第一轮优先测试 sherpa-onnx。如果中文效果或接入体验不理想，再测试 FunASR / SenseVoice。

## E.8 边界

第二阶段 ASR 不做：

- 实时流式字幕。
- 唤醒词。
- 多人说话人识别。
- 复杂语音活动 UI。
- Android 原生 ASR 适配。

Android 作为后续可能平台，第二阶段只在架构上保留 Provider 可替换能力。

---

# Part F：长期记忆 V0

## F.1 功能定位

长期记忆用于让角色记住长期有效的信息、用户偏好、项目决策和关系变化。

第二阶段先做 Markdown 记忆，不做 RAG 和向量数据库。

## F.2 记忆写入模式

采用全自动写入。

但 MemoryJudge prompt 必须严格约束：

- 不是什么都记。
- 也不能什么都不记。
- 只记录长期有效、后续有用、会影响角色对话或项目推进的信息。
- 不记录普通寒暄。
- 不记录一次性临时信息。
- 不记录不确定事实。
- 不记录敏感隐私信息，除非用户明确要求。

## F.3 写入时机

每轮有效对话结束后判断一次。

流程：

```text
用户消息
↓
角色 StagePlan 回复完成
↓
MemoryJudge 判断是否值得记忆
↓
生成 memory item
↓
MemoryValidator 校验
↓
写入对应 md 文件
```

## F.4 记忆文件分类

第二阶段不做 Global Memory，每个角色有独立记忆。

建议路径：

```text
VirtualPartner/UserData/Memory/{characterId}/
  user_profile.md
  preferences.md
  project_notes.md
  relationship_notes.md
  important_events.md
  conversation_summaries.md
```

分类说明：

- `user_profile.md`：该角色知道的用户长期稳定信息。
- `preferences.md`：该角色知道的用户偏好。
- `project_notes.md`：该角色记住的项目长期事实、开发方向、技术决策。
- `relationship_notes.md`：该角色与用户的关系变化和互动偏好。
- `important_events.md`：重要事件。
- `conversation_summaries.md`：阶段性对话摘要。

## F.5 记忆格式

使用段落式 Markdown。

示例：

```md
## 2026-05-12｜项目设计决策｜high

用户确认第二版 Momotalk UI 采用右侧滑出式 9:16 手机界面。Play 后右侧显示小手机按钮，点击后手机界面从右侧滑出，点击手机外区域关闭。

来源：Momotalk UI 需求讨论
适用场景：后续实现第二版 UI、编写需求文档、和 agent 对接时参考。
```

## F.6 重要性等级

记忆带 importance：

```text
low
medium
high
core
```

含义建议：

- `core`：长期稳定、几乎总是需要遵守的偏好或核心设定。
- `high`：重要项目决策、关键偏好。
- `medium`：一般有用信息。
- `low`：可能有用但不稳定或优先级较低的信息。

## F.7 Prompt 注入策略

第二阶段不做 RAG。

每轮对话前读取当前聊天角色的 md 记忆，优先注入：

```text
core
high
```

`medium / low` 默认不主动注入，或只在 Debug 中可见。

记忆作为隐式上下文使用。角色不应该频繁说“我记得”或“根据记忆”，除非用户主动询问。

后续记忆规模变大后，再考虑：

- 关键词检索。
- 摘要压缩。
- embedding。
- 向量库。
- RAG。

## F.8 MemoryJudge

MemoryJudge 复用现有 LLM 配置，但使用独立 prompt。

输入建议：

- 本轮用户消息。
- 角色本轮 speech 文本。
- 当前 characterId。
- 当前日期时间。
- 当前角色已有 core / high 记忆摘要。

输出结构示例：

```json
{
  "shouldRemember": true,
  "category": "project_notes",
  "importance": "high",
  "title": "Momotalk UI 入口形态",
  "memoryText": "用户确认第二版 Momotalk UI 采用右侧滑出式 9:16 手机界面。",
  "reason": "这是第二版 UI 的长期设计决策。"
}
```

## F.9 Memory Debug Panel

需要提供 Debug 面板。

功能：

- 显示 loaded memories。
- 显示 latest memory decision。
- 显示本轮 MemoryJudge 结果。
- 显示写入成功 / 失败。
- Reload Memory。
- Open Memory Folder。
- Clear Pending / Latest Decision。

第二阶段不强制做正式 UI 的记忆管理。md 文件本身可以手动编辑。

## F.10 记忆与聊天记录边界

聊天记录和长期记忆必须严格分开：

```text
ChatHistory JSON = 历史流水账
Memory MD = 筛选后的长期有效信息
```

MemoryJudge 可以读取本轮对话内容来判断是否记录，但聊天记录本身不等于记忆。

---

# Part G：多角色架构预留

## G.1 功能定位

第二阶段只做单角色运行、多角色结构预留。

当前主要角色仍然是 Toki，但系统不应继续把 Toki 硬编码在 Momotalk、Voice、Memory、Prompt、Expression 等流程中。

第二阶段不做：

- 多角色同时在场完整调度。
- 角色之间对话。
- 群聊。
- 多角色调度器。

## G.2 CharacterProfile

CharacterProfile 是角色静态配置资产。

暂定字段：

```text
characterId
displayName
avatarIcon
momotalkStatus
boneMapProfile
faceBoneMapProfile
animationProfile
expressionProfile
mouthPoseProfile
voiceProfile
promptProfile
memoryDirectory
chatHistoryPath
```

字段可根据 agent 实际开发需要增删。

## G.3 CharacterRuntimeContext

CharacterRuntimeContext 是角色运行时实例状态。

建议包含：

```text
characterId
characterProfile
runtimeRoot
Timeline / StagePlan Player
ActionCoordinator
AvatarPoseApplier
RootOrientationController
LocomotionActionExecutor
FSM Scheduler
MouthTextureController
ExpressionActionExecutor
SpeechBubbleView
currentExpression
currentStagePlanState
interactionState
```

## G.4 CharacterRuntimeBinder 自动绑定

通过角色根物体上的 CharacterRuntimeBinder 自动绑定。

```text
CharacterRuntimeBinder
  characterId = toki
  characterProfile = TokiProfile
```

Binder 只在自己角色根节点下收集组件，不做全场景乱搜。

收集组件：

```text
StagePlanPlayer
ActionCoordinator
AvatarPoseApplier
RootOrientationController
LocomotionActionExecutor
MouthTextureController
ExpressionActionExecutor
SpeechBubbleView
FSM Scheduler
```

启动后注册到 CharacterRegistry，生成 CharacterRuntimeContext。

## G.5 Momotalk 联系人来源

Momotalk 只显示当前场景中已注册 RuntimeContext 的角色。

规则：

```text
有 CharacterProfile + 有 RuntimeContext
→ 显示联系人

只有 CharacterProfile，但角色不在场景中
→ 不显示联系人
```

## G.6 LLMRelay 与角色分发

LlmRelay 仍然是公共请求服务，不为每个角色单独创建一套 LlmRelay。

当前聊天对象由 Momotalk 决定。

推荐流程：

```text
点击 Toki 联系人
↓
targetCharacterId = toki
↓
PromptAssembler 使用 Toki 的 Profile + Prompt + Memory
↓
LlmRelay 请求 LLM
↓
LLM 返回 raw JSON
↓
StagePlanParser / StagePlanValidator
↓
StagePlanDispatcher.Dispatch(toki, validatedStagePlan)
↓
TokiRuntimeContext.StagePlanPlayer 播放
```

LLM 不负责决定 JSON 发给哪个角色。targetCharacterId 由 Momotalk / ConversationController 决定。

## G.7 全局服务与每角色实例

全局服务：

```text
LlmRelay
AsrServiceManager
TtsServiceManager
MomotalkUIManager
MemorySystem
CharacterRegistry
StagePlanDispatcher
```

每角色实例：

```text
StagePlanPlayer
ActionCoordinator
AvatarPoseApplier
RootOrientationController
LocomotionActionExecutor
FSM Scheduler
MouthTextureController
ExpressionActionExecutor
SpeechBubbleView
CharacterRuntimeContext
```

---

# Part H：Prompt 组装与规则

## H.1 核心原则

第二阶段 Prompt 默认要求 LLM 输出 StagePlan 2.0 JSON。

LLM 仍然只生成 Runtime 支持的结构化 JSON，不输出普通聊天文本 + JSON 混合体。

规则：

- 不生成未声明 action。
- 不生成未登记 expression / animation / bone / voiceId。
- 不直接控制 Unity 对象。
- 不接触真实 Transform path。
- 不越过 StagePlanValidator。
- 不需要强行动作，根据用户意图生成。
- 普通聊天只需要 speech。
- 需要情绪时加 expression。
- 需要身体动作时再加 bonePose / animation / facing / locomotion。

## H.2 Prompt 组装

Prompt 按当前 targetCharacterId 动态组装。

```text
Global runtime rules
+ StagePlan 2.0 schema rules
+ 当前角色 CharacterPrompt
+ 当前角色可用能力
+ 当前角色 core/high memory
+ 当前用户输入
```

不做 Global Memory。和谁聊天，就读取谁的角色记忆和角色 prompt。

## H.3 Prompt 模块建议

```text
character.md
stageplan-rules.md
runtime-capabilities.md
parameter-bones.md
preset-actions.md
locomotion.md
expressions.md
voice.md
memory.md
momotalk.md
examples.md
verified-examples.md
```

## H.4 Memory 注入规则

记忆作为隐式上下文注入。

Prompt 应明确：

```text
Use memory naturally.
Do not explicitly say "I remember from memory" unless the user asks.
```

---

# Part I：ASR / TTS 本地服务统一管理

## I.1 功能定位

ASR 和 TTS 都做成本地 Python 服务，Unity 运行时自动拉起，失败时降级但不阻塞项目。

## I.2 建议结构

```text
LocalServiceManager
  AsrServiceManager
  TtsServiceManager
```

统一能力：

```text
autoStartOnPlay
healthCheck
startTimeout
serviceUrl
processPath
workingDirectory
logPath
stopOnExit
restartService
```

建议路径：

```text
VirtualPartner/LocalServices/ASR/
VirtualPartner/LocalServices/TTS/
VirtualPartner/UserData/Logs/LocalServices/
```

## I.3 降级原则

ASR 失败：

```text
语音按钮禁用或显示服务不可用
文本输入仍然可用
```

TTS 失败：

```text
StagePlan 继续
Momotalk 气泡继续
嘴型退回文本估算
```

本地服务不可用不应导致整个项目无法运行。

---

# Part J：Debug 面板整合

第二阶段 Debug 面板继续定位为 Runtime 调试工具，不做正式产品设置 UI。

建议新增栏目：

## J.1 Momotalk

```text
当前页面
当前聊天角色
未读数
最后一条消息
清空聊天记录
打开聊天记录目录
```

## J.2 Plan / StagePlan

```text
当前格式：StagePlan 2.0
当前 stage index
当前 stageId
当前 running actions
当前 blocking actions
raw JSON
validation result
```

## J.3 ASR

```text
服务状态
当前 session
listening / speaking / recognizing / done
最新识别文本
FillInputOnly / AutoSendToLlm
```

## J.4 TTS

```text
服务状态
当前 engine
当前 voiceId
cache 命中
当前播放音频
清理缓存
```

## J.5 Expression / Mouth

```text
当前 expression
当前 mouthIndex
手动测试 mouthIndex
手动播放 neutral / smile / thinking / surprised / embarrassed
SpeechMouthDriver 状态
Audio RMS 数值
```

## J.6 Memory

```text
loaded memories
latest memory decision
MemoryJudge raw result
Reload
Open Memory Folder
```

## J.7 Character

```text
当前注册角色
当前 targetCharacterId
RuntimeContext 状态
CharacterProfile 信息
```

---

# Part K：第二阶段开发顺序

最终确认开发顺序：

## Phase 2.1：Momotalk UI 最小闭环

目标：正式 UI 可以替代 Debug 面板进行文本对话。

任务：

- 右侧小手机按钮。
- 手机 UI 右侧滑出。
- Loading。
- 联系人列表。
- Toki 聊天页。
- 输入框发送。
- LlmRelay 接入。
- StagePlan speech 同步气泡。
- typing indicator。
- 聊天记录 JSON。
- 未读红点。

验收：

```text
用户可以通过 Momotalk UI 和 Toki 文本聊天。
角色回复气泡按 StagePlan speech 顺序出现。
角色场景演出仍走 Runtime 链路。
```

## Phase 2.2：StagePlan JSON 2.0 / 阶段式执行模型

目标：用阶段式执行模型替代第一阶段绝对时间轴 timeline，作为第二阶段主 JSON 格式。

任务：

- StagePlanData。
- StagePlanParser。
- StagePlanValidator。
- StagePlanPlayer。
- StageActionExecutor。
- 每 stage 最多一个 speech。
- stage 顺序执行。
- stage 内 action 并行执行。
- 等 blocking action 完成后进入下一 stage。

验收：

```text
LLM 返回 StagePlan 2.0 后，Runtime 能按阶段顺序执行。
speech 不会因为固定时间轴导致错位。
```

## Phase 2.3：嘴型 / 基础表情系统 V1

目标：Toki 说话时嘴巴贴图随声音或文本开合，并支持基础表情 action。

任务：

- MouthTextureController。
- MouthPoseProfile。
- ExpressionProfile。
- ExpressionActionExecutor。
- FaceBoneMapProfile。
- SpeechMouthDriver。
- Expression Debug。

验收：

```text
Toki 说话时嘴型有反馈。
LLM 可以通过 expression action 控制基础表情。
表情结束后回 IdleFace。
```

## Phase 2.4：TTS / 语音克隆输出 V1

目标：StagePlan speech 可以触发 TTS 生成语音并播放。

任务：

- TTS Python 服务。
- GPT-SoVITS 优先测试。
- Unity TtsServiceClient。
- AudioSource 播放。
- TTS 缓存。
- voiceId / emotion / speed 预留。
- SpeechMouthDriver 接入音频驱动。

验收：

```text
Toki 的 speech 可以合成为角色语音播放。
TTS 音频播放完成后，speech action 才完成。
TTS 失败时对话和 StagePlan 仍可继续。
```

## Phase 2.5：ASR 语音输入 V1

目标：点击麦克风进入语音输入模式，ASR 识别文本填入输入框或自动发送。

任务：

- ASR Python 服务。
- Unity AsrServiceManager。
- Voice Mode UI。
- VAD 自动结束本轮识别。
- FillInputOnly / AutoSendToLlm 配置。

验收：

```text
点击麦克风后说一句话，静音后自动识别为文字。
文字可以填入 Momotalk 输入框，也可以配置为自动发送。
```

## Phase 2.6：长期记忆 V0

目标：角色自动记录长期有效信息，并在后续对话中注入高优先级记忆。

任务：

- MemoryJudge prompt。
- MemoryValidator。
- Markdown MemoryStore。
- core/high 注入。
- Memory Debug Panel。

验收：

```text
系统可以自动记录重要项目决策 / 用户偏好。
后续对话能读取并注入当前角色 core/high 记忆。
```

## Phase 2.7：多角色架构预留

目标：减少 Toki 硬编码，为后续多角色扩展做结构准备。

任务：

- CharacterProfile。
- CharacterRuntimeContext。
- CharacterRuntimeBinder。
- CharacterVoiceProfile。
- CharacterMemoryStore。
- CharacterPromptProfile。
- Momotalk 联系人列表从 RuntimeContext 生成。
- StagePlanDispatcher 按 targetCharacterId 分发。

验收：

```text
当前仍主要是 Toki，但新增角色不需要重写 Momotalk / Voice / Memory / Prompt 主流程。
只有场景中已绑定 RuntimeContext 的角色才显示在 Momotalk。
```

## Phase 2.8：Debug 面板整合与收尾

目标：将第二阶段新增系统纳入统一 Runtime Debug 面板。

任务：

- Momotalk Debug。
- StagePlan Debug。
- ASR Debug。
- TTS Debug。
- Expression / Mouth Debug。
- Memory Debug。
- Character Debug。

验收：

```text
开发者可以在统一 Debug 面板查看第二阶段所有关键运行状态。
```

---

# Part L：第二阶段最终验收标准

第二阶段最终验收按以下标准执行：

1. Play 后右侧出现 Momotalk 按钮。
2. 打开手机 UI，可以进入 Toki 聊天页。
3. 用户输入文本后，LLM 返回 StagePlan 2.0。
4. StagePlan 按阶段执行，speech 不错位。
5. Momotalk 气泡按 stage / speech 顺序出现。
6. Toki 可以切换基础表情。
7. Toki 说话时嘴巴贴图随语音或文本开合。
8. TTS 可以用 Toki voiceId 合成并播放语音。
9. ASR 可以识别用户语音，填入输入框或自动发送。
10. Toki 可以自动记录长期记忆到 md。
11. 重新对话时能注入该角色 core/high 记忆。
12. 当前场景中已绑定角色会出现在 Momotalk 联系人列表。
13. Debug 面板能查看 Momotalk / StagePlan / ASR / TTS / Memory / Character 状态。

---

# Part M：第三阶段及以后候选方向

以下内容建议留到第三阶段或后续：

1. 多角色同时在场完整调度。
2. 角色之间对话。
3. 家具 / 道具交互。
4. NavMesh 路径规划。
5. 复杂避障。
6. RAG 记忆库。
7. Gesture / 手指精细动作库。
8. 复杂情绪人格系统。
9. 更完整的 Toki 专属音色训练与音色管理。
10. Android 平台适配。

---

# 结论

第二阶段最终主线是：

```text
Momotalk UI
↓
StagePlan 2.0
↓
嘴型 / 基础表情
↓
TTS 语音克隆输出
↓
ASR 语音输入
↓
Markdown 长期记忆
↓
多角色架构预留
↓
Debug 整合
```

第二阶段的核心目标是：

> 将 Toki 从第一阶段的“LLM 控制角色演出闭环”推进到“可以通过正式 Momotalk UI 进行文本与语音交流、具备角色语音、嘴型、基础表情、长期记忆和多角色扩展基础”的可体验版本。

