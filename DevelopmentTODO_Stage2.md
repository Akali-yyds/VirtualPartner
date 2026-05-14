# VirtualPartner 第二阶段阶段性开发 TODO

本文档是第二阶段开发的长期跟踪清单，来源于 `virtual_partner第二阶段开发方向细节文档.md` 和本轮计划讨论。

开发原则以 `ReadFirst.md` 为准：简单、高效、直接；高内聚、低耦合；先讨论明确，再开发；只做当前阶段需要的内容。

## 执行节奏

- [ ] 进入某个阶段前，先讨论并明确该阶段的具体目标、边界和验收方式。
- [ ] 明确后再开发，不提前实现后续阶段内容。
- [ ] TODO 只记录阶段级任务，不提前写死未讨论过的类、字段、接口或路径细节。
- [ ] 开发完成后，通过 Play Mode、Inspector、日志和必要的资源检查进行手动验收。
- [ ] 对 StagePlan、Memory 等纯逻辑模块，按需要补充小范围自动化测试。
- [ ] 验收通过后，勾选该阶段完成，再进入下一阶段。

## 第二阶段总原则

- [ ] 第二阶段主格式为 StagePlan JSON 2.0。
- [ ] StagePlan 2.0 验收通过后，timeline 1.0 不再作为主系统活跃格式。
- [ ] timeline 1.0 迁出前必须完成 Git 备份或 tag。
- [ ] timeline 1.0 迁出后，主项目代码和样例不保留双格式实现；迁出位置和迁回方法写入单独迁移说明文档。
- [ ] Momotalk 资源准备可并行，但导入 Unity 前必须完成授权归档。
- [ ] TTS 接口阶段允许使用 MockTTS 验收。
- [ ] ASR 接口阶段允许使用 MockASR 验收。
- [ ] MemoryJudge 只面向 StagePlan 2.0 对话结果。
- [ ] 聊天记录、记忆、缓存等持久化路径在对应阶段开始前再讨论确认。

## 阶段 2.0：二阶段开发约定与基线确认

**阶段目标**

确认第二阶段开发方式、继承边界和验收节奏，为后续阶段提供清晰基线。

**前置条件**

- [x] 第一阶段阶段 0-10 已完成并通过验收。
- [x] 已阅读 `ReadFirst.md`。
- [x] 已阅读 `README_Stage1.md`。
- [x] 已阅读 `virtual_partner第二阶段开发方向细节文档.md`。

**开发任务**

- [x] 确认第二阶段总目标和明确不做内容。
- [x] 确认第二阶段继续继承第一阶段 Runtime 原则。
- [x] 确认后续每个阶段开始前都先单独讨论。
- [x] 确认 TODO 只保留阶段级任务，不提前写死实现细节。
- [x] 确认 README 阶段归档规则。

**手动验收标准**

- [x] 根目录存在 `DevelopmentTODO_Stage2.md`。
- [x] 根目录存在 `README.md` 文档入口。
- [x] 根目录存在 `README_Stage1.md` 一阶段归档。
- [x] 文档明确 StagePlan 2.0 是第二阶段主格式。
- [x] 文档明确 timeline 1.0 迁出前必须 Git 备份或 tag。

**验收记录**

- 用户已确认 Stage 2.0 文档基线验收通过。
- 已确认本阶段只涉及文档基线，不改 Unity Runtime，不创建 `README_Stage2.md`。

**完成状态**

- [x] 阶段完成。

## 阶段 2.1：CharacterProfile / RuntimeContext 最小预留

**阶段目标**

建立最小角色配置和运行时上下文入口，避免 Momotalk、Voice、Memory、Prompt 等后续系统继续硬编码 Toki。

**前置条件**

- [x] 阶段 2.0 已完成。
- [x] 已确认当前仍以 Toki / CH0187 为主要角色。
- [x] 已确认本阶段只做多角色架构预留，不做多角色同时在场调度。

**开发任务**

- [x] 建立当前阶段所需的最小角色静态配置入口。
- [x] 建立当前阶段所需的最小角色运行时上下文入口。
- [x] 支持当前场景中的 Toki 注册为可交互角色。
- [x] 为后续 Momotalk 联系人、Prompt、Voice、Memory 分发提供角色 id 来源。
- [x] 保持实现轻量，不提前开发完整多角色调度系统。

**手动验收标准**

