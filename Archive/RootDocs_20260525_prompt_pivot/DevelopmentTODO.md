# VirtualPartner 全局开发进展 TODO

更新时间：2026-05-19

本文档是项目当前唯一的全局开发进展入口，合并第一阶段、第二阶段和 Standalone App Shell MVP 的阶段状态。

开发原则以 [ReadFirst.md](./ReadFirst.md) 为准：简单、高效、直接；高内聚、低耦合；先讨论明确，再开发；只做当前阶段需要的内容。

## 执行节奏

- [ ] 进入新阶段前，先讨论并明确目标、边界和验收方式。
- [ ] 用户明确要求生成 plan 后，再生成可执行计划。
- [ ] plan 确认后再开发，不提前实现后续阶段内容。
- [ ] 开发完成后，通过 Play Mode、Standalone、Inspector、日志、Debug 面板和必要的资源检查进行验收。
- [ ] 用户确认验收通过后，再更新本文档状态。

## 当前总状态

- [x] 第一阶段 Runtime 原型完成。
- [x] 第二阶段正式交互体验完成。
- [x] StagePlan 2.0 已成为当前唯一活跃主格式。
- [x] timeline 1.0 已迁出到 `Archive/Timeline1_0/`，仅保留历史追溯。
- [x] Standalone App Shell MVP 完成：Esc 菜单、继续、退出确认、应用内退出。

## 第一阶段：Runtime 最小闭环

### 阶段 0：项目开发约定与验收流程

**阶段目标**  
固定协作方式和验收流程，确认 Toki / CH0187 作为第一版主角色。

**关键结果**

- [x] 建立阶段式开发节奏。
- [x] 确认 Unity 工程路径为 `VirtualPartner/`。
- [x] 确认主模型入口为 `Assets/Character/Toki/Prefabs/SourceParts/CH0187_Mesh.prefab`。

**完成状态**

- [x] 阶段完成。

### 阶段 1：角色初始化与 Idle

**阶段目标**  
角色能在场景中稳定加载，Play 后采集 BaseRotation，并持续提供 Idle 姿态。

**关键结果**

- [x] Toki 能正常显示并播放 Idle。
- [x] 已登记骨骼能采集 BaseRotation。
- [x] 未出现明显骨骼错位、爆姿态或材质丢失。

**完成状态**

- [x] 阶段完成。

### 阶段 2：BoneMap 与 Debug 单骨骼控制

**阶段目标**  
用 Debug 面板验证语义骨骼、轴向、左右镜像和语义零点。

**关键结果**

- [x] 建立语义骨骼到真实骨骼的映射配置。
- [x] 支持单骨骼参数调试、Zero、Release 和 JSON 姿态片段导出。
- [x] Debug 控制为后续 ActionCoordinator 接入预留接口。

**完成状态**

- [x] 阶段完成。

### 阶段 3：ActionCoordinator 与骨骼交接过渡

**阶段目标**  
建立统一骨骼控制权管理，避免动作源直接抢写 Transform。

**关键结果**

- [x] 支持 owner 管理、抢占、释放和骨骼交接过渡。
- [x] Debug 与 Idle 能通过 ActionCoordinator 平滑交接。
- [x] 多次占用和释放后姿态不累计漂移。

**完成状态**

- [x] 阶段完成。

### 阶段 4：本地 JSON TimelinePlayer

**阶段目标**  
在不接 LLM 的情况下，用本地 JSON 验证 Runtime 链路。

**关键结果**

- [x] 支持本地 timeline 1.0 的 speech 和 bonePose。
- [x] 支持新 timeline 替换旧 timeline。
- [x] 非法 action 局部失败时，合法 action 可继续执行。

**历史说明**

- timeline 1.0 后续已被 StagePlan 2.0 替代，并迁出主项目。

**完成状态**

- [x] 阶段完成。

### 阶段 5：预设动画接入

