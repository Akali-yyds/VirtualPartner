# VirtualPartner 全局开发方向文档

更新时间：2026-05-19

本文档记录 VirtualPartner 当前实现方向、架构边界和后续开发时必须遵守的系统原则。它以当前完成态为准，不再把已经迁出的 timeline 1.0 当作活跃主线。

开发协作规则以 [ReadFirst.md](./ReadFirst.md) 为准。具体进展状态见 [DevelopmentTODO.md](./DevelopmentTODO.md)。

## 1. 当前定位

VirtualPartner 当前是一个 Unity 桌面虚拟陪伴角色项目。主角色为 Toki / CH0187，当前目标是稳定提供 Momotalk 文本与语音交流、角色语音输出、基础表情与嘴型、动作演出、Markdown 长期记忆和 Standalone 最小应用外壳。

当前主体验链路：

```text
用户文本或 ASR 文本
-> MomotalkConversationController
-> LlmRelay
-> LLM 返回 StagePlan 2.0 JSON
-> StagePlanValidator 校验
-> StagePlanPlayer 按 stage 执行
-> speech / expression / bonePose / animation / facing / locomotion
-> TTS / mouth / expression / ActionCoordinator / Root / Locomotion
-> MemoryJudge 在有效对话完成后判断是否写入长期记忆
```

LLM 只输出 Runtime 支持的结构化 JSON，不直接控制 Unity 对象、不直接写 Transform、不接触真实骨骼路径。

## 2. 当前主格式：StagePlan 2.0

当前唯一活跃主格式是 StagePlan 2.0。

基本结构：

```json
{
  "schemaVersion": "2.0",
  "type": "stagePlan",
  "metadata": {
    "intent": "greeting",
    "mood": "friendly"
  },
  "stages": [
    {
      "actions": [
        {
          "type": "speech",
          "text": "老师，我们继续吧。",
          "emotion": "friendly",
          "speed": 1.0
        }
      ]
    }
  ]
}
```

核心规则：

- `stages` 按数组顺序执行。
- Runtime 按数组顺序生成 stageIndex，LLM 不输出 `stageId`。
- stage 内 action 同时启动。
- 当前 stage 的 blocking action 完成后，才进入下一 stage。
- 每个 stage 最多一个 speech。
- speech 长度由真实 TTS 音频或文本估算决定，不由固定绝对时间轴决定。
- StagePlan 2.0 禁止 `timeline`、`start`、`end`、`stageId`。

历史说明：

- 第一阶段 timeline 1.0 已完成过验收，但在第二阶段被 StagePlan 2.0 替代。
- timeline 1.0 Runtime 和样例已迁出到 `Archive/Timeline1_0/`。
- 后续开发不得重新引入双格式主系统，除非单独开新阶段讨论。

## 3. Runtime 核心边界

### ActionCoordinator

ActionCoordinator 是骨骼控制权核心，负责：

- 骨骼 owner 管理。
- Debug / StagePlan bonePose / Locomotion / PresetAnimation / Idle 的抢占和释放。
- 骨骼交接过渡。
- 输出最终姿态给 AvatarPoseApplier。

任何动作源都不能绕过 ActionCoordinator 直接写受控骨骼。

### AvatarPoseApplier

AvatarPoseApplier 是最终骨骼 Transform 写入出口。Runtime 其他模块只提交动作意图或姿态目标，不分散写入真实骨骼。

### IdleBaseProvider

Idle 是最低层常驻姿态来源。被其他动作抢占的骨骼执行当前动作，未抢占骨骼继续使用 Idle。释放回 Idle 时，目标是当前动态 IdlePose，而不是固定起始帧。

### Root / Locomotion

Root 朝向和位移不属于普通骨骼控制。

- RootOrientationController 负责 facing 和 AutoTurnToCamera。
- LocomotionActionExecutor 负责 walk / run 位移和对应动作采样。
- MovementConstraintController 负责活动区域与障碍检测。

locomotion 只沿角色当前 forward 移动。需要改变方向时，先通过 facing 改变 Root 朝向。

### FSM

FSM 只负责非交互状态下的自主行为调度。它不直接写骨骼、不直接移动 Root、不绕过 StagePlan 或 ActionCoordinator。

用户交互优先级高于 FSM。用户交互打断 FSM 后，FSM 不恢复旧动作，而是从 Idle 重新开始等待。

## 4. 角色与多角色预留

当前主要角色仍是 Toki，但系统已建立最小多角色结构预留：

