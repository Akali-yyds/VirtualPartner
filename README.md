# VirtualPartner

更新时间：2026-05-25

VirtualPartner 是一个 Unity 桌面虚拟陪伴角色项目。当前主角色是 Toki / CH0187，核心体验是通过 Momotalk 风格界面进行文字或语音交流，并让角色用 speech、表情、嘴型、参数动作、预设动作、朝向和位移形成可见反馈。

## 当前基线

当前活跃主链路已经回退到稳定的 `LlmRelay` one-shot StagePlan 路径：

```text
Momotalk / ASR 输入
-> MomotalkConversationController
-> LlmRelay
-> LLM 返回完整 StagePlan 2.0 JSON
-> StagePlanValidator
-> StagePlanPlayer
-> speech / expression / bonePose / animation / facing / locomotion
```

Stage 3 AgentRun / ToolRegistry / AgentLoop 的探索工作已经从当前活跃分支迁出并归档；它不再是当前运行基线，也不作为 Momotalk 正式入口。当前开发重点改为提示词工程：让 `LlmRelay` 在 one-shot 模式下生成更稳定、更丰富、更少被示例锚定的 StagePlan。

## 当前调整

- `examples.md` 是唯一默认注入的示例提示词，定位为格式参考，不允许按示例内容原样生成。
- `bone-pose-examples.md` 保留为参考资产，但不再默认注入 `LlmRelay` prompt。
- Runtime Generated Capabilities 继续作为运行时能力真源，但已瘦身，避免和静态提示词重复。
- Momotalk 增加独立的 `Clear Memory` 能力，只清除长期记忆，不清除聊天记录。
- 旧 AgentLoop / Tool Call 方向保留为研究资产，后续是否重启需要重新评估，不默认沿用 3.11 / 3.12 的实现。

## 文档入口

- [DevelopmentDirection.md](./DevelopmentDirection.md)：当前开发方向和架构边界。
- [DevelopmentTODO.md](./DevelopmentTODO.md)：当前 TODO、已完成项和待验收项。
- [FutureDevelopmentPlan.md](./FutureDevelopmentPlan.md)：未来候选方向。
- [ReadFirst.md](./ReadFirst.md)：协作开发规则。
- [DevelopmentDirection_stage3.md](./DevelopmentDirection_stage3.md)：Stage 3 Agent 探索的归档说明。
- [DevelopmentTODO_Stage3.md](./DevelopmentTODO_Stage3.md)：Stage 3 Agent 探索的暂停状态。
- [Archive/RootDocs_20260525_prompt_pivot/](./Archive/RootDocs_20260525_prompt_pivot/)：本次更新前的根目录原文档备份。

## 项目入口

Unity 工程：

```text
VirtualPartner/
```

主要场景：

```text
VirtualPartner/Assets/Scenes/SampleScene.unity
```

主要提示词目录：

```text
VirtualPartner/Assets/VirtualPartner/Prompts/
```

本地服务说明：

```text
VirtualPartner/LocalServices/TTS/README.md
VirtualPartner/LocalServices/ASR/README.md
```
