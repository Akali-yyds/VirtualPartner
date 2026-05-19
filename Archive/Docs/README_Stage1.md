# VirtualPartner 当前阶段 README

更新时间：2026-05-12

本文档用于存档当前开发进度，并给后续继续开发时快速恢复上下文。更细的长期方向见 `virtual_partner开发方向文档.md`，阶段任务和验收记录见 `DevelopmentTODO.md`，开发协作原则以 `ReadFirst.md` 为准。

## 项目定位

VirtualPartner 第一版目标是做一个 Unity 内的虚拟伙伴 Runtime 原型：固定角色 Toki / CH0187，在场景中通过本地 JSON timeline 或 LLM 返回的 JSON timeline 驱动文字、骨骼姿态、预设动画、转向和位移。

当前第一版主线已经完成阶段 0-10，并通过手动验收。项目重点仍是简单、高效、直接：先把可控链路跑通，再逐步打磨表现。

## 当前状态

- Unity 工程目录：`VirtualPartner/`
- 主场景：`VirtualPartner/Assets/Scenes/SampleScene.unity`
- 固定角色：Toki / CH0187
- 主模型入口：`VirtualPartner/Assets/Character/Toki/Prefabs/SourceParts/CH0187_Mesh.prefab`
- Runtime 主入口：`VirtualPartner/Assets/VirtualPartner/Runtime/VirtualPartnerStage1Bootstrap.cs`
- 当前已验收阶段：0-10

已完成能力：

- Play 后加载角色并采集 BaseRotation。
- Idle 姿态作为最低层姿态来源。
- BoneMap 语义骨骼控制与 Debug 单骨骼调试。
- ActionCoordinator 统一骨骼 owner、抢占、释放和过渡。
- 本地 JSON TimelinePlayer。
- speech / bonePose / animation / facing / locomotion 五类 timeline action。
- 预设动画白名单。
- Root 转向与 walk / run 位移。
- MovementConstraint 活动区域和障碍限制。
- FSM 自主空闲行为。
- LLM Relay 最小闭环。
- 模块化 LLM Prompt。
- Eye 成对骨骼控制。
- 统一 Runtime Debug 面板。

当前明确不包含：

- TTS。
- 正式产品 UI。
- 对话历史和长期记忆。
- NavMesh / 寻路 / 复杂避障。
- 新 timeline action 类型。
- LLM 直接控制真实骨骼名、衣服、头饰、手指、武器、材质、表情或嘴型。

## 目录说明

```text
VirtualPartner/
  Assets/
    Scenes/
      SampleScene.unity
    Character/Toki/
      Prefabs/SourceParts/CH0187_Mesh.prefab
      Animations/
    VirtualPartner/
      Runtime/
      Profiles/
      Prompts/
      Timelines/
      Editor/
  UserSettings/
    VirtualPartnerLlmConfig.json
```

关键目录：

- `Runtime/`：核心运行时代码。
- `Profiles/`：BoneMap、预设动画、locomotion、FSM 配置资产。
- `Prompts/`：LLM prompt 模块化文档。
- `Timelines/`：本地 sample timeline。
- `Editor/`：Profile 编辑辅助。
- `UserSettings/`：本地私有配置目录，被 `.gitignore` 忽略，不提交真实 API key。

## Runtime 主要系统

### Bootstrap

`VirtualPartnerStage1Bootstrap` 是当前场景的统一接线入口。Play 后大致流程：

1. 校验场景引用。
2. 配置 Idle、AvatarPoseApplier、ActionCoordinator、Root、Locomotion、TimelinePlayer、FSM、LlmRelay。
3. 捕获 BaseRotation。
4. 播放 Idle。
5. 启动 FSM。

退出 Play 或组件 Disable 时，会停止 LLM pending request、FSM、timeline、debug owner、locomotion 和 Idle。

### Idle 与 BaseRotation

- `IdleBaseProvider` 负责 Idle clip 播放时间。
- `AvatarPoseApplier` 负责采集 BaseRotation，并每帧应用 Idle 采样姿态。
- BaseRotation 是所有参数骨骼姿态的相对零点。

### BoneMap 与 ActionCoordinator