- CharacterProfile：角色静态配置入口。
- CharacterRuntimeContext：角色运行时上下文。
- CharacterRuntimeBinder：角色根节点绑定和组件收集。
- CharacterRegistry：当前场景已注册角色查询。

Momotalk、Prompt、Voice、Memory 等流程应通过 characterId 和 RuntimeContext 获取目标角色信息，不继续硬编码 Toki。

当前不做多角色同时在场调度、角色间主动对话或群聊。

## 5. Momotalk 交互边界

Momotalk 是正式交互入口，负责：

- 右侧手机按钮。
- Loading、联系人列表、聊天页。
- 文本输入和发送。
- ASR 识别结果填入或自动发送。
- 用户气泡、角色气泡、typing indicator。
- 聊天历史保存恢复。
- 未读提示。

Momotalk 不直接控制骨骼、Root、表情或语音播放。角色回复只来自通过 StagePlan 校验后的 speech action。

聊天记录和长期记忆必须分开：

```text
ChatHistory JSON = 对话流水账
Memory Markdown = 筛选后的长期有效信息
```

## 6. 表情与嘴型

嘴型由 MouthTextureController 控制，基于 Toki 的 8x8 嘴型图集和 MaterialPropertyBlock 切换 mouth index。

表情由 ExpressionActionExecutor 执行，来自 ExpressionProfile 白名单。当前基础表情包含 neutral、smile、thinking、surprised、embarrassed 等已配置项。

嘴型优先级：

```text
Debug Mouth Override
> Speech Mouth Driver
> Expression Base Mouth
> Idle / Default Mouth
```

表情相关面部骨骼仍必须走 ActionCoordinator，不允许直接写 Transform。

## 7. TTS 与 ASR

TTS 和 ASR 都通过本地服务接入。Unity 负责调用、状态展示和降级，不把模型推理逻辑内嵌到 Runtime。

TTS 当前职责：

- speech action 请求本地 TTS 包装服务。
- 播放生成音频。
- 等待音频完成后结束 speech blocking action。
- 失败时降级到文本估算时长和文本嘴型。
- 支持缓存和 Debug 状态展示。

ASR 当前职责：

- 通过 Momotalk 语音入口进入 Voice Mode。
- 调用本地 ASR 服务录音、VAD 和识别。
- 识别结果填入输入框或按配置自动发送。
- 服务不可用时不影响文本聊天。

## 8. Markdown 长期记忆

长期记忆使用 Markdown 文件，不使用 RAG 或向量数据库。

当前路径：

```text
VirtualPartner/UserData/Memory/{characterId}/
```

MemoryJudge 只在有效 Momotalk LLM 对话完整完成后运行。它判断是否写入长期有效信息，例如用户偏好、项目长期决策、关系变化或重要事件。

Prompt 注入优先读取当前角色 core / high 记忆。记忆是隐式上下文，角色不应频繁主动说明“我根据记忆知道”。

## 9. Runtime Debug

统一 Runtime Debug 面板用于开发调试，不是正式产品设置 UI。

当前观察入口包括：

- Overview
- LLM
- StagePlan
- Momotalk
- TTS
- ASR
- Memory
- Character
- FSM
- Root
- Bone
- Expr/Mouth

后续新增系统应优先接入统一 Debug，而不是分散创建不可追踪的临时调试入口。

## 10. Standalone App Shell

AppShell 是应用级 UI 与生命周期外壳，不接管 Momotalk、StagePlan、TTS、ASR、Memory 或角色动作 Runtime。

当前已实现 MVP：

- AppShellBootstrap。
- AppMenuUIManager。
- AppLifecycleController。
- Esc 打开 / 关闭 App Menu。
- 菜单只包含继续和退出。
- 退出确认。
- Editor 中停止 Play。
- Windows Standalone 中调用 Application.Quit。
- 退出前尽量复用现有 Runtime 停止 / 取消 / Shutdown 能力。

当前未实现 Settings、设置保存、分辨率切换、全屏切换、F11、音量控制。这些内容放入 [FutureDevelopmentPlan.md](./FutureDevelopmentPlan.md)。

## 11. 后续开发原则

- 保持简单、高效、直接。
- 只开发当前阶段明确需要的能力。
- 不为单次使用过早抽象。
- 不绕过现有高内聚模块边界。
- 不把暂缓需求写成已实现能力。
- 对 StagePlan、Memory 等纯逻辑模块，必要时补小范围自动化测试。
- 对 UI、角色表现、TTS、ASR、Standalone 行为，以 Play Mode、Standalone 和 Debug 面板手动验收为主。
