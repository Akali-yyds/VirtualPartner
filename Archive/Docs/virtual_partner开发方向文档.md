# VirtualPartner / Toki LLM 驱动角色项目开发方向文档 v1.1 定稿

> 文档状态：第一版开发方向定稿  
> 项目阶段：准备进入开发  
> 目标角色：Toki / CH0187  
> 文档用途：约束整体开发方向、模块边界、核心规则与禁止事项。本文档不是代码级详细设计，不要求一次性把所有字段、类和接口写死。具体实现细节应在开发到对应模块时继续细化。

---

## 1. 文档定位

本项目不再继续停留在纯讨论阶段。本文档的目的不是把所有实现细节提前讨论完，而是明确第一版的大体开发方向，避免后续开发时架构跑偏、模块职责混乱、过渡逻辑重复、Runtime 兜底过多。

后续开发方式应采用：

```text
拆分模块
↓
开发一个模块
↓
调试该模块
↓
确认稳定
↓
进入下一个模块
```

每个模块开发时，可以再单独讨论该模块的具体类、字段、接口和调试方式。本文档只负责保证第一版总体方向清楚。

核心开发原则：

```text
简单、高效
高内聚、低耦合
Runtime 严格执行明确指令
不写隐藏补丁
不重复实现兜底逻辑
每个模块只做自己的事
```

---

## 2. 项目目标

项目目标是在 Unity 中实现一个由 LLM 驱动的虚拟角色交互系统。

角色在没有用户交互时，由 Idle 和 FSM 自主行为接管；用户提交文本后，请求通过 LlmRelay 发送给 LLM，LLM 返回统一格式的 JSON timeline，Unity Runtime 解析并执行该 timeline，驱动角色完成文字气泡、骨骼动作、预设动画、Root 转向和位移动作。

第一版目标是最小闭环：

```text
用户文本输入
↓
LlmRelay 请求 LLM
↓
LLM 返回 JSON timeline
↓
Unity 播放 timeline
↓
角色显示文字气泡并完成动作演出
↓
演出结束回到 Idle / FSM
```

第一版不追求完整陪伴系统，不追求复杂 UI、不追求完整表情、不追求语音输入和 TTS，先把 Runtime 动作链路、骨骼控制、过渡和调试工具跑通。

---

## 3. 第一版范围

### 3.1 第一版必须实现

1. 固定角色 Toki。
2. 使用现有 Toki 角色资源目录。
3. 文本输入。
4. LlmRelay 调用 LLM。
5. LLM 返回 JSON timeline。
6. Unity 校验并播放 JSON timeline。
7. 文字气泡显示。
8. 用户交互入口自动 TurnToCamera。
9. LLM 参数骨骼动作 `bonePose`。
10. 预设动画 `animation`。
11. Root 转向动作 `facing`。
12. walk / run 位移动作 `locomotion`。
13. Idle 常驻。
14. FSM 空闲自主行为。
15. MovementConstraint 活动范围 / 障碍停止。
16. ActionCoordinator 骨骼所有权管理。
17. 骨骼交接过渡。
18. Debug 单骨骼调试面板。
19. Play 后自动采集 BaseRotation。

### 3.2 第一版暂不实现

1. 不做正式多角色切换，只预留工程结构。
2. 不做语音输入，只预留接口。
3. 不做 TTS。
4. 不做完整 Momotalk 手机 UI。
5. 不做道具交互。
6. 不做复杂场景物体交互。
7. 不做长期情绪系统。
8. 不做长期记忆系统。
9. 不做复杂表情系统。
10. 不做精细 LookAt。
11. 不做眼睛注视。
12. 不做 NavMesh、复杂避障和路径规划。
13. 不做多 timeline 队列。

---

## 4. 当前角色与资源基础

第一版固定角色：

```text
Role: Toki
Character Id: CH0187
主模型入口: Toki/Prefabs/SourceParts/CH0187_Mesh.prefab
```

第一版主要使用：

```text
Prefabs/SourceParts/CH0187_Mesh.prefab
Animations/
Materials/
Textures/
Runtime/
```

Toki 包含身体骨骼、头发、衣服、裙摆、武器、Halo、Machine 等附属骨骼。第一版重点开放身体主骨骼给 LLM 参数动作；头发、衣服、裙摆、武器、Machine 等主要由 Idle / 预设动画管理。