- [x] Play 后能确认 Toki 被注册为当前可交互角色。
- [x] 后续系统可通过角色 id 获取当前角色上下文。
- [x] 当前 Runtime 闭环不因最小角色预留而退化。
- [x] 未加入多角色群聊、角色间对话或多角色调度。

**验收记录**

- 用户已确认 Stage 2.1 手动验收通过。
- 已新增最小 `CharacterProfile`、`CharacterRuntimeContext`、`CharacterRegistry` 和 `CharacterRuntimeBinder`。
- 已确认 `VirtualPartnerBootstrap` 可注册 Toki，`CharacterRegistry` 可通过 `toki` 查询当前角色上下文。
- 已确认 RuntimeContext 当前只作为查询入口，不接管现有 Runtime 行为。

**完成状态**

- [x] 阶段完成。

## 阶段 2.2：StagePlan JSON 2.0 本地解析与校验

**阶段目标**

建立 StagePlan 2.0 的本地解析和校验能力，为替代 timeline 1.0 打基础。

**前置条件**

- [x] 阶段 2.1 已完成。
- [x] 已确认 StagePlan 2.0 是第二阶段唯一主格式。
- [x] 已确认本阶段只做本地 JSON 解析和校验，不接 LLM。

**开发任务**

- [x] 支持读取或输入本地 StagePlan 2.0 JSON。
- [x] 支持校验 schemaVersion、type、stages 和 stage actions 基础结构。
- [x] 支持每个 stage 最多一个 speech 的校验。
- [x] 支持结构性错误拒绝整条 StagePlan。
- [x] 支持局部 action 错误记录 warning，并允许合法 action 继续参与后续执行。
- [x] 准备本地 StagePlan 2.0 样例用于后续执行验证。

**手动验收标准**

- [x] 合法 StagePlan 2.0 JSON 能通过校验。
- [x] 缺少关键结构的 StagePlan 会被拒绝。
- [x] stage 内多个 speech 会被明确识别为非法。
- [x] 非法局部 action 能产生清晰 warning。
- [x] 本阶段不要求播放动作。

**验收记录**

- 用户已确认 Stage 2.2 手动验收通过。
- 已新增 StagePlan 2.0 DTO、Validator、独立 Debug 面板和 Basic / Full 本地样例。
- 已确认 Basic / Full 样例可通过本地校验，缺关键结构、多 speech、timeline 1.0 字段和非法局部 action 均能被识别。
- 已确认 StagePlan 2.0 标准格式不包含 `stageId`，Runtime 后续按数组顺序生成 `stageIndex`，LLM 不负责命名 stage。
- 已确认本阶段只做校验，不播放、不接 LLM、不替换 timeline 1.0 链路。

**完成状态**

- [x] 阶段完成。

## 阶段 2.3：StagePlanPlayer 本地执行闭环

**阶段目标**

用 StagePlan 2.0 阶段式执行模型跑通本地播放链路，替代一阶段绝对时间轴执行方式。

**前置条件**

- [x] 阶段 2.2 已完成。
- [x] StagePlan 2.0 本地校验可用。
- [x] 第一阶段 ActionCoordinator、SpeechBubble、Root、Locomotion 等链路仍可正常工作。

**开发任务**

- [x] 支持 stages 按顺序执行。
- [x] 支持 stage 内 action 同时开始。
- [x] 支持等待当前 stage 的 blocking action 完成后进入下一 stage。
- [x] 支持 speech、bonePose、animation、facing、locomotion 在 StagePlan 下复用现有 Runtime 链路。
- [x] 支持新 StagePlan 替换旧 StagePlan 的最小流程。
- [x] 保持 StagePlan 播放模块不直接写骨骼、不直接写 Root。

**手动验收标准**

- [x] 本地 StagePlan 能按 stage 顺序执行。
- [x] stage 内多个合法 action 能并行启动。
- [x] speech 不再依赖绝对 start / end 时间轴推进。
- [x] bonePose、animation、facing、locomotion 仍走现有协调链路。
- [x] 替换 StagePlan 时旧演出能按当前规则停止或交接。

**验收记录**