- `BoneMapProfile` 维护语义骨骼到真实骨骼的映射、side、轴范围、镜像规则。
- `ActionCoordinator` 是所有骨骼写入的统一 owner 管理器。
- 当前 owner 类型包括 Idle、Debug、TimelineBonePose、PresetAnimation、Locomotion。
- owner 切换时通过统一过渡回到目标姿态，避免多个系统直接抢写 Transform。

Eye 当前已经作为成对控制能力接入：对外是单个 `Eye` 语义骨骼，底层同时控制左右眼骨骼。

### Timeline

`TimelinePlayer` 接收 JSON timeline，先走 `TimelineValidator`，校验通过后执行。

当前支持 action：

- `speech`：显示文字气泡。
- `bonePose`：参数化控制语义骨骼。
- `animation`：调用白名单预设动画。
- `facing`：Root 转向。
- `locomotion`：walk / run 沿角色当前 forward 位移。

timeline 所有权：

- FSM 使用 owner id：`FSM`。
- LLM 使用 owner id：`LLM`。
- 原有 `PlayJson / ReplaceJson / StopTimeline` 视为外部调试或外部 timeline。
- FSM 不抢占外部 timeline 或 LLM timeline。
- LLM 校验通过后可替换当前非 FSM timeline。

### 预设动画

配置资产：`VirtualPartner/Assets/VirtualPartner/Profiles/Toki_PresetAnimationProfile.asset`

预设动画通过白名单名称调用。LLM prompt 中会动态注入：

- animation name
- clip.length duration
- fullBody / partial 推断
- prompt 文档中的用途说明

未登记动画不会播放，只记录 warning。

### Root / Locomotion / MovementConstraint

- `RootOrientationController`：处理 facing、交互 TurnToCamera、FSM 世界 yaw 转向。
- `LocomotionActionExecutor`：处理 walk / run，按配置速度和 duration 推进 root。
- `MovementConstraintController`：检测 RoomMoveArea 和 ObstacleArea。

locomotion 规则：

- timeline 中不包含 steps / direction。
- walk / run 永远沿角色当前 forward 移动。
- 需要换方向时，先 facing，再 locomotion。
- 触碰活动边界或障碍时停止在上一合法位置，不回退、不寻路、不重试。

### FSM 自主行为

配置资产：`VirtualPartner/Assets/VirtualPartner/Profiles/Toki_FSMProfile.asset`

当前 FSM 行为：

- 非用户交互、没有外部 timeline 时开始调度。
- 每轮等待时间随机抽取，默认 10-20 秒。
- 按权重抽取动作。
- 支持 `PresetAnimation` 和 `Locomotion`。
- FSM locomotion 随机转向使用世界 Yaw 0-360 的最小实现。
- 动作结束或失败后释放动作，重新进入等待流程，角色姿态自然回到 Idle。

FSM timeline owner 规则：

- FSM 只停止自己启动的 timeline。
- 外部 timeline 播放中，FSM 暂停调度。
- 用户交互中，FSM 停止当前自身行为，不恢复旧动作。

### LLM Relay

`LlmRelay` 实现用户输入到 LLM timeline 的最小闭环。

本地配置路径：

```text
VirtualPartner/UserSettings/VirtualPartnerLlmConfig.json
```

主要字段：

- `apiKey`
- `model`
- `chatCompletionsUrl`
- `baseUrl`
- `useJsonResponseFormat`
- `supportsDeveloperRole`
- `interactionTimeoutSeconds`

规则：

- 使用 Chat Completions 兼容接口。
- 每次请求只发送当前用户消息，不发送历史。
- 默认要求 JSON object response format。
- API key 不在 Console、面板或 request 日志中显示。
- 请求使用 latest-wins：连续提交时只接受最新 request。
- LLM timeline 播放结束后不会立刻恢复 FSM，而是进入 10 秒交互空闲计时。

### Prompt 模块

Prompt 文档目录：

```text
VirtualPartner/Assets/VirtualPartner/Prompts/
```

当前模块：

- `character.md`：角色人设，占位文档。
- `timeline-rules.md`：JSON schema、action 类型、结构规则。
- `parameter-bones.md`：参数动作骨骼说明。
- `preset-actions.md`：预设动作说明。
- `locomotion.md`：位移和 facing 语义规则。
- `examples.md`：短 timeline 示例。

最终 prompt 由固定系统约束、这些文档内容、Profile 动态能力列表共同组装。Debug 面板里可以 Copy Prompt。

## 调试入口