Halo 可以作为角色整体骨骼系统的一部分，但第一版不让 LLM 主动控制 Halo，只让它跟随角色。

---

## 5. 核心架构

第一版 Runtime 的核心不是 FSM，也不是 LLM，而是统一的动作与骨骼协调链路。

总体链路：

```text
用户输入
↓
LlmRelay
↓
LLM 返回 JSON timeline
↓
TimelineValidator 校验
↓
TimelinePlayer 按时间提交 action
↓
ActionCoordinator 管理骨骼所有权与过渡
↓
AvatarPoseApplier 写入最终骨骼姿态
```

Root 朝向和位移不属于普通骨骼控制，由独立模块管理：

```text
RootOrientationController：负责 Root Y 轴转向
LocomotionActionExecutor：负责 Root 位移 + walk/run 动画采样
MovementConstraintController：负责活动范围和障碍检测
```

---

## 6. 模块职责边界

### 6.1 LlmRelay

负责请求 LLM，并向 LLM 注入当前角色能力说明。它不执行动作、不控制骨骼、不处理过渡。

### 6.2 TimelineValidator

负责校验 LLM 返回的 JSON timeline。它可以拒绝非法 action 或记录局部失败，但不补时间、不补 facing、不顺延后续动作。

### 6.3 TimelinePlayer

负责按 timeline 的 start / end 提交 action 请求。它不决定骨骼归属，不写 Transform。

### 6.4 ActionCoordinator

运行时骨骼控制核心。负责骨骼 owner、优先级抢占、动作打断、骨骼交接过渡和最终姿态输出。

它不调用 LLM，不管 UI，不管 FSM 随机策略，不管 Root 转向，不管场景边界。

### 6.5 IdleBaseProvider

常驻采样 Idle 动画，提供任意时刻的 Idle 姿态。Idle 不因局部骨骼被抢占而停止。

### 6.6 AnimationPoseSampler

负责采样 AnimationClip。它只提供姿态数据，不直接写骨骼。

### 6.7 LocomotionActionExecutor

负责在指定时间段内播放 / 循环采样 walk 或 run，并按速度移动角色根节点。它不负责边界检测，不负责骨骼过渡。

### 6.8 RootOrientationController

负责 Root Y 轴转向，包括 timeline 中的 `facing` 和进入用户交互状态时的 AutoTurnToCamera。

### 6.9 MovementConstraintController

负责检测角色根节点是否超出活动区域或进入障碍区域。非法时只发出 `RequestStopLocomotion(reason)`，不直接停止动画、不移动角色、不处理骨骼。

### 6.10 FSM / AutonomousBehaviorScheduler

负责空闲自主行为调度：等待随机时间、按权重抽动作、提交动作请求、动作结束回 Idle。FSM 不直接播放动画、不移动角色、不改骨骼、不处理过渡。

### 6.11 Debug 面板

Debug 是骨骼级控制源，不是状态。Debug 不绕过 ActionCoordinator，拖动滑块时也应通过统一协调层占用骨骼。

### 6.12 AvatarPoseApplier

唯一最终写入骨骼 Transform 的出口。

---

## 7. 状态优先级与骨骼控制权优先级

必须区分两套优先级。

### 7.1 状态优先级

```text
用户交互 > FSM > Idle
```

规则：

1. Idle 常驻。
2. 非用户交互时 FSM 可以运行。
3. 用户点击发送消息后进入用户交互状态。
4. 进入用户交互状态后停止 FSM。
5. 用户 10 秒无新消息后恢复 FSM。

### 7.2 骨骼控制权优先级

```text
Debug 骨骼控制 > LLM 参数动作 > Locomotion > PresetAnimation
```

这套优先级只处理“同一块骨骼被多个动作申请时谁能拿到控制权”。

Debug 不是状态，不会关闭 FSM，也不会关闭用户交互。Debug 只锁定自己正在调试的骨骼。

---

## 8. 骨骼系统规则

### 8.1 语义骨骼名

LLM 不使用真实骨骼名，例如 `Bip001 L UpperArm`。LLM 使用语义骨骼名：

```text
Head
Neck
Chest
UpperArm + side
Forearm + side
Hand + side
Thigh + side
Calf + side
Foot + side
Toe + side
```

