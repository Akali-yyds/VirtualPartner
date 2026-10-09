# 自然语言动作交互：路线评估

日期：2026-10-09。性质：调研与建议，不是已确认的新实施计划。本次未安装模型、下载权重或修改运行代码，未进行动作模型本机性能测试。

## 判断

当前最适合的产品架构是：LLM 理解语义、编排时间；Unity 提供身体与环境约束；本地目标控制处理即时、精确交互；经过验证的学习型动作生成器提供连续动作先验；最终仍由现有 ActionCoordinator 仲裁写入。

这不是现在同时开发两套完整系统。下一步优先获取两项决策证据：当前角色的重定向质量、轻量运动模型在本机的实际生成成本与动作正确率。保留已有日常链路，暂停扩展新的手写整套动作。

不能仅靠文档评选“效果最佳模型”。尚未取得相同提示、相同角色、相同硬件上的对照结果。

## 本项目事实

- Unity 6000.3.12f1；i7-9750H；约 16 GB RAM；GTX 1650 4 GB VRAM。
- PhoneOS 场景角色 Animator Avatar 有效但非 Humanoid，无 Animator Controller；场景检查没有 Rigidbody／ConfigurableJoint。当前主链路是运动学骨骼演出，不是物理策略控制。
- 已有 spatialPose、双骨链 IK、预检、保持与恢复、低优先级思考姿态、部分控制组成功及失败反馈。不能再按“仅有骨骼角度协议”的旧状况估算工作。
- SpatialMotionRuntime 的 60 Hz 全轨迹预检仍在调用线程同步完成，FK 递归计算并处理多个骨骼。需要 Profiler 才能判断其预算，不能因为数学上是 IK 就宣称当前实现开销很小。
- 末端位置、掌心、手指方向和肘提示仍主要由 LLM 输出。缺少依据语义目标选择姿态与路径的独立规划能力；拒绝越界不等于找到可行运动。
- 流式 StagePlan 已存在。LlmRelay 首完整 stage 打点在校验前；代码初始缓冲默认 2 个 stage。StagePlanPlayer 通常等待真实语音开始才启动同 stage 身体动作，prepare 可以提前执行。实际场景配置需与代码默认值区别。
- 10 月 8 日一次真实测试中，首 token 约 0.49–0.88 秒，首完整 stage 约 0.80–2.91 秒，成功呈现语音的场景约 6.57–25.80 秒。这是单轮受不同长度影响的样本，不是性能分位数。它提示主要等待值得从 TTS 与调度拆查，不能全归因于 JSON 或 API。

关键代码：

- `VirtualPartner/Assets/VirtualPartner/Runtime/LlmRelay.cs`：HandleStreamingStage、StartBufferedStreamingStages。
- `VirtualPartner/Assets/VirtualPartner/Runtime/StagePlanPlayer.cs`：同 stage 语音同步门控、空间动作派发。
- `VirtualPartner/Assets/VirtualPartner/Runtime/SpatialMotionRuntime.cs`：TryStartAtomic、Evaluate、Tick。
- `VirtualPartner/Assets/VirtualPartner/Runtime/SpatialPoseBuffer.cs`：FK 与 SolveLimb。
- `VirtualPartner/Assets/VirtualPartner/Runtime/VirtualPartnerStage1Bootstrap.cs`：Idle 采样 → Player → Spatial Tick → FinalizeFrame。
- `VirtualPartner/Assets/VirtualPartner/Runtime/TtsManager.cs`：StreamAndPlaySpeech 的预缓冲与启动条件。

## 路线取舍

| 路线 | 优势 | 限制 | 当前建议 |
|---|---|---|---|
| LLM 直接坐标／关节参数 | 兼容现有链路 | 不可靠几何推测，难保证自然度 | 保留底层兼容与 Debug，不继续作为主要语义接口 |
| 语义目标＋本地规划／约束 IK | 当前状态和场景目标可直接反馈，避免额外 GPU 服务 | 单独不足以产生丰富自然全身演出 | 在线基础能力 |
| 离线文本动作生成 | 最容易隔离验证自然度和重定向 | 不是连续交互或场景闭环 | 第一轮学习型能力实验 |
| 按当前姿态续生成的运行时模型 | 连贯运动与更灵活动作组合 | 起始条件、延迟、资源及打断成本 | 离线证据通过后优先方向 |
| 物理策略／强化学习 | 动态稳定与接触能力 | 需要新的模拟、控制与训练体系 | 长期独立研究 |

生成器提供候选运动，不直接写角色。重定向后检查目标误差、关节、足部和环境约束；小误差由局部求解修正，大幅改变动作意图的片段应拒绝。不能用 IK 大幅扭曲坏片段后宣称模型正确。

同一时刻下肢与骨盆必须保持关联所有权。不能随意把一个生成的下半身与另一个上半身叠加后假定平衡成立。保持／取消／恢复继续复用已有请求与占用机制。

## 官方来源核查与候选优先级

### MoMask：离线可行性基线

官方 README 确认 CPU Demo；gen_t2m.py 明确支持 gpu_id=-1。生成使用 20 FPS，提供关节位置 NPY 与 BVH。官方提供 Python 3.10 pip 安装路径，纯生成不要求完整训练数据集。