**阶段目标**  
通过配置调用白名单预设动画，并接入统一骨骼控制规则。

**关键结果**

- [x] 已登记动画可通过结构化动作调用。
- [x] 未登记动画不会播放，并有清晰日志。
- [x] animation 与 bonePose 不会乱抢或双写骨骼。

**完成状态**

- [x] 阶段完成。

### 阶段 6：Root 转向与 Locomotion

**阶段目标**  
角色能改变 Root 朝向，并沿当前 forward 执行 walk / run 位移。

**关键结果**

- [x] 支持 facing、AutoTurnToCamera、walk / run 位移。
- [x] locomotion 不包含 steps / direction，只沿当前 forward 移动。
- [x] Root 保护、非法参数校验和 Debug 抢占符合阶段目标。

**完成状态**

- [x] 阶段完成。

### 阶段 7：MovementConstraint

**阶段目标**  
限制角色活动范围，避免走出安全区域或进入障碍区域。

**关键结果**

- [x] RoomMoveArea / ObstacleArea 可限制 locomotion。
- [x] 非法移动时 Root 保持在上一合法位置。
- [x] 用户交互和 FSM locomotion 使用同一套停止逻辑。

**完成状态**

- [x] 阶段完成。

### 阶段 8：FSM 自主行为

**阶段目标**  
角色在非用户交互状态下随机等待、执行空闲动作，并回到 Idle 循环。

**关键结果**

- [x] 支持等待时间范围、权重抽取、预设动画和 locomotion。
- [x] 用户交互可打断 FSM，交互结束后从 Idle 重新开始。

**完成状态**

- [x] 阶段完成。

### 阶段 9：LlmRelay 接入

**阶段目标**  
用户输入文本后，请求 LLM 返回结构化动作数据并执行。

**关键结果**

- [x] 已跑通用户输入、LLM 请求、结构化返回、校验和播放。
- [x] 支持 latest-wins 替换规则。

**历史说明**

- 第一阶段使用 timeline 1.0；第二阶段已迁移到 StagePlan 2.0。

**完成状态**

- [x] 阶段完成。

### 阶段 10：调试工具补全与第一版打磨

**阶段目标**  
补齐第一版演示所需可观察性和调试工具。

**关键结果**

- [x] 统一 Runtime Debug 面板可观察 LLM、Timeline、FSM、Root、Bone 等关键状态。
- [x] 第一版最小闭环稳定可演示。

**完成状态**

- [x] 阶段完成。

## 第二阶段：正式交互与语音陪伴体验

### 阶段 2.0：二阶段开发约定与基线确认

**阶段目标**  
确认第二阶段继承边界、主格式和验收节奏。

**关键结果**

- [x] 确认 StagePlan 2.0 是第二阶段主格式。
- [x] 确认 timeline 1.0 迁出前必须有 Git 备份或 tag。
- [x] 确认每阶段开始前单独讨论。

**完成状态**

- [x] 阶段完成。

### 阶段 2.1：CharacterProfile / RuntimeContext 最小预留

**阶段目标**  
建立最小角色配置和运行时上下文入口，减少 Toki 硬编码。

**关键结果**

- [x] 新增 CharacterProfile、CharacterRuntimeContext、CharacterRegistry 和 CharacterRuntimeBinder。
- [x] Play 后 Toki 可注册为当前可交互角色。
- [x] RuntimeContext 当前只作为查询入口，不接管现有 Runtime 行为。

**完成状态**

- [x] 阶段完成。

### 阶段 2.2：StagePlan JSON 2.0 本地解析与校验

**阶段目标**  
建立 StagePlan 2.0 的本地解析和校验能力。

**关键结果**

- [x] 支持 `schemaVersion`、`type`、`stages`、`actions` 基础校验。
- [x] 每个 stage 最多一个 speech。
- [x] 禁止 `timeline`、`start`、`end`、`stageId` 进入当前主格式。
- [x] Runtime 按数组顺序生成 stageIndex，LLM 不负责命名 stage。