带左右的骨骼使用：

```json
{
  "bone": "UpperArm",
  "side": "R"
}
```

### 8.2 左右镜像

同一组语义参数用于左侧和右侧时，视觉上应表现为镜像，而不是平行同向。左右差异由 BoneMap 中的 mirror 处理，LLM 不需要知道真实轴向差异。

### 8.3 方向左右与身体左右

`facing / locomotion` 的左、右、前、后默认按镜头视角解释。

身体部位的左、右永远按角色自身解释。

例如：

```text
往左走 = 往屏幕左侧走
举左手 = 举角色自己的左手
```

### 8.4 BasePose 与 BaseRotation

Play 前角色在场景里的初始姿态定义为 BasePose。

Play 后、Idle / FSM / Runtime 动作开始前，系统自动采集已登记骨骼的 localRotation，作为 BaseRotation。

```text
语义 0,0,0 = BaseRotation
语义 rotation = 相对 BaseRotation 的偏移
```

Debug 面板不允许手动覆盖 BaseRotation，避免把非标准调试姿态误保存为全 0 基准。

### 8.5 localRotation

第一版骨骼旋转统一使用 localRotation。LLM 可以输出欧拉角，但 Runtime 内部插值应使用 Quaternion。

### 8.6 骨骼安全限制

每个开放给 LLM 的语义骨骼都要配置可操作轴和角度范围。不可操作轴不暴露给 LLM。

越界时裁剪到合法范围，并记录警告日志，不直接拒绝整个动作。

### 8.7 眼睛

眼睛是特殊成对控制组。后续如果开放，只开放 `Eyes`，不开放单独左眼 / 右眼。第一版不做精细眼睛 LookAt。

---

## 9. Idle 规则

Idle 是最低层常驻姿态来源。

角色启动后，Idle 默认接管全身骨骼，包括身体、头发、衣服、裙摆、武器、Halo、Machine 等。

当某些骨骼被 LLM / FSM / Locomotion / Debug 抢占时：

```text
被抢占骨骼执行抢占动作
未被抢占骨骼继续播放 Idle
Idle 后台仍继续采样全身姿态
```

骨骼交接回 Idle 时，目标不是 Idle 起始帧，也不是释放瞬间记录的静态姿态，而是每帧动态采样的 `IdlePose(now)`。

---

## 10. JSON Timeline 规则

LLM 返回 JSON timeline。

基本结构：

```json
{
  "schemaVersion": "1.0",
  "timeline": [
    {
      "start": 0.0,
      "end": 1.5,
      "actions": []
    }
  ]
}
```

硬规则：

1. 所有 timeline 段必须有 `start / end`。
2. Runtime 不补 `end`。
3. Runtime 不顺延后续动作。
4. Runtime 不自动补 `facing`。
5. 非法 action 局部失败，合法 action 继续执行。

第一版支持五种 action：

```text
speech
bonePose
animation
facing
locomotion
```

### 10.1 speech

文字气泡显示。显示时间等于所在 timeline 段时间。

### 10.2 bonePose

LLM 直接给出骨骼目标姿态。

`rotation` 永远是目标值，不是增量值。

```json
{
  "type": "bonePose",
  "bones": [
    {
      "bone": "Head",
      "rotation": { "x": 0, "y": 20, "z": 0 }
    }
  ]
}
```

### 10.3 animation

调用白名单预设动画。

```json
{
  "type": "animation",
  "name": "Smile_Slight"
}
```

没有配置的动画不播放。

### 10.4 facing

Root Y 轴转向动作。

```json
{
  "start": 0.0,
  "end": 0.3,
  "actions": [
    {
      "type": "facing",
      "target": "screenLeft"
    }
  ]
}
```

规则：

1. `facing` 必须独占一个 timeline 段。
2. `facing` 段中不能混入 speech / bonePose / animation / locomotion。
3. 没有 `facing` = 保持当前朝向。
4. 没有 `keep`。
5. 如果用户明确要求转向，LLM 必须生成 `facing` 段。

支持 target：

```text
camera
screenLeft
screenRight
screenForward
screenBackward
```

### 10.5 locomotion

位移动作。

```json
{
  "start": 0.3,
  "end": 1.8,
  "actions": [
    {
      "type": "locomotion",
      "mode": "walk"
    }
  ]
}
```