但默认生成脚本同时进行两次 100 迭代的关节位置到 BVH 转换（有／无 foot IK）。必须分开记录模型推理、转换与渲染，不能将后处理耗时算作模型速度，也不能只报模型速度忽略最终可播放耗时。CPU 可执行不等于在 i7-9750H 上低延迟。

定位：先证明“生成的新动作能正确到达当前角色”，不是默认最终在线模型。

### DiP：新增的在线候选

MDM 官方 DiP 文档说明：自回归、每次预测后续约 2 秒，10 步扩散（文档称 5 步亦可工作），固定 DistilBERT 文本编码器，并提供目标位置条件版本。它具备前段运动条件，方向上比每次独立生成完整片段更适合连续交互。

实际 sample.generate 示例会从数据集获取前缀，因此“使用我们的当前姿态”需要正确转换到源模型运动表示，不能把 Unity Quaternion 数组直接传入。目标条件能力也不等于任意接触／手部方向约束。本机显存、速度、Windows 依赖均未验证。

定位：与 MoMask 共享一小套基准动作；在基线绑定可行后，优先做短序列速度与续接试验。不是已确认优于其他模型的质量冠军。

### ARDY／Kimodo：能力参考与资源允许后的候选

ARDY 官方描述支持在线文本、root 路径、全身关键帧和稀疏关节位置／旋转条件；当前有 Core/G1，不应假设已有所有 SOMA 版本。官方主要测试 Ubuntu 22.04、RTX 4090、Python 3.11，文本编码器默认 CUDA bfloat16 约 14 GB VRAM，支持 CPU 路径。其预测帧数/FPS 表示生成时间跨度，不等于计算延迟。

Kimodo 官方说明整体 GPU 模式约 17 GB VRAM，CPU 文本编码可降到 3 GB 以下；输出局部／全局旋转、root、足接触标签，适合更完整重定向。CPU offload 会占系统内存且增加编码成本，在 16 GB RAM、4 GB VRAM 与 Unity/TTS 共存环境下仍有风险。不能把“低于 3 GB”当成整套系统可用的证明。

定位：如果允许远程 GPU，可提高其试验优先级；目前本机优先级低于轻量基线。相关代码 Apache-2.0，模型／数据另有条款。

### 其他

- MotionLCM：一／少步生成与约束控制有价值；官方原始环境 RTX 3090，本机成本未知；代码许可证限制非商业研究。可作研究比较，暂不首选产品主线。
- DART：自回归和交互约束研究有参考价值；官方性能分析环境为 RTX 4090、i7-13700K、64 GiB RAM、Ubuntu，不能套用其性能。
- HY-Motion：前次已核实官方最低显存 24／26 GB，不是本机首选。
- ProtoMotions：物理模拟与模仿学习框架，不是插进现有 Unity 项目就能工作的运动生成插件。

## 最小决策实验（建议，未执行）

1. 在独立测试场景建立源骨架／当前角色并排播放。先用一个已知正确的源运动验证重定向；它只是测试夹具，不作为产品动作答案。随后才测生成动作。
2. 对照当前坐标 IK、语义目标控制、MoMask 生成；DiP 先做单独的环境和短续接性能探针，避免一开始维护多个完整服务。
3. 固定 12 条语义测试，包括左右／高度／幅度变化、指向已存在对象、双臂配合、当前保持继续、打断与复位。每条多个固定种子／多次运行，全部记录，不挑最好片段。
4. 区分源生成是否正确与重定向后是否正确：人物本体左右、手掌朝向、动作次序、脚滑、关节异常、穿插、自然度、进入／退出连续性。
5. 冷启动与热推理分开测；分别跑模型单独运行、Unity 同时运行、Unity＋真实 TTS 同时运行。记录首可见反馈、首相关动作、首语音、整段完成、CPU/RAM/VRAM、Unity 帧时间 p50/p95。
6. 在线生成还需生成耗时低于输出片段时长并留有余量，首段启动也要单独达标。平均快于实时不足以保证没有尾延迟卡顿。
7. 若只达到异步生成，就明确作为较长演出入口，不冒充即时反馈；若重定向后无明显质量收益，或与语音并发无法运行，则不接入在线主链路。

本机第一优先级是重定向与测量基线，而不是先写通用 Python 动作服务、改 StagePlan 版本或安装多个大模型。对低延迟的改善并行从 TTS 首包和同步门控取证，不能靠思考动画掩盖正式响应耗时。

## 来源（2026-10-09 读取官方文件）

- https://github.com/EricGuo5513/momask-codes
- https://github.com/EricGuo5513/momask-codes/blob/main/gen_t2m.py
- https://github.com/GuyTevet/motion-diffusion-model/blob/main/DiP.md
- https://github.com/GuyTevet/motion-diffusion-model/blob/main/sample/generate.py
- https://github.com/nv-tlabs/ardy
- https://github.com/nv-tlabs/kimodo
- https://github.com/Dai-Wenxun/MotionLCM
- https://github.com/Dai-Wenxun/MotionLCM/blob/main/LICENSE
- https://github.com/zkf1997/DART
- https://github.com/NVlabs/ProtoMotions

GitHub 元数据 API 请求遇到限流／连接错误，正文通过 raw 官方 README／源码读取；因此未宣称已核验仓库完整提交历史或最新发布版本。性能结论区分官方说明、本机既有记录与尚待实测。
