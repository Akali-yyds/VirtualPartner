# VirtualPartner

更新时间：2026-06-14

VirtualPartner 是一个基于 Unity 的桌面虚拟陪伴角色项目。它的目标不是只做一个聊天窗口，而是把虚拟角色、Momotalk 风格手机 UI、LLM 行为规划、本地语音服务和可交互场景组合成一个可运行的桌面陪伴系统。

当前主要角色是 Toki / CH0187。主链路以 `LlmRelay` one-shot StagePlan 2.0 为稳定基线：用户通过文本或语音输入，LLM 返回结构化 StagePlan，Unity 侧再播放语音、表情、骨骼姿势、预设动画、朝向和位移反馈。

## 演示视频

- [Bilibili 演示视频](https://www.bilibili.com/video/BV1KSEy6pEHX/)

## 当前能力

- Momotalk 风格虚拟手机界面：支持联系人与聊天式交互，后续会继续并入完整虚拟手机 AppHost 体系。
- 文本与语音输入：支持文本对话、ASR 语音识别入口、本地 TTS 语音播放链路。
- LLM 行为规划：使用 StagePlan 2.0 JSON 描述角色行为，当前稳定路径为 `LlmRelay` one-shot 生成。
- 角色反馈播放：支持 `speech`、`expression`、`bonePose`、`animation`、`facing`、`locomotion` 等动作类型。
- 提示词系统：拆分角色设定、动作规则、运行时能力、示例和具名手势提示词，便于持续优化角色表现。
- 场景呈现：固定背景图与可移动 `SceneCamera` 分离，房间外轮廓描边可调，支持镜头控制模式。
- 运行时工具：Debug 面板默认隐藏，可通过按钮打开；API 配置支持在面板内填写、保存与测试。
- 发布脚手架：包含 Windows V1 Launcher、本地服务 payload 构建脚本和可迁移压缩包流程。

## 技术栈

- Unity `6000.3.12f1`
- C# / uGUI
- Universal Render Pipeline `17.3.0`
- Cinemachine `3.1.6`
- Unity Input System `1.19.0`
- Python 本地服务
- GPT-SoVITS / 本地 TTS
- sherpa-onnx / 本地 ASR
- OpenAI-compatible Chat Completions API
- PowerShell 打包脚本
- WPF Launcher / .NET

## 项目结构

```text
VirtualPartner-new/
  VirtualPartner/
    Assets/
      Scenes/
        SampleScene.unity
      VirtualPartner/
        Art/
        Editor/
        Materials/
        Profiles/
        Prompts/
        Runtime/
        Shaders/
        StagePlans/
        UI/
    LocalServices/
      ASR/
      TTS/
    Packages/
    ProjectSettings/

  Launcher/
    VirtualPartnerLauncher/
    Build-RuntimePayloads.ps1
    Build-V1Release.ps1
    launcher_config.v1.json

  DevelopmentDirection.md
  DevelopmentTODO.md
  FutureOptimization.md
  ReadFirst.md
  ReleasePackagingGuide.md
```

## 运行环境

- Windows 10 / Windows 11
- Unity 6，当前工程版本为 `6000.3.12f1`
- 可访问 OpenAI-compatible Chat Completions API 的模型与 API Key
- 本地 TTS 运行环境，当前流程使用 GPT-SoVITS runtime/payload
- 本地 ASR 运行环境，当前流程使用 sherpa-onnx 相关模型与服务
- 如需重新构建运行时 payload：需要 Conda、PowerShell 和对应模型/服务文件
- 如需重新发布 V1 包：需要 .NET SDK、Unity Windows Build 产物和 `Launcher/` 打包脚本

## 文档入口

- [DevelopmentDirection.md](./DevelopmentDirection.md)：项目方向、架构边界与阶段性目标。
- [DevelopmentTODO.md](./DevelopmentTODO.md)：当前开发进度、已完成内容和待办事项。
- [ReleasePackagingGuide.md](./ReleasePackagingGuide.md)：从 Unity 导出到生成可迁移压缩包的流程记录。
- [FutureOptimization.md](./FutureOptimization.md)：未来候选优化方向。
- [ReadFirst.md](./ReadFirst.md)：协作开发规则与工程约束。

## 说明

这个项目目前包含 Unity 客户端、本地语音服务、LLM API 配置和发布脚手架，开发环境配置无法用一小段命令准确讲清楚。后续会把“快速开始 / 开发环境配置 / 发布流程”整理成独立文档，避免 README 变成不可靠的长篇安装说明。