规则：

1. 不包含 steps。
2. 不包含 direction。
3. 持续时间 = end - start。
4. 沿角色当前 forward 移动。
5. 如果需要改变方向，必须先通过 `facing` 转向。
6. 不要求持续时间是动画周期整数倍。
7. 支持 `walk / run`。

---

## 11. Root 朝向与位移

### 11.1 AutoTurnToCamera

从 FSM / Idle 进入用户交互状态时，本地自动执行一次 TurnToCamera。

已确认规则：

1. 只在从非交互状态进入用户交互状态时执行一次。
2. 同一用户交互状态内连续发消息，不再自动转向镜头。
3. 先停止 FSM，再 AutoTurnToCamera。
4. AutoTurnToCamera 期间 Idle 继续播放。
5. AutoTurnToCamera 只旋转 Root Y 轴，不控制骨骼。
6. AutoTurnToCamera 使用固定耗时，默认可设为 0.3 秒，Inspector 可调。

### 11.2 没有 facing 就保持当前朝向

Runtime 不给 timeline 自动补 facing。角色朝向是持续状态。

例如用户先让角色转向右边，下一句只说“举手”，LLM 不需要生成 facing，角色保持面向右边举手。

### 11.3 locomotion 沿当前 forward 移动

locomotion 不含 direction。角色当前朝向哪里，walk/run 就往哪里移动。

如果用户说“向左走走，然后看着我”，LLM 应编排：

```text
facing screenLeft
locomotion walk
facing camera
```

---

## 12. 动作生命周期与抢占规则

### 12.1 参数动作

参数动作最小单位是单个骨骼。某个骨骼被抢走，不影响其他骨骼继续执行。

连续 timeline 段控制同一骨骼时，不释放回 Idle，直接进入下一段目标姿态。

### 12.2 预设动画

预设动画以配置声明的骨骼集合为执行单位。

开始前有必需骨骼申请不到，整个动画不执行。

执行中关键骨骼被抢走，整个动画停止。

### 12.3 Locomotion

Locomotion 是整体动作实例，包含 Root 位移和 walk/run 动画采样。

一旦被高优先级动作抢占关键骨骼，整个位移动作停止，角色停在当前位置。

### 12.4 Debug

Debug 是骨骼级最高控制源。Debug 拖动某个骨骼时，该骨骼 owner = Debug。Debug 不绕过 ActionCoordinator。

---

## 13. 骨骼交接过渡

过渡只有一个概念：骨骼交接过渡。

只要 owner 发生变化，就创建交接过渡。

交接目标：

```text
旧动作 -> 新动作：过渡到新动作当前目标姿态
旧动作 -> Idle：过渡到 IdlePose(now)
```

交接给 Idle 时，目标姿态每帧动态采样。

LLM 不输出 transitionIn / transitionOut / transitionTime。过渡时长、曲线、目标采样都由本地统一管理。

---

## 14. 预设动画配置

所有可被 LLM / FSM 调用的预设动画必须登记在配置中。没有配置的动画不播放。

AnimationActionProfile 至少需要表达：

```text
动画名
clip 引用
动作类型 preset / locomotion
申请骨骼集合
关键骨骼集合
是否循环
是否允许调用
```

动画文件可以通过工具扫描曲线绑定路径，生成影响骨骼初稿，再由人工确认关键骨骼集合。

---

## 15. Locomotion 配置

第一版支持 walk / run。

LocomotionProfile 至少包含：

```text
walkClip
runClip
walkSpeed
runSpeed
walkAffectedBones
runAffectedBones
```

位移距离：

```text
distance = speed * duration
```

其中：

```text
duration = end - start
```

walkSpeed / runSpeed 在 Unity 组件或 ScriptableObject 中可调。

---

## 16. FSM 自主行为

FSM 是空闲自主行为调度器，不是单纯位移模块。

基本循环：

```text
Idle
↓
等待随机时间
↓
按权重抽取 FSMAction
↓
提交动作请求
↓
动作完成或被打断
↓
回到 Idle
```

第一版 FSMAction 支持：

```text
presetAnimation
locomotion
```

FSMAction 通用字段可包括：

```text
name
type
enabled
weight
cooldown，预留，默认 0
```

FSM 被用户交互打断后，不恢复原动作；用户交互结束后重新从 Idle 开始，等待随机时间，再抽取新动作。

