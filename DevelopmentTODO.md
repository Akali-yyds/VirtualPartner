# VirtualPartner 第一版阶段性开发 TODO

本文档是第一版开发的长期跟踪清单，来源于 `virtual_partner开发方向文档.md`，但只保留阶段级目标、任务和验收标准。

开发原则以 `ReadFirst.md` 为准：简单、高效、直接；先讨论明确，再开发；只做当前阶段需要的内容。

## 执行节奏

- [ ] 进入某个阶段前，先讨论并明确该阶段的具体目标、边界和验收方式。
- [ ] 明确后再开发，不提前实现后续阶段内容。
- [ ] 开发完成后，通过 Play Mode、Inspector、日志和必要的资源检查进行手动验收。
- [ ] 验收通过后，勾选该阶段完成，再进入下一阶段。

## 阶段 0：项目开发约定与验收流程

**阶段目标**

固定后续开发协作方式，让每个阶段都能按“讨论、开发、验收、进入下一阶段”的节奏推进。

**前置条件**

- [x] 已阅读 `ReadFirst.md`。
- [x] 已阅读 `virtual_partner开发方向文档.md` 的总体方向。
- [x] 已确认 Unity 工程路径为 `VirtualPartner/`。
- [x] 已完成初始化 git 提交，阶段 1 开始前已有项目基线。

**开发任务**

- [x] 明确第一版固定角色为 Toki / CH0187。
- [x] 明确主模型入口使用 `Assets/Character/Toki/Prefabs/SourceParts/CH0187_Mesh.prefab`。
- [x] 明确后续每阶段开发前都要先单独讨论，不在 TODO 文档里提前写死代码细节。
- [x] 明确验收以 Unity 手动验证为主，自动化测试在具体阶段需要时再补充。

**手动验收标准**

- [x] 根目录存在 `DevelopmentTODO.md`。
- [x] 文档包含阶段 0 到阶段 10。
- [x] 每个阶段都有目标、任务和验收标准。
- [x] 文档没有提前规定未讨论过的类、字段或接口细节。

**验收记录**

- 初始化提交已确认：`e35fce8 项目初始化`。
- 当前计划变更仅涉及 `DevelopmentTODO.md`。
- 关键路径已确认存在：`VirtualPartner/`、`ReadFirst.md`、`virtual_partner开发方向文档.md`、`DevelopmentTODO.md`、`VirtualPartner/Assets/Character/Toki/Prefabs/SourceParts/CH0187_Mesh.prefab`。

**完成状态**

- [x] 阶段完成。

## 阶段 1：角色初始化与 Idle

**阶段目标**

角色能在场景中稳定加载，Play 后自动采集 BaseRotation，并能稳定播放 Idle。

**前置条件**

- [x] 阶段 0 已完成。
- [x] Toki 主模型 prefab 能在当前 Unity 工程中正常打开。
- [x] 当前场景中已有可用于观察角色的基础相机、灯光和场景环境。

**开发任务**

- [x] 接入 Toki 主模型入口。
- [x] 在 Play 后自动采集已登记骨骼的 BaseRotation。
- [x] 建立最低层 Idle 姿态来源。
- [x] 建立最终写入骨骼姿态的最小流程。
- [x] 保持本阶段写入流程只服务 Idle / BaseRotation 跑通，不提前实现完整 ActionCoordinator。

**手动验收标准**

- [x] 进入 Play 后，Toki 能正常显示。
- [x] 进入 Play 后，Idle 能持续播放或持续提供稳定姿态。
- [x] BaseRotation 采集结果可通过日志或 Inspector 确认。
- [x] 角色未出现明显骨骼错位、爆姿态或材质丢失。

**完成状态**

- [x] 阶段完成。

## 阶段 2：BoneMap 与 Debug 单骨骼控制

**阶段目标**

能用 Debug 面板单独控制语义骨骼，用于验证骨骼轴向、左右镜像和语义零点。

**前置条件**

- [x] 阶段 1 已完成。
- [x] BaseRotation 能稳定采集。
- [x] Idle 姿态能作为后续骨骼交接的目标参考。

**开发任务**

- [x] 建立语义骨骼到真实骨骼的映射配置。
- [x] 支持单个语义骨骼的 x / y / z 参数调试。
- [x] 支持 Zero，将骨骼置为相对 BaseRotation 的语义零点。
- [x] 支持导出当前调试姿态的 JSON 参数片段。
- [x] 预留 Debug 占用 / Release 接口，具体统一所有权交给阶段 3 实现。

**手动验收标准**

- [x] 能选择一个语义骨骼并单独调整。
- [x] Zero 后骨骼回到相对 BaseRotation 的语义零点。
- [x] 能导出当前调试姿态 JSON。
- [x] 左右骨骼能验证镜像关系，而不是同向平移式动作。