- 用户已确认 Stage 2.3 手动验收通过。
- 已新增独立 `StagePlanPlayer`，播放前复用 `StagePlanValidator.Validate(json, CharacterProfile)`。
- 已支持按数组顺序生成 `stageIndex`，并按 stage 顺序执行 StagePlan 2.0。
- 已支持 stage 内 action 同时启动，并以 `StageActionResult` 记录 `Completed`、`Failed`、`Interrupted`、`Skipped`、`OwnershipDenied` 等 terminal 结果。
- 已接入 `speech`、`bonePose`、`animation`、`facing`、`locomotion` 的现有 Runtime 链路；`expression` 本阶段按计划 warning no-op 并返回 `Skipped`。
- 已扩展独立 `StagePlanDebugPanel`，提供 `Play`、`Replace`、`Stop` 和播放结果计数。
- 已确认 Basic 样例可在 Play Mode 中启动并完成，`toki` 注册成功，Console 无 error。

**完成状态**

- [x] 阶段完成。

## 阶段 2.4：LlmRelay / Prompt 迁移到 StagePlan 2.0

**阶段目标**

让 LLM 只输出 StagePlan 2.0，并让 Unity 校验通过后执行 StagePlan。

**前置条件**

- [x] 阶段 2.3 已完成。
- [x] 本地 StagePlan 2.0 执行闭环稳定。
- [x] 已确认本阶段不再扩展新的二阶段表现功能。

**开发任务**

- [x] 将 LLM 输出要求改为 StagePlan 2.0 JSON。
- [x] 将 Prompt 模块迁移到 StagePlan 2.0 规则。
- [x] 注入当前角色可用能力时，保持白名单和结构化输出原则。
- [x] LLM 返回后先校验 StagePlan，再播放。
- [x] 保留 latest-wins 行为。
- [x] 确认 LLM 不输出 timeline 1.0 格式。

**手动验收标准**

- [x] 用户提交文本后，LLM 返回 StagePlan 2.0。
- [x] 合法 StagePlan 能驱动角色 speech 和动作。
- [x] 非法 StagePlan 能被清晰拒绝或局部 warning。
- [x] 连续提交消息时仍遵循 latest-wins。
- [x] Prompt 中不再要求 timeline 1.0。

**验收记录**

- 用户已确认 Stage 2.4 手动验收通过。
- 已确认 `LlmRelay` 从 timeline 1.0 输出链路迁移到 StagePlan 2.0 输出、提取、校验和播放链路。
- 已确认 Prompt 文件保留原 Unity 引用文件名，但内容已迁移为 StagePlan 2.0 规则，并明确禁止 `timeline`、`start`、`end`、`stageId`。
- 已确认 2.4 的目标角色由 `LlmRelay` 当前绑定的 `CharacterProfile` 决定，StagePlan JSON 不使用 `characterId` 分发。
- 已确认 latest-wins 行为：新请求刷新 requestId，旧 response 忽略，新合法 response 才 Replace 当前 LLM StagePlan，新非法 response 不中断当前播放。

**完成状态**

- [x] 阶段完成。

## 阶段 2.5：timeline 1.0 完整迁出

**阶段目标**

在 StagePlan 2.0 验收稳定后，将 timeline 1.0 从主项目迁出，避免双格式 Runtime 干扰后续开发。

**前置条件**

- [x] 阶段 2.4 已完成。
- [x] LLM 和本地测试均已使用 StagePlan 2.0。
- [x] timeline 1.0 迁出前已完成 Git 备份或 tag。

**开发任务**

- [x] 确认 timeline 1.0 相关代码、样例和 Prompt 的迁出范围。
- [x] 将 timeline 1.0 从主项目活跃代码和样例中迁出。
- [x] 确认主项目不保留双格式 Runtime。
- [x] 创建单独迁移说明文档，记录迁出位置和必要时迁回方法。
- [x] 清理因迁出产生的无效引用。

**手动验收标准**

- [x] Git 备份或 tag 已可追溯。
- [x] 主项目中不再保留 timeline 1.0 活跃执行路径。
- [x] 主项目中不再保留 timeline 1.0 样例作为当前格式样例。
- [x] StagePlan 2.0 本地和 LLM 闭环仍可运行。
- [x] 迁移说明文档能说明必要时如何找回旧实现。

**验收记录**