**完成状态**

- [x] 阶段完成。

### 阶段 2.3：StagePlanPlayer 本地执行闭环

**阶段目标**  
用 StagePlan 2.0 阶段式执行模型跑通本地播放链路。

**关键结果**

- [x] stages 按顺序执行，stage 内 actions 同时启动。
- [x] 等待 blocking action 完成后进入下一 stage。
- [x] speech、bonePose、animation、facing、locomotion 复用现有 Runtime 链路。
- [x] expression 在本阶段先作为 no-op warning，后续阶段正式接入。

**完成状态**

- [x] 阶段完成。

### 阶段 2.4：LlmRelay / Prompt 迁移到 StagePlan 2.0

**阶段目标**  
让 LLM 只输出 StagePlan 2.0，并由 Unity 校验后执行。

**关键结果**

- [x] Prompt 已迁移到 StagePlan 2.0 规则。
- [x] LlmRelay 迁移到 StagePlan 输出、提取、校验和播放链路。
- [x] 旧 response 忽略，新合法 response 才替换当前 LLM StagePlan。

**完成状态**

- [x] 阶段完成。

### 阶段 2.5：timeline 1.0 完整迁出

**阶段目标**  
将 timeline 1.0 从主项目迁出，避免双格式 Runtime 干扰。

**关键结果**

- [x] 迁出前已存在 Git tag `stage2.4-approved`。
- [x] timeline 1.0 Runtime 脚本和样例已迁出到 `Archive/Timeline1_0/`。
- [x] 主项目活跃 Runtime、Prompt、Debug 文案和场景组件不再保留 timeline 1.0 正向执行路径。

**完成状态**

- [x] 阶段完成。

### 阶段 2.6：Momotalk 参考资源与 UI 素材准备

**阶段目标**  
准备 Momotalk UI 参考归档、资源目录和预览图。

**关键结果**

- [x] 建立 `Archive/MomotalkVisualReference/README.md`。
- [x] 准备 `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/` 资源目录。
- [x] 输出 Loading、联系人首页、聊天页预览图和核心 UI 图片资源。

**完成状态**

- [x] 阶段完成。

### 阶段 2.7：Momotalk UI 外壳

**阶段目标**  
建立可打开、可关闭、具备基础页面结构的 Momotalk 手机 UI。

**关键结果**

- [x] Play 后右侧出现 Momotalk 按钮。
- [x] 手机面板可右侧滑出和关闭。
- [x] 支持 Loading、联系人列表、Toki 聊天页静态结构。

**完成状态**

- [x] 阶段完成。

### 阶段 2.8：Momotalk 文本聊天闭环

**阶段目标**  
让 Momotalk UI 成为正式文本聊天入口。

**关键结果**

- [x] 支持聊天页输入、发送、用户气泡、typing indicator。
- [x] StagePlan speech 按 stage 顺序同步为角色气泡。
- [x] 支持基础聊天记录保存恢复和关闭期间未读提示。
- [x] 搜索、Chat Info 临时详情页、置顶、清除聊天记录和返回修复已通过验收。

**完成状态**

- [x] 阶段完成。

### 阶段 2.9：嘴型 / 基础表情系统 V1

**阶段目标**  
让 Toki 说话时有基础嘴型反馈，并支持白名单表情 action。

**关键结果**

- [x] 支持 mouth index 切换和基础嘴型配置。
- [x] StagePlan expression action 可切换白名单表情。
- [x] 文本 fallback 嘴型可用，Debug 嘴型覆盖优先级最高。

**完成状态**

- [x] 阶段完成。

### 阶段 2.10：TTS 接口、降级与缓存框架

**阶段目标**  
建立 TTS 调用入口、播放入口、缓存框架和失败降级。

**关键结果**

