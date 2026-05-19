# VirtualPartner

更新时间：2026-05-19

VirtualPartner 是一个 Unity 桌面虚拟陪伴角色项目。当前主角色是 Toki / CH0187，目标是在本地桌面环境中通过 Momotalk 风格界面与角色进行文本和语音交流，并让角色用表情、嘴型、动作、语音和长期记忆形成可体验的陪伴反馈。

当前项目已经完成从第一阶段 Runtime 原型到第二阶段可体验版本的开发，并补齐了 Windows Standalone 的最小应用菜单与退出能力。

## 当前体验

- 通过右侧 Momotalk 按钮打开手机式聊天界面。
- 支持与 Toki 进行文本对话，并显示聊天记录、typing 状态和未读提示。
- 支持 StagePlan 2.0 驱动角色 speech、动作、转向、移动和基础表情。
- 支持本地 TTS 服务输出角色语音，失败时可降级为文本估算表现。
- 支持本地 ASR 服务进行语音输入，可填入输入框或自动发送。
- 支持基础嘴型反馈、Markdown 长期记忆和统一 Runtime Debug 面板。
- 支持 Standalone 中通过 Esc 打开应用菜单，并从应用内正常退出。

## 文档入口

- [DevelopmentTODO.md](./DevelopmentTODO.md)：全局开发进展、阶段状态和关键验收记录。
- [DevelopmentDirection.md](./DevelopmentDirection.md)：当前实现方向、架构边界和开发约束。
- [FutureDevelopmentPlan.md](./FutureDevelopmentPlan.md)：暂缓内容和未来候选方向。
- [ReadFirst.md](./ReadFirst.md)：协作开发规则，开发前必须遵守。
- [Archive/Docs/](./Archive/Docs/)：历史阶段文档归档。

## 项目入口

Unity 工程位于：

```text
VirtualPartner/
```

主要场景：

```text
VirtualPartner/Assets/Scenes/SampleScene.unity
```

本地服务说明：

```text
VirtualPartner/LocalServices/TTS/README.md
VirtualPartner/LocalServices/ASR/README.md
```

## 当前状态

当前主系统以 StagePlan 2.0 为唯一活跃执行格式。旧 timeline 1.0 已迁出到历史归档，不再作为 Runtime 主链路使用。

后续开发继续按 `ReadFirst.md` 的原则推进：先讨论边界，再生成计划，再实现，再验收，通过后更新全局 TODO。