- 已确认迁出前存在 Git tag `stage2.4-approved`。
- 已将 timeline 1.0 Runtime 脚本和样例迁出到 `Archive/Timeline1_0/`，归档 README 明确说明归档仅用于追溯，恢复优先使用 Git tag。
- 已确认主项目活跃 Runtime、Prompt、Debug 文案和场景组件不再保留 timeline 1.0 正向执行路径。
- 已确认 `SampleScene` 无 Missing Script，Unity 编译无项目 error，StagePlan Debug 已并入 RuntimeDebug 的 StagePlan 页面。
- 用户已手动验收通过。

**完成状态**

- [x] 阶段完成。

## 阶段 2.6：Momotalk 参考资源与 UI 素材准备

**阶段目标**

为阶段 2.7 Momotalk UI 外壳准备参考归档、UI 美术资源清单、生成素材和 1080x1920 系预览图。手机外框、Loading、联系人首页以 Momotalk BWIKI 为主要参考；对话界面以 U1805/momotalk 为主要参考，目标是尽可能贴近参考效果。

**前置条件**

- [x] 阶段 2.5 已完成。
- [x] 已确认 Momotalk 手机外框、Loading、联系人首页主要参考 [Momotalk BWIKI](https://wiki.biligame.com/ba/Momotalk)。
- [x] 已确认 Momotalk 对话界面主要参考 [U1805/momotalk](https://github.com/U1805/momotalk)。
- [x] 已确认本阶段只做资源准备和预览验收，不实现 Unity Runtime UI 逻辑。

**开发任务**

- [x] 新建 `Archive/MomotalkVisualReference/README.md`，记录 BWIKI、U1805/momotalk、GitHub Pages 预览、访问日期和参考 commit。
- [x] 在归档文档中明确视觉分工：BWIKI 负责手机壳、Loading、联系人首页方向；U1805/momotalk 负责聊天页方向。
- [x] 准备 `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/` 下的 Textures、Icons、Fonts、Preview 资源目录。
- [x] 准备 `phone_frame.png` 时只保留 BWIKI 原始手机框效果，不再手绘或重做手机外壳。
- [x] 不输出 `phone_mask.png`，裁切范围只在归档文档中用坐标记录，避免辅助图被误当成 UI 素材。
- [x] 准备手机框、Loading、联系人项、聊天气泡、输入栏、状态提示等 UI 图片资源。
- [x] 从 `loading_icon.png` 上半部分裁出桃子小图标，并用于联系人首页标题区域。
- [x] 聊天页底部只保留单行输入栏：左侧麦克风按钮，中间输入框，右侧图片按钮和发送按钮；不保留下方角色圈和加号工具栏。
- [x] 准备发送、麦克风、图片、返回、更多、个人、爱心、选项、通知、添加等聊天页图标资源。
- [x] 准备 U1805/momotalk 参考字体文件或可用于 2.7 的字体导入说明。
- [x] 输出 `preview_loading.png`、`preview_contact_home.png`、`preview_chat.png`，并确认内容被压进 BWIKI 原手机框的屏幕区。
- [x] 在归档文档中记录哪些元素使用图片资源，哪些元素建议在阶段 2.7 直接用 Unity UI 绘制。

**手动验收标准**

- [x] `Archive/MomotalkVisualReference/README.md` 存在，并记录 BWIKI、U1805/momotalk、GitHub Pages、访问日期、参考 commit、视觉分工和阶段边界。
- [x] `VirtualPartner/Assets/VirtualPartner/UI/Momotalk/` 资源目录存在，并包含 Textures、Icons、Fonts、Preview 分区。
- [x] 核心资源存在：手机框、Loading、联系人项、聊天气泡、输入栏、发送图标、麦克风图标和未读红点。
- [x] `preview_loading.png` 明显贴近 BWIKI 手机 Loading 效果。
- [x] `preview_contact_home.png` 保持 BWIKI 手机应用壳与粉白氛围。
- [x] `preview_chat.png` 贴近 U1805/momotalk 对话页样式，包括字体、气泡、底栏和按钮风格。
- [x] PNG 资源能被 Unity 正常识别，适合后续设置为 Sprite。
- [x] 本阶段没有新增 Momotalk Runtime 脚本，没有实现右侧滑出、联系人切换、聊天记录、typing indicator 或消息发送逻辑。
- [x] 未生成或导入 Toki 头像，角色头像资源后续由用户提供。

**验收记录**

- 用户已确认 Stage 2.6 资源版本验收通过。
- 已确认 `phone_frame.png` 只保留 BWIKI 原始手机框效果，不保留伪手机壳或 `phone_mask.png` 辅助图资源。
- 已确认联系人首页左上角使用从 `loading_icon.png` 裁出的桃子小图标 + `MomoTalk` 标题。
- 已确认聊天页底部只保留单行输入栏：左侧麦克风、中间输入框、右侧图片和发送按钮；不保留下方角色圈和加号工具栏。
- 已确认本阶段只完成资源准备和预览图，不新增 Momotalk Runtime 脚本。

**完成状态**

- [x] 阶段完成。

## 阶段 2.7：Momotalk UI 外壳

**阶段目标**

建立可打开、可关闭、具备基础页面结构的 Momotalk 手机 UI 外壳。

**前置条件**

- [x] 阶段 2.1 已完成。
- [x] 阶段 2.6 的资源授权归档已完成，或本阶段明确只使用临时占位资源。
- [x] 已确认本阶段只做 UI 外壳，不接 LLM 文本闭环。

**开发任务**

- [x] 实现右侧手机或消息按钮。
- [x] 实现 9:16 手机面板。
- [x] 实现右侧滑出和关闭行为。
- [x] 实现 Loading 页面。
- [x] 实现联系人列表基础展示。
- [x] 实现 Toki 聊天页静态结构。
- [x] 保持 Momotalk UI 不直接控制骨骼或 Root。

**手动验收标准**

- [x] Play 后右侧出现 Momotalk 按钮。
- [x] 点击按钮后手机面板从右侧打开。
- [x] 手机面板比例和基础视觉接近目标参考。
- [x] 能在 Loading、联系人列表、聊天页之间切换。
- [x] 点击外部区域或关闭入口能关闭手机 UI。
- [x] 关闭 UI 不影响角色 Idle / FSM。

**验收记录**

- [x] 用户已确认 Stage 2.7 手动验收通过。

**完成状态**

- [x] 阶段完成。

## 阶段 2.8：Momotalk 文本聊天闭环

**阶段目标**

让 Momotalk UI 成为正式文本聊天入口，并将 StagePlan speech 按顺序同步为聊天气泡。

**前置条件**

- [x] 阶段 2.4 已完成。
- [x] 阶段 2.7 已完成。
- [x] StagePlan 2.0 LLM 闭环可用。

**开发任务**

- [x] 支持在 Momotalk 聊天页输入文本并发送。
- [x] 发送后立即显示用户消息。
- [x] 发送后调用当前目标角色的 LLM 流程。
- [x] 支持 typing indicator。
- [x] 支持 StagePlan speech 按 stage 顺序显示为角色消息气泡。
- [x] 支持基础聊天记录保存和恢复。
- [x] 支持关闭期间新消息未读提示。
- [x] 保持角色场景演出仍走 Runtime 链路。

**手动验收标准**

- [x] 用户可以通过 Momotalk UI 和 Toki 文本聊天。
- [x] 用户消息发送后立即显示。
- [x] 角色回复只来自通过 StagePlan 校验的 speech。
- [x] 多个 stage 的 speech 会按执行顺序显示为多条气泡。
- [x] 关闭 Momotalk 时新角色消息能产生未读提示。
- [x] 场景中的 Toki 演出仍由 StagePlan Runtime 执行。

**验收记录**

- [x] 用户已确认 Stage 2.8 手动验收通过。
- [x] 已确认 Momotalk 搜索、Chat Info 临时详情页、置顶、清除聊天记录和 Chat Info 返回按钮修复验收通过。

**完成状态**

- [x] 阶段完成。

## 阶段 2.9：嘴型 / 基础表情系统 V1

**阶段目标**

让 Toki 说话时具备基础嘴型反馈，并支持第一批白名单表情 action。

**前置条件**

- [ ] 阶段 2.3 已完成。
- [ ] 已确认 Toki 嘴型资源和材质槽可用于 8x8 嘴型图集切换。
- [ ] 已确认本阶段不做音素级口型。

**开发任务**

- [ ] 支持嘴型 index 切换。
- [ ] 建立基础嘴型配置。
- [ ] 建立基础表情白名单。
- [ ] 支持 expression action 通过 StagePlan 调用。
- [ ] 表情相关面部骨骼仍走 ActionCoordinator。
- [ ] 支持文本 fallback 嘴型驱动。
- [ ] 提供基础嘴型和表情调试能力。

**手动验收标准**

- [ ] 能手动测试嘴型 index 切换。
- [ ] Toki 说话时嘴巴有基础开合反馈。
- [ ] StagePlan expression action 能切换白名单表情。
- [ ] 表情结束后能回到 Idle 面部状态。
- [ ] Debug 嘴型覆盖优先级高于说话嘴型。
- [ ] 本阶段没有开放 LLM 直接控制真实嘴型 index。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.10：TTS 接口、降级与缓存框架

**阶段目标**

先建立 TTS 调用入口、播放入口、缓存框架和失败降级，允许使用 MockTTS 完成本阶段验收。

**前置条件**

- [ ] 阶段 2.8 已完成。
- [ ] 已确认本阶段不接真实语音克隆模型。
- [ ] 已确认 MockTTS 可作为本阶段验收方式。

**开发任务**

- [ ] 建立 Unity 侧 TTS 调用入口。
- [ ] 支持 TTS 服务状态展示。
- [ ] 支持 speech action 请求 TTS 或 MockTTS。
- [ ] 支持 AudioSource 播放入口。
- [ ] 支持 TTS 失败时降级到文本估算嘴型。
- [ ] 建立 TTS 缓存框架。
- [ ] Debug 能显示 TTS 当前状态和最新错误。

**手动验收标准**

- [ ] 使用 MockTTS 时，speech action 能等待模拟音频或模拟时长完成。
- [ ] MockTTS 完成后当前 stage 可以继续推进。
- [ ] TTS 失败时 StagePlan 和 Momotalk 气泡仍继续。
- [ ] 嘴型能在无真实音频时使用文本估算模式。
- [ ] Debug 能看到 TTS 状态。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.11：TTS 真实语音克隆接入

**阶段目标**

接入真实 TTS 语音克隆服务，让 Toki 的 StagePlan speech 可以播放角色语音。

**前置条件**

- [ ] 阶段 2.10 已完成。
- [ ] 已确认本地 TTS 服务候选和运行方式。
- [ ] 已准备可用于测试的 Toki 语音素材或 voiceId。

**开发任务**

- [ ] 优先测试 GPT-SoVITS 接入。
- [ ] 支持真实 TTS 服务健康检查。
- [ ] 支持 speech 文本生成音频并播放。
- [ ] 支持 TTS 结果进入缓存。
- [ ] 支持 speech action 等待真实音频播放完成。
- [ ] 支持嘴型驱动接入真实音频强度。
- [ ] 保持 TTS 失败时可降级。

**手动验收标准**

- [ ] Toki speech 可以合成为角色语音播放。
- [ ] 音频播放完成后 speech action 才完成。
- [ ] 嘴型能随真实音频强度开合。
- [ ] 缓存命中时能复用已有音频。
- [ ] TTS 服务不可用时对话链路不崩溃。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.12：ASR 接口与 Voice Mode UI

**阶段目标**

先建立 ASR 服务入口、Voice Mode UI 和失败降级，允许使用 MockASR 完成本阶段验收。

**前置条件**

- [ ] 阶段 2.8 已完成。
- [ ] 已确认本阶段不接真实 ASR 模型。
- [ ] 已确认 MockASR 可作为本阶段验收方式。

**开发任务**

- [ ] 在 Momotalk 输入区加入语音入口。
- [ ] 支持 Voice Mode 基础状态展示。
- [ ] 支持 ASR 服务状态展示。
- [ ] 支持启动、取消和结果接收的最小流程。
- [ ] 支持 FillInputOnly 和 AutoSendToLlm 配置。
- [ ] 支持 MockASR 返回文本。
- [ ] ASR 不可用时文本输入仍可用。

**手动验收标准**

- [ ] 点击语音入口能进入 Voice Mode。
- [ ] MockASR 能返回一段文本。
- [ ] FillInputOnly 模式下识别文本填入输入框。
- [ ] AutoSendToLlm 模式下识别文本能进入发送流程。
- [ ] ASR 不可用时 UI 能显示不可用状态，文本聊天不受影响。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.13：ASR 真实识别接入

**阶段目标**

接入真实本地 ASR 服务，让用户语音可以识别为文本并进入 Momotalk 输入流程。

**前置条件**

- [ ] 阶段 2.12 已完成。
- [ ] 已确认本地 ASR 服务候选和运行方式。
- [ ] 已确认当前优先测试 sherpa-onnx。

**开发任务**

- [ ] 优先测试 sherpa-onnx 接入。
- [ ] 支持真实 ASR 服务健康检查。
- [ ] 支持录音和 VAD 自动结束一轮识别。
- [ ] 支持识别结果填入输入框。
- [ ] 支持按配置自动发送给 LLM。
- [ ] 支持 ASR 失败降级。
- [ ] 如 sherpa-onnx 不满足需求，再讨论其他候选路线。

**手动验收标准**

- [ ] 点击麦克风后可以开始语音输入。
- [ ] 用户说话并静音后能得到识别文本。
- [ ] 识别文本能填入 Momotalk 输入框。
- [ ] AutoSendToLlm 模式能触发后续 StagePlan 对话。
- [ ] ASR 服务失败不会影响文本聊天。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.14：Markdown 长期记忆 V0

**阶段目标**

让角色自动记录长期有效信息，并在后续对话中注入当前角色的高优先级记忆。

**前置条件**

- [ ] 阶段 2.4 已完成。
- [ ] 已确认 MemoryJudge 只面向 StagePlan 2.0 对话结果。
- [ ] 已确认聊天记录、记忆、缓存等路径需要在本阶段开始前单独讨论。

**开发任务**

- [ ] 讨论并确认长期记忆存储路径和目录组织。
- [ ] 建立 MemoryJudge prompt。
- [ ] 支持在 StagePlan 2.0 对话完成后判断是否值得记忆。
- [ ] 支持校验记忆结果。
- [ ] 支持写入 Markdown 记忆。
- [ ] 支持读取当前角色 core / high 记忆并注入 Prompt。
- [ ] 提供 Memory Debug 能力。
- [ ] 保持聊天记录和长期记忆边界清晰。

**手动验收标准**

- [ ] 普通寒暄不会被随意写入长期记忆。
- [ ] 重要项目决策或用户偏好可以被写入 Markdown 记忆。
- [ ] 后续对话能读取并注入当前角色 core / high 记忆。
- [ ] MemoryJudge 不处理 timeline 1.0 或非 StagePlan 对话结果。
- [ ] Debug 能查看最新 MemoryJudge 结果和加载记忆。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。

## 阶段 2.15：Debug 整合与二阶段收尾

**阶段目标**

将第二阶段新增系统纳入统一 Runtime Debug 面板，并完成二阶段最终验收和 README_Stage2 归档。

**前置条件**

- [ ] 阶段 2.14 已完成。
- [ ] 第二阶段主要功能已经完成初步联调。
- [ ] 已记录各阶段发现的调试痛点。

**开发任务**

- [ ] 整合 Momotalk 状态观察。
- [ ] 整合 StagePlan 状态观察。
- [ ] 整合 Expression / Mouth 状态观察。
- [ ] 整合 TTS 状态观察。
- [ ] 整合 ASR 状态观察。
- [ ] 整合 Memory 状态观察。
- [ ] 整合 Character 状态观察。
- [ ] 进行二阶段整体手动验收。
- [ ] 生成 `README_Stage2.md`。

**手动验收标准**

- [ ] Play 后右侧出现 Momotalk 按钮。
- [ ] 打开手机 UI，可以进入 Toki 聊天页。
- [ ] 用户输入文本后，LLM 返回 StagePlan 2.0。
- [ ] StagePlan 按阶段执行，speech 不错位。
- [ ] Momotalk 气泡按 stage / speech 顺序出现。
- [ ] Toki 可以切换基础表情。
- [ ] Toki 说话时嘴巴贴图随语音或文本开合。
- [ ] TTS 可以用 Toki voiceId 合成并播放语音。
- [ ] ASR 可以识别用户语音，填入输入框或自动发送。
- [ ] Toki 可以自动记录长期记忆到 Markdown。
- [ ] 重新对话时能注入该角色 core / high 记忆。
- [ ] 当前场景中已绑定角色会出现在 Momotalk 联系人列表。
- [ ] Debug 面板能查看 Momotalk、StagePlan、ASR、TTS、Memory、Character 等关键状态。
- [ ] `README_Stage2.md` 已生成。

**验收记录**

- [ ] 待阶段完成后记录。

**完成状态**

- [ ] 阶段完成。