FSM 动作失败或被边界停止后，也回到 Idle 并重新等待，不立刻重新抽取，避免抖动。

---

## 17. MovementConstraint

MovementConstraintController 独立负责移动合法性判断。

判断对象：角色根节点位置。

活动区域使用 RoomMoveArea 安全框，家具 / 障碍使用 ObstacleArea 安全框。

活动框可以比真实地面边界向内收缩，障碍框可以比真实家具边界向外扩张。

非法时发出：

```text
RequestStopLocomotion(reason)
```

reason 可为：

```text
OutOfBounds
ObstacleHit
Unknown
```

不回退、不规划、不重新随机、不改骨骼。

用户交互 locomotion 和 FSM locomotion 使用同一套停止流程。

---

## 18. 用户交互与 timeline 替换

用户点击发送才进入用户交互状态，输入框打字不算。

进入用户交互状态后停止 FSM。

用户提交新消息时，当前 timeline 不立刻停止，而是继续播放，直到新 LLM 结果返回并校验通过，形成新的可播放 timeline。

新 timeline 准备好后：

```text
停止旧 timeline
清掉旧 speech
播放新 timeline
```

旧 timeline 不排队、不恢复，latest timeline wins。

旧 timeline 停止时：

```text
如果新 timeline 申请同一骨骼：旧动作 -> 新动作
如果新 timeline 不申请该骨骼：旧动作 -> Idle
```

---

## 19. Debug 面板

第一版 Debug 面板用于验证骨骼轴向、左右镜像、BaseRotation 和动作参数。

规则：

1. Debug 是骨骼级控制源，不是状态。
2. 拖动某个骨骼滑块时，该骨骼 owner = Debug。
3. Release 释放骨骼所有权。
4. Zero 将骨骼置为语义 0,0,0，但仍保持 Debug 占用。
5. 一次只调一个语义骨骼。
6. 姿态采样默认关闭，勾选后采样。
7. 可以导出当前姿态相对 BaseRotation 的 JSON 动作参数。
8. 不允许手动覆盖 BaseRotation。

---

## 20. Profile / 配置结构

第一版建议以 CharacterProfile 作为角色总配置入口。

```text
CharacterProfile
  ├─ BoneMapProfile
  ├─ AnimationActionProfile 列表
  ├─ LocomotionProfile
  ├─ FSMProfile
  └─ DebugConfig
```

运行时配置使用 ScriptableObject，必要时支持 JSON 导入 / 导出。

给 LLM 的能力说明不是完整 Runtime 配置，而是从配置中导出的精简能力信息，包括：

```text
可控骨骼
可操作轴和范围
可用预设动画
可用 locomotion mode
可用 facing target
timeline 编排规则
禁止输出字段
```

LLM 不需要知道真实骨骼 path、BaseRotation、mirror、Unity 层级等内部细节。

---

## 21. LlmRelay 能力注入原则

LlmRelay 需要向 LLM 注入：

```text
当前角色名 Toki
可用 action 类型
timeline 编排规则
可控骨骼列表
每个骨骼可操作轴和范围
可用预设动画列表
walk/run 说明
facing target 列表
JSON 示例
```

明确禁止 LLM 输出：

```text
steps
direction
keep
transition
真实骨骼名
未登记动画
不可操作轴
```

---

## 22. 表情 / 嘴部 / speech

第一版不做复杂表情系统。

微笑、眨眼、皱眉等后续可以作为预设动画调用。

speech 第一版只负责文字气泡显示。

嘴部贴图切换不在第一版强制接入 speech。如果后面做说话口型，应作为独立 SpeechMouthController 模块处理。

---

## 23. 多角色扩展

第一版只支持 Toki。

工程结构预留多角色扩展。未来每个角色拥有自己的 CharacterProfile，包括独立的 BoneMap、AnimationActionProfile、LocomotionProfile、FSMProfile 和 LLM 能力导出。

第一版不做多角色同时在场，也不做角色切换 UI。

---

## 24. 第一阶段开发任务拆分

后续开发建议按以下阶段推进。每个阶段开发完成后先单独调试，确认稳定后再进入下一阶段。

### 阶段 1：角色初始化与 Idle

目标：角色能加载，BaseRotation 自动采集，Idle 能稳定播放。