Play 后默认显示统一面板：

```text
VirtualPartner Runtime Debug
```

栏目：

- `Overview`：LLM、Timeline、FSM、Root、Bone 关键状态汇总。
- `LLM`：输入文本、Submit、Reload Config、Stop LLM Timeline、Copy Prompt。
- `Timeline`：JSON 编辑、Load Sample、Paste、Validate、Play、Replace、Stop。
- `FSM`：Enable / Disable、Enter Interaction、Exit Interaction。
- `Root`：位置、Yaw、Turn、Locomotion、Constraint 状态。
- `Bone`：BoneMap 列表、Apply Debug Overlay、rotation slider、Zero、Export JSON、Refresh UI。

旧分散面板默认隐藏，但未删除：

- `TimelineDebugPanel`
- `LlmInteractionDebugPanel`
- `AutonomousBehaviorDebugPanel`
- `RootLocomotionDebugPanel`
- `VirtualPartnerBoneDebugPanel`

需要回退时，可以在 Inspector 中手动恢复对应旧面板的 `standaloneVisible`。

Bone 统一页的 `Refresh UI` 只刷新显示状态，不重采 BaseRotation，不自动搜索骨骼，不修改 `BoneMapProfile`。

## 本地 Timeline 测试

Sample timeline：

- `VirtualPartner/Assets/VirtualPartner/Timelines/Stage4_SampleTimeline.json`
- `VirtualPartner/Assets/VirtualPartner/Timelines/Stage5_SampleTimeline.json`
- `VirtualPartner/Assets/VirtualPartner/Timelines/Stage6_SampleTimeline.json`

手动测试：

1. 打开 `SampleScene`。
2. 进入 Play。
3. 在统一 Debug 面板切到 `Timeline`。
4. 点击 `Load Sample`。
5. 点击 `Validate`。
6. 点击 `Play` 或 `Replace`。
7. 观察 speech、bonePose、animation、facing、locomotion 是否按预期执行。

## LLM 最小闭环测试

前置：

- 在 `VirtualPartner/UserSettings/VirtualPartnerLlmConfig.json` 填好本地 API 配置。
- 不要提交真实 API key。

流程：

1. 进入 Play。
2. 统一面板切到 `LLM`。
3. 点击 `Reload Config`，确认 config ready。
4. 输入例如“打个招呼”。
5. 点击 `Submit`。
6. 观察 LLM raw response、extracted timeline、Timeline owner 和角色演出。
7. timeline 播完后，确认角色仍保持用户交互状态，约 10 秒无新输入后 FSM 恢复等待。

方向语义约定：

- “往前 / 走过来 / 靠近我 / 朝我走”默认表示朝用户或摄像机方向，优先使用 `facing: camera` 后接 walk。
- `screenForward` 表示往画面深处、通常是远离用户。
- `run` 只在用户明确要求快跑、冲刺或紧急移动时使用。

## 当前验收记录

阶段 0-10 已完成并通过手动验收。

阶段概览：

- 阶段 0：项目开发约定与验收流程。
- 阶段 1：角色初始化与 Idle。
- 阶段 2：BoneMap 与 Debug 单骨骼控制。
- 阶段 3：ActionCoordinator 与骨骼交接过渡。
- 阶段 4：本地 JSON TimelinePlayer。
- 阶段 5：预设动画接入。
- 阶段 6：Root 转向与 Locomotion。
- 阶段 7：MovementConstraint。
- 阶段 8：FSM 自主行为。
- 阶段 9：LlmRelay 接入与 Prompt 模块化。
- 阶段 10：统一 Debug 面板与第一版打磨。

## 后续开发注意

- 每个新阶段开始前先讨论目标、边界和验收方式。
- 明确后再开发，不提前实现后续阶段内容。
- 优先复用现有 Runtime API，不复制第二套逻辑。
- 继续保持 timeline schema 稳定，除非明确进入扩展 action 类型阶段。
- 涉及 LLM 输出质量时，优先调 prompt 和动态能力注入，再考虑 Runtime 扩展。
- 涉及移动自然度时，当前 FSM 随机转向只是世界 Yaw 0-360 的最小实现，后续可再讨论更自然的 wander 策略。
- 涉及武器、表情、嘴型、材质、服装等表现时，需要单独立阶段讨论，不要混入已有 Runtime 调试改动。