**完成状态**

- [x] 阶段完成。

## 阶段 3：ActionCoordinator 与骨骼交接过渡

**阶段目标**

建立统一的骨骼控制权管理和交接过渡，让不同动作源不会直接抢写骨骼 Transform。

**前置条件**

- [x] 阶段 2 已完成。
- [x] Debug 单骨骼参数控制和姿态导出可用。
- [x] Idle 姿态可以持续作为最低层姿态来源。

**开发任务**

- [x] 建立骨骼 owner 管理规则。
- [x] 支持动作源申请、占用、抢占和释放骨骼。
- [x] 支持 owner 变化时创建统一的骨骼交接过渡。
- [x] 正式接入 Debug 占用和 Release。
- [x] 先打通 Debug 与 Idle 之间通过 ActionCoordinator 的占用和释放流程。

**手动验收标准**

- [x] Debug 占用骨骼时，该骨骼不再被 Idle 覆盖。
- [x] Debug 释放骨骼时，骨骼能通过 ActionCoordinator 平滑回到当前 Idle 姿态。
- [x] 多次占用和释放后，角色姿态不累计漂移。
- [x] 日志或 Inspector 能辅助确认当前骨骼 owner。

**完成状态**

- [x] 阶段完成。

## 阶段 4：本地 JSON TimelinePlayer

**阶段目标**

先不接 LLM，用本地 JSON timeline 验证 Runtime 链路，支持文字气泡和骨骼参数动作。

**前置条件**

- [x] 阶段 3 已完成。
- [x] 骨骼控制权和交接过渡可以稳定工作。
- [x] 已明确本阶段只验证本地 JSON，不请求 LLM。

**开发任务**

- [x] 支持读取或输入本地 JSON timeline。
- [x] 支持校验 timeline 的基础结构和 action 类型。
- [x] 支持按 start / end 播放 timeline 段。
- [x] 支持 speech 显示和 bonePose 骨骼动作。
- [x] 支持新 timeline 替换旧 timeline 的最小流程。

**手动验收标准**

- [x] 合法本地 JSON 能驱动文字气泡显示。
- [x] 合法本地 JSON 能驱动指定骨骼动作。
- [x] 非法 action 局部失败时，不影响合法 action 继续执行。
- [x] 新 timeline 准备好后能替换旧 timeline，旧 speech 被清理。

**验收记录**

- 用户已确认阶段 4 手动验收通过。
- 已确认本阶段只验证本地 JSON timeline，不接 LLM。

**完成状态**

- [x] 阶段完成。

## 阶段 5：预设动画接入

**阶段目标**

通过配置调用白名单预设动画，并让预设动画与 bonePose 共用统一骨骼控制权规则。

**前置条件**

- [x] 阶段 4 已完成。
- [x] TimelinePlayer 能提交 action 请求。
- [x] ActionCoordinator 能处理骨骼申请和释放。

**开发任务**

- [x] 建立可调用预设动画的配置入口。
- [x] 支持采样预设动画姿态。
- [x] 支持 timeline 调用已登记的 animation。
- [x] 支持预设动画被更高优先级动作抢占时整体停止。

**手动验收标准**

- [x] 已登记动画可以通过 timeline 播放。
- [x] 未登记动画不会播放，并能看到清晰日志。
- [x] animation 与 bonePose 同时涉及相同骨骼时，不出现乱抢或双写。
- [x] 动画结束后，相关骨骼能回到 Idle 或后续动作。

**验收记录**

- 用户已确认阶段 5 手动验收通过。
- 已确认首个白名单预设动画 `CafeReaction` 可通过本地 timeline 调用。

**完成状态**

- [x] 阶段完成。

## 阶段 6：Root 转向与 Locomotion

**阶段目标**

角色能根据 facing 改变 Root 朝向，并通过 walk / run 沿当前 forward 位移。

**前置条件**

- [x] 阶段 5 已完成。
- [x] timeline 已能播放 speech、bonePose 和 animation。
- [x] 已确认本阶段只做简单 Root 转向和位移，不做复杂路径规划。

**开发任务**

- [x] 支持 facing action 改变 Root Y 轴朝向。
- [x] 支持进入用户交互状态时自动 TurnToCamera。
- [x] 支持 walk / run 位移动作。
- [x] 明确 locomotion 不包含 steps / direction，只沿当前 forward 按速度和时间移动。
- [x] 支持位移时采样对应动作姿态。
- [x] 支持按配置速度计算位移距离。

**手动验收标准**

- [x] facing 能让角色转向 camera、screenLeft、screenRight 等目标。
- [x] locomotion 能让角色沿当前 forward 移动，且不依赖 steps / direction 字段。
- [x] 需要改变移动方向时，先 facing 再 locomotion 的 timeline 能正确表现。
- [x] 用户交互入口只在从非交互进入交互时自动转向一次。