- [x] MockTTS 可驱动 speech action 等待模拟时长完成。
- [x] TTS 失败时 StagePlan 和 Momotalk 气泡继续。
- [x] Debug 可查看 TTS 状态。
- [x] 修复未读红点和等待回复期间 typing indicator 恢复问题。

**完成状态**

- [x] 阶段完成。

### 阶段 2.11：TTS 真实语音克隆接入

**阶段目标**  
接入真实 TTS 语音克隆服务，让 Toki speech 播放角色语音。

**关键结果**

- [x] 接入 GPT-SoVITS 包装服务和启动说明。
- [x] speech 可合成语音并等待音频播放完成。
- [x] 嘴型能随真实音频强度开合。
- [x] 补充 LLM StagePlan JSON 抽取兜底。

**完成状态**

- [x] 阶段完成。

### 阶段 2.12：ASR 接口与 Voice Mode UI

**阶段目标**  
建立 ASR 服务入口、Voice Mode UI 和 MockASR 流程。

**关键结果**

- [x] Momotalk 输入区支持语音入口。
- [x] MockASR 可返回文本。
- [x] 支持 FillInputOnly 和 AutoSendToLlm 配置。
- [x] ASR 不可用时文本输入仍可用。

**完成状态**

- [x] 阶段完成。

### 阶段 2.13：ASR 真实识别接入

**阶段目标**  
接入真实本地 ASR 服务，让用户语音进入 Momotalk 输入流程。

**关键结果**

- [x] 接入 sherpa-onnx 本地 ASR 服务。
- [x] 支持真实麦克风输入、VAD 自动结束、识别结果填入输入框和自动发送。
- [x] 服务启动时预热 recognizer 和常驻麦克风输入流，减少点击后丢字问题。

**完成状态**

- [x] 阶段完成。

### 阶段 2.14：Markdown 长期记忆 V0

**阶段目标**  
让角色自动记录长期有效信息，并在后续对话中注入高优先级记忆。

**关键结果**

- [x] 新增独立 Markdown 长期记忆链路。
- [x] 只有完整 finished 的正式 Momotalk LLM requestId 会进入 MemoryJudge。
- [x] 长期记忆写入 `VirtualPartner/UserData/Memory/{characterId}/` 六类 Markdown 文件。
- [x] LlmRelay 会注入当前角色 core / high 记忆。

**完成状态**

- [x] 阶段完成。

### 阶段 2.15：Debug 整合与二阶段收尾

**阶段目标**  
将第二阶段新增系统纳入统一 Runtime Debug，并完成二阶段归档。

**关键结果**

- [x] Runtime Debug 可查看 Momotalk、StagePlan、ASR、TTS、Memory、Character 等关键状态。
- [x] 已生成第二阶段归档文档，并同步更新根 README。
- [x] 二阶段最终验收通过。

**完成状态**

- [x] 阶段完成。

### 阶段 2.16：Standalone Basic App Shell MVP

**阶段目标**  
为 Windows Standalone 补齐最小应用外壳：Esc 菜单、继续、退出确认和应用内退出。

**关键结果**

- [x] 新增 AppShellBootstrap、AppMenuUIManager、AppLifecycleController。
- [x] Esc 可打开 / 关闭 App Menu。
- [x] App Menu 本轮只包含继续和退出。
- [x] 退出前复用现有 Runtime 清理链路。
- [x] Editor 中确认退出会停止 Play，Standalone 中确认退出会调用 Application.Quit。

**明确未做**

- [x] 暂缓 Settings、设置保存、分辨率 / 全屏切换、F11、音量控制。
- [x] 不重构 Momotalk、StagePlan、TTS、ASR、Memory 或角色动作 Runtime。

**完成状态**

- [x] 阶段完成。

## 历史文档

更完整的阶段原始记录已归档到：

```text
Archive/Docs/
Archive/Timeline1_0/
Archive/MomotalkVisualReference/
```