内容：

1. Toki prefab 接入。
2. BaseRotation 自动采集。
3. IdleBaseProvider。
4. AvatarPoseApplier 初版。

完成标准：进入 Play 后角色能播放 Idle，且未发生骨骼姿态异常。

### 阶段 2：BoneMap 与 Debug 单骨骼控制

目标：能用 Debug 面板单独控制语义骨骼。

内容：

1. BoneMapProfile。
2. 语义骨骼到真实骨骼映射。
3. 单骨骼 x/y/z 滑块。
4. Debug 占用、Release、Zero。
5. 当前姿态参数导出。

完成标准：能验证骨骼轴向、左右镜像和语义 0,0,0。

### 阶段 3：ActionCoordinator 与骨骼交接过渡

目标：建立骨骼 owner 表、动作抢占和统一过渡。

内容：

1. BoneOwnerTable。
2. ActiveActionList。
3. HandoffTransition。
4. Debug / Idle 之间的交接。
5. 基础过渡曲线。

完成标准：Debug 占用和释放骨骼时能平滑回到 Idle。

### 阶段 4：本地 JSON TimelinePlayer

目标：先不接 LLM，用本地 JSON 验证 Runtime。

内容：

1. TimelineValidator。
2. TimelinePlayer。
3. speech。
4. bonePose。
5. timeline 替换规则初版。

完成标准：本地 JSON 可以驱动气泡和骨骼动作。

### 阶段 5：预设动画接入

目标：官方 / 自制预设动画能通过配置调用。

内容：

1. AnimationActionProfile。
2. AnimationPoseSampler。
3. 预设动画骨骼申请。
4. 预设动画被抢占时整体停止。

完成标准：timeline 可以调用预设动画，且和 bonePose 不乱抢骨骼。

### 阶段 6：Root 转向与 Locomotion

目标：角色能转向、walk/run 位移。

内容：

1. RootOrientationController。
2. facing action。
3. AutoTurnToCamera。
4. LocomotionActionExecutor。
5. walk/run speed。

完成标准：角色能按当前 forward 移动，能通过 facing 改变移动方向。

### 阶段 7：MovementConstraint

目标：角色不会走出活动范围或进入家具障碍框。

内容：

1. RoomMoveArea。
2. ObstacleArea。
3. 根节点位置判断。
4. RequestStopLocomotion。

完成标准：触碰边界或障碍时 locomotion 停止，角色停在当前位置。

### 阶段 8：FSM 自主行为

目标：角色空闲时能自动执行动作。

内容：

1. FSMProfile。
2. idleWaitRange。
3. FSMActionList。
4. presetAnimation action。
5. locomotion action。
6. 被用户交互打断后重置 FSM。

完成标准：非交互状态下角色能随机等待、随机动作、回 Idle 循环。

### 阶段 9：LlmRelay 接入

目标：用户文本输入后，LLM 返回 timeline，Unity 执行演出。

内容：

1. LlmRelay。
2. LLM 能力注入。
3. JSON 输出约束。
4. Unity 接收并播放 LLM timeline。

完成标准：用户输入文本后，角色能回复文字并完成动作。

### 阶段 10：调试工具补全与第一版打磨

目标：增强可观察性，修复运行时细节。

内容：

1. Runtime owner 监视器。
2. Timeline 调试器。
3. 动画扫描工具。
4. 常见错误日志。
5. 第一版整体联调。

完成标准：第一版闭环稳定可演示。

---

## 25. 最终架构总结

第一版应围绕统一骨骼协调核心构建：Idle 始终作为最低层动态姿态来源，FSM 和 LLM 只生成动作请求，Debug 只是骨骼级最高控制源，预设动画和位移动作只提供姿态或位移意图，所有骨骼所有权、抢占、打断、交接过渡和释放都由统一协调层集中处理，最终只有一个出口负责把协调后的 localRotation 写入 Toki 的真实骨骼。

Root 朝向和 Root 位移由独立控制器统一管理，不混入骨骼控制逻辑。

Runtime 严格执行明确 timeline，不做隐藏补丁、不做复杂兜底、不猜测 LLM 没有表达的意图。

后续开发应按模块逐步推进：开发一部分、调试一部分、确认稳定后再进入下一部分，直到完成第一版最小闭环。