**验收记录**

- 用户已确认阶段 6 手动验收通过。
- 已确认 Root 转向、walk / run 位移、Root 保护、非法 timeline 校验和 Debug 抢占 locomotion 均符合阶段 6 目标。

**完成状态**

- [x] 阶段完成。

## 阶段 7：MovementConstraint

**阶段目标**

限制角色活动范围，让角色不会走出安全区域或进入障碍区域。

**前置条件**

- [x] 阶段 6 已完成。
- [x] walk / run 位移已经能稳定运行。
- [x] 已明确本阶段不做 NavMesh、寻路或复杂避障。

**开发任务**

- [x] 支持配置角色活动安全区域。
- [x] 支持配置障碍安全区域。
- [x] 在 locomotion 过程中检测根节点位置是否合法。
- [x] 非法时请求停止 locomotion。

**手动验收标准**

- [x] 角色接近活动区域边界时，locomotion 能停止。
- [x] 角色接近障碍区域时，locomotion 能停止。
- [x] 停止后角色留在当前位置，不自动回退、不重新规划。
- [x] 用户交互 locomotion 和 FSM locomotion 使用同一套停止逻辑。

**验收记录**

- 用户已确认阶段 7 手动验收通过。
- 已确认 RoomMoveArea / ObstacleArea 能限制 locomotion，非法移动时 Root 保持在上一合法位置。

**完成状态**

- [x] 阶段完成。

## 阶段 8：FSM 自主行为

**阶段目标**

角色在非用户交互状态下，能随机等待、随机执行空闲动作，并回到 Idle 循环。

**前置条件**

- [x] 阶段 7 已完成。
- [x] 预设动画和 locomotion 都能通过统一入口执行。
- [x] MovementConstraint 能停止非法 locomotion。

**开发任务**

- [x] 支持配置空闲等待时间范围。
- [x] 支持按权重抽取 FSM 动作。
- [x] 支持 FSM 调用预设动画。
- [x] 支持 FSM 调用 locomotion。
- [x] 支持用户交互打断 FSM 后重新从 Idle 开始。

**手动验收标准**

- [x] 非交互状态下，角色会等待一段随机时间后执行动作。
- [x] FSM 动作结束后，角色回到 Idle 并重新等待。
- [x] 用户提交消息后，FSM 停止当前动作。
- [x] 用户交互结束后，FSM 不恢复旧动作，而是重新开始空闲循环。

**完成状态**

- [x] 阶段完成。

## 阶段 9：LlmRelay 接入

**阶段目标**

用户输入文本后，请求 LLM 返回 JSON timeline，Unity 校验并执行该 timeline。

**前置条件**

- [x] 阶段 8 已完成。
- [ ] 本地 JSON timeline 链路已经稳定。
- [ ] 已明确 LLM 只输出受支持的 timeline action。

**开发任务**

- [ ] 支持用户文本输入和提交。
- [ ] 支持向 LLM 注入当前角色能力说明。
- [ ] 支持接收 LLM 返回的 JSON timeline。
- [ ] 支持校验通过后播放 LLM timeline。
- [ ] 支持 latest timeline wins 的替换规则。

**手动验收标准**

- [ ] 用户提交文本后，角色能显示回复气泡。
- [ ] 用户提交文本后，角色能执行 LLM 返回的动作。
- [ ] LLM 返回非法 action 时，合法部分仍可继续执行。
- [ ] 连续提交消息时，新 timeline 准备好后能替换旧 timeline。

**完成状态**

- [ ] 阶段完成。

## 阶段 10：调试工具补全与第一版打磨

**阶段目标**

补齐第一版演示所需的可观察性和调试工具，修复整体联调中的运行时细节。

**前置条件**

- [ ] 阶段 9 已完成。
- [ ] 用户输入到 LLM timeline 再到角色演出的最小闭环已跑通。
- [ ] 已记录前面阶段发现的调试痛点。

**开发任务**

- [ ] 补充 Runtime owner 观察能力。
- [ ] 补充 Timeline 播放状态观察能力。
- [ ] 补充常见错误日志。
- [ ] 整理第一版演示场景。
- [ ] 修复整体联调中暴露的最小必要问题。

**手动验收标准**

- [ ] 第一版最小闭环能稳定演示。
- [ ] 出现非法 timeline 或非法动作时，有足够日志定位问题。
- [ ] 能观察当前 timeline 播放状态和关键骨骼 owner。
- [ ] 未加入第一版明确排除的功能，如 TTS、长期记忆、复杂 UI、NavMesh。

**完成状态**

- [ ] 阶段完成。
