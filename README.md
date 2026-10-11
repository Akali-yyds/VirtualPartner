# VirtualPartner

更新时间：2026-10-11

VirtualPartner 是一个基于 Unity 的桌面虚拟陪伴角色项目。它的目标不是只做一个聊天窗口，而是把虚拟角色、Momotalk 风格手机 UI、LLM 行为规划、本地语音服务和可交互场景组合成一个可运行的桌面陪伴系统。

当前主要角色是 Toki / CH0187。日常入口是虚拟手机 OS；对话通过 `LlmRelay → StagePlan 2.0 → Validator → Player` 执行，支持流式接收与分阶段播放，由 Unity 协调语音、表情、骨骼、朝向和位移。

**当前重点：MoMask 实验检查点与 Toki 骨架适配。** 三路线比较、短中长动作对照及临时输入面板已交付。用户初测认为 MoMask 能根据自然语言产生较流畅的动作，选择其作为后续优先路线；同时发现穿模和 Toki 头部前倾等适配问题。当前保存的是实验阶段检查点，尚未完成角色适配验收，也未把学习式动作生成接入日常聊天。

## 演示视频

- [Bilibili 演示视频](https://www.bilibili.com/video/BV1KSEy6pEHX/)

## 当前开发进度

| 阶段 | 当前状态 |
|---|---|
| PhoneOS 初步视觉与四个 App 接入 | 已完成 Momotalk、Settings、Camera、Debug 的主要功能接入 |
| 三键导航、最近任务、应用状态 | 已实现任务切换、移除、返回关系、草稿及页面状态保留 |
| 视觉统一与其余页面改造 | 桌面、聊天、最近任务代表页已接受；其余页面已交付，人工体验确认仍有剩余项 |
| 真实对话与语音链路 | DeepSeek → StagePlan → 真实 GPT-SoVITS 播放已有验证；不代表全部动作场景通过 |
| 空间动作与等待思考姿态 | 已实现并保存阶段检查点；开放动作准确性、完整关节约束与自然度尚未验收 |
| 自然语言动作路线实验 | 已交付三路线基线、88 条主样本及真实 TTS 并发记录；额外完成 MoMask / DiP 短中长 18 段对照，用户优先选择 MoMask |
| MoMask 临时交互实验 | 已交付独立场景输入面板、常驻预热模型、无 TTS 的生成／重播／中断及分阶段计时；用户已初步实测 |
| Toki 专项适配与上下文时长 | **待讨论、未实施**：头颈基准姿态、Q 版比例下的活动范围与穿模，以及由 LLM 和上下文决定动作时长 |

PhoneOS 已有四种分辨率 × 三种手机高度的布局检查，以及应用、导航和受控服务回归；中文输入法、语音识别准确率、镜头手感和最终视觉仍需实机确认。真实移动验收曾被场景障碍区拒绝，未通过修改边界或假动作掩盖。

详细记录：[PhoneOS 当前进度](Docs/PhoneOS-Progress.md) · [页面与真实链路验收](Docs/PhoneOS-RemainingPagesReview.md) · [空间动作检查点](Docs/SpatialMotion-Progress.md) · [动作路线实验进度](Docs/MotionLab-Progress.md)。

## 当前技术路线

### 日常对话与角色执行

```text
文本输入 / ASR
       ↓
Momotalk → LlmRelay（OpenAI-compatible API，目前使用 DeepSeek）
       ↓
StagePlan 2.0（流式接收 / 分阶段调度）
       ↓
Validator → Player
       ├─ GPT-SoVITS → 音频播放 / 口型
       ├─ 表情
       └─ 骨骼 / 空间轨迹 / 朝向 / 位移
                    ↓
              ActionCoordinator → 角色骨架
```

保留直接 `bonePose` 控制。新增的 `spatialPose` / `poseReset` 探索将空间目标、轨迹、双骨链 IK、保持与恢复接入现有协调器；请求等待期间使用当前角色专用的低优先级思考姿态。思考动画用于等待反馈，不计作正式动作响应，也不作为 LLM 必须复制的动作答案。

### 已比较的动作生成方案

| 路线 | 分工 | 当前需要验证的问题 |
|---|---|---|
| LLM 语义目标 + Unity IK | LLM 表达“肩高、左前方、挥两下”等意图；Unity 使用实际骨架换算目标 | 动作语义覆盖、掌心与关节方向、自然度、保持与局部接管 |
| MoMask | 从文本生成完整动作片段，再转换到当前角色骨架 | 动作准确性、左右与幅度、脚滑、重定向及转换开销 |
| DiP | 使用动作前缀，短段自回归生成；对比 5 / 10 步 | 当前姿态转换、连续性、打断、实时速度与资源占用 |

当前角色不是 Humanoid，不能直接假定 Unity Humanoid 重定向可用。实验将源骨架与角色并排显示，分别保存原始输出、转换结果、计时和录屏；播放仍通过 `ActionCoordinator`，避免与 Idle、FSM、Debug 争用骨骼。

实验机器为 i7-9750H / 16 GB RAM / GTX 1650 4 GB，采用本地独立 Python 环境，不改动 GPT-SoVITS 环境。MoMask 与 DiP 均已取得本机真实生成结果，但生成成功、骨长保持和合法 JSON 都不能替代动作意图与观感验收。

实验记录包含 88 条主样本：语义控制和 MoMask 各 36 条、DiP 两类前缀各 8 条；失败结果同样保留。随后以 9 条指令生成 MoMask / DiP 各 9 段短、中、长动作，保留全部 18 段对照。用户选择 MoMask 为后续优先路线；语义 + IK 与 DiP 的实验保留作为比较基线，正式接入方式尚待确定。

### MoMask 临时交互与计时

Unity 菜单 **VirtualPartner → Motion Lab → Open MoMask playground** 打开独立实验场景。等待模型加载和预热至 `READY` 后，可输入自然语言，生成、重播或中断动作，并排查看源骨架和 Toki。当前面板提供 2／4／8 秒与随机种子用于对照实验；这些固定档位不是后续交互的最终时长策略。

```text
中文自然语言 → LLM 转成英文动作描述 ─┐
英文直接输入（跳过 LLM）───────────┤
                                ↓
常驻 MoMask → HumanML22 位置序列 → Toki 重定向 → ActionCoordinator
```

面板不调用 TTS，不读取聊天历史或写入长期记忆。LLM 转换、MoMask 生成和提交到首个实际身体动作分别计时，冷启动与预热排除。一次中文挥手样本测得 **LLM 0.835 秒、MoMask 0.640 秒、首动作 1.532 秒**；MoMask 数值包含坐标转换与导出。这是少量暖运行实验，不能当作稳定延迟保证。

另有服务预热后的“早上好呀”真实并发样本：LLM 完整输出 **1.184 秒**，MoMask **2.021 秒**，提交至首动作 **3.260 秒**、真实音频播放 **11.270 秒**。MoMask 与 TTS 并行，不能相加计算等待；该测试使用独立短提示，未经过日常 Momotalk / StagePlan 完整链路。

使用与证据见 [临时输入面板](Docs/MoMask-Playground.md)、[短中长动作对照](Docs/MotionLab-DurationComparison.md)、[真实暖运行问候计时](Docs/MotionLab-WarmGreeting-Timing.md)及[实验评估](Docs/MotionLab-Evaluation.md)。模型权重、生成缓存、原始结果和录像保留在本机实验目录，不随源码检查点提交到 GitHub。

## 当前能力

- 虚拟手机 OS：已接入 Momotalk、Settings、Camera、Debug 四个 App，默认场景为 `Assets/Scenes/PhoneOS.unity`。真实 LLM 验收需有效 API 配置；接入范围与验证边界见 [PhoneOS-LiveIntegration](Docs/PhoneOS-LiveIntegration.md)。
- 文本与语音输入：支持文本对话、ASR 语音识别入口、本地 TTS 语音播放链路。
- LLM 行为规划：使用 StagePlan 2.0 JSON 描述角色行为，支持流式 stage 接收；不要求始终等完整回复结束后才开始播放。
- 角色反馈播放：支持 `speech`、`expression`、`bonePose`、`animation`、`facing`、`locomotion` 等动作类型。
- 提示词系统：拆分角色设定、动作规则和运行时能力；运行时占用与保持摘要提供当前身体状态。示例用于协议说明，不保证特定角度适用于每次动作。
- 场景呈现：固定背景图与可移动 `SceneCamera` 分离，房间外轮廓描边可调，支持镜头控制模式。
- 运行时工具：日常使用手机 Debug App；API 配置统一进入 Settings，支持编辑、测试和显式保存。旧界面保留用于开发回归。
- 发布脚手架：包含 Windows V1 Launcher、本地服务 payload 构建脚本和可迁移压缩包流程。

## 技术栈

- Unity `6000.3.12f1`
- C# / uGUI / TextMeshPro / Noto Sans 与中文回退
- Universal Render Pipeline `17.3.0`
- Cinemachine `3.1.6`
- Unity Input System `1.19.0`
- Python 本地服务
- GPT-SoVITS / 本地 TTS
- sherpa-onnx / 本地 ASR
- OpenAI-compatible Chat Completions API
- PowerShell 打包脚本
- WPF Launcher / .NET
- 动作实验：PyTorch / MoMask / DiP / HumanML3D 动作表示；实验依赖独立于日常运行环境

## 下一步优化方向

以下是用户反馈与待讨论方向，不代表新阶段已实施或已生成详细开发计划。

1. **优先完成 MoMask → Toki 的正常、流畅演出。** 核对头颈映射、轴向与基准姿态，定位用户观察到的默认头部前倾。根因尚未确认，不能直接归因于 MoMask 或某一个骨骼角度。
2. **适配 Q 版身体比例与活动范围。** Toki 的大头、短肢体等比例与生成源人体不同；重点检查手臂与头部／躯干穿插、关节范围和脚底支撑。结合源骨架与最终角色对照，区分生成问题和重定向问题。
3. **让 LLM 和具体上下文决定动作时长。** 不把人工选择的 2／4／8 秒作为最终交互要求。需讨论模型片段长度限制、动作顺序与长动作衔接；当前适配器仍有长度限制，尚未实现任意时长生成。
4. **持续测量暖运行与语音并发。** 分别记录 LLM、生成、转换、首动作与真实音频播放，保留不含冷启动的计时；动作先行、语音同步与连续交互策略仍待确定。
5. **角色适配与人工观感确认后，再讨论日常接入。** 当前优先修正头部、穿模和动作范围，不把“生成并播放成功”当成完整验收；保留现有聊天、FSM、记忆和语音业务链路。

本轮聚焦站立手势、指向、头部与躯干配合、浅蹲和保持，不扩展跳跃、动态平衡、精确身体接触或手指控制。物理策略训练、大规模动作模型与远程 GPU 不作为当前实施前提。

## 项目结构

```text
VirtualPartner-new/
  VirtualPartner/
    Assets/
      Scenes/
        PhoneOS.unity
        PhoneOS_VisualReview.unity
        MotionLab.unity          # 独立动作实验场景
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

  Docs/                       # 分阶段进度、验收与调研
  Tools/                      # 开发与实验工具，MotionLab 含生成、计时和对照播放器

  DevelopmentDirection.md
  DevelopmentTODO.md
  FutureOptimization.md
  ReadFirst.md
  ReleasePackagingGuide.md
```

本机还有独立的 `ModelRepairTool` 模型修复工程及 `TTS/GPT-SoVITS` 服务目录，它们不是本仓库中的同名子模块。动作模型的源码、环境和权重放在独立实验目录，不提交到 Unity 资源或 Git。

## 运行环境

- Windows 10 / Windows 11
- Unity 6，当前工程版本为 `6000.3.12f1`
- 可访问 OpenAI-compatible Chat Completions API 的模型与 API Key
- 本地 TTS 运行环境，当前流程使用 GPT-SoVITS runtime/payload
- 本地 ASR 运行环境，当前流程使用 sherpa-onnx 相关模型与服务
- 如需重新构建运行时 payload：需要 Conda、PowerShell 和对应模型/服务文件
- 如需重新发布 V1 包：需要 .NET SDK、Unity Windows Build 产物和 `Launcher/` 打包脚本

## 文档入口

- [PhoneOS-Progress](Docs/PhoneOS-Progress.md)：手机 OS 的实际交付阶段与剩余验收。
- [SpatialMotion-Progress](Docs/SpatialMotion-Progress.md)：空间动作、保持与思考姿态检查点及已知不足。
- [MotionLab-Progress](Docs/MotionLab-Progress.md)：当前动作路线实验、证据与测量边界。
- [MoMask-Playground](Docs/MoMask-Playground.md)：临时自然语言输入面板与暖运行计时。
- [MotionLab-DurationComparison](Docs/MotionLab-DurationComparison.md)：MoMask / DiP 短中长动作对照及用户反馈。
- [MotionLab-WarmGreeting-Timing](Docs/MotionLab-WarmGreeting-Timing.md)：真实 LLM、MoMask、TTS 与 Unity 并发计时。
- [MotionRoute-Evaluation](Docs/MotionRoute-Evaluation-2026-10-09.md)：路线选择的调研依据与官方来源。
- [DevelopmentDirection.md](./DevelopmentDirection.md)：项目方向、架构边界与阶段性目标。
- [DevelopmentTODO.md](./DevelopmentTODO.md)：当前开发进度、已完成内容和待办事项。
- [ReleasePackagingGuide.md](./ReleasePackagingGuide.md)：从 Unity 导出到生成可迁移压缩包的流程记录。
- [FutureOptimization.md](./FutureOptimization.md)：未来候选优化方向。
- [ReadFirst.md](./ReadFirst.md)：协作开发规则与工程约束。

## 说明

这个项目目前包含 Unity 客户端、本地语音服务、LLM API 配置和发布脚手架，开发环境配置无法用一小段命令准确讲清楚。后续会把“快速开始 / 开发环境配置 / 发布流程”整理成独立文档，避免 README 变成不可靠的长篇安装说明。

API 密钥、用户聊天、长期记忆、本地缓存及模型权重不应进入仓库。真实服务测试采用隔离上下文；不使用模拟回复冒充真实链路通过。项目代码、角色素材、动作模型权重及数据集各自适用的许可需要分别确认。
