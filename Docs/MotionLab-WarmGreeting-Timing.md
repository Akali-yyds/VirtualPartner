# 预热状态下问候链路计时

日期：2026-10-11。输入：`早上好呀`。最终有效样本：`F:/Project/MotionExperiments/Evidence/2026-10-11-WarmGreeting-v3`。

## 测试条件

真实 deepseek-chat 输出中文回复与英文动作描述；MoMask GPU 模型先加载并预热 40/80/160 帧，GPT-SoVITS 沿用原 CPU 配置并完成真实合成预热。所有服务启动与预热不计入计时。Unity 实验场景已进入 Play Mode。LLM 使用专用短提示和隔离上下文；未经过日常 Momotalk / StagePlan 完整提示与调度，不能当成日常聊天性能。

LLM 本次回复：**早上好呀，很高兴见到你！**

LLM 本次动作描述：`A person stands upright, gently shifts weight to one leg, and slightly tilts the head with a relaxed posture.`；选择 4 秒动作。MoMask 种子 11，18 步，无 BVH 拟合。动作就绪即执行，TTS 与 MoMask 在 LLM 完整 JSON 解析后并行开始。未强行同步动作与声音。

## 分阶段耗时

| 阶段 | 耗时 |
|---|---:|
| LLM 请求 → 客户端首个内容 token | 0.863 秒 |
| LLM 请求 → 完整输出 | 1.184 秒（包含首 token 等待） |
| MoMask 开始 → 动作文件生成完毕 | 2.021 秒 |
| 其中：MoMask 文本编码 | 0.692 秒 |
| 其中：生成/解码/源骨架恢复，不含文本编码 | 1.287 秒 |
| 其中：其余准备与导出 | 约 0.043 秒 |
| Unity 动作文件载入/校验 | 0.020 秒 |
| Unity 开始载入 → 首个协调器控制的身体动作 | 0.037 秒 |
| TTS HTTP 请求 → 完整 WAV 返回 | 9.995 秒 |
| WAV 返回 → Unity AudioSource 播放开始 | 0.023 秒 |

TTS 现有日志（按源码计时点解释）：参考音频/提示处理约 0.000 秒；目标文本预处理/分批约 1.552 秒；语义 token 预测约 2.865 秒；音频合成约 4.686 秒。与 HTTP 总耗时的剩余约 0.892 秒未进一步细分，可能包含其他处理与传输，不能全部归因于网络。

## 从用户提交开始的实际时间线

| 事件 | 提交后 |
|---|---:|
| LLM 完整回复可用 | 1.194 秒 |
| TTS 启动 | 1.204 秒 |
| MoMask 完成 | 3.220 秒 |
| 首个身体动作 | **3.260 秒** |
| 首次真实音频播放 | **11.270 秒** |
| 动作与语音全部结束 | 13.541 秒 |

MoMask 和 TTS 并行，所以不能把两者耗时相加当总等待。此次短回复低于现有 28 字符流式阈值，走完整 WAV 路径，音频约 2.28 秒。若要主要动作与语音同时开始，语音仍是此样本的较晚就绪项；未将这个推断称为实际同步策略复测。

## 验证与边界

- `completed=true`、`realAudio=true`、`cached=false`、`GptSoVits`，真实 HTTP 200，未用文字或 Mock 代替语音。
- 全请求阶段 Unity 帧时间 P50 5.32 ms、P95 6.75 ms、最大 36.98 ms；计时过程中未录屏。结束后单独保存截图。
- 首轮存在四元数范数累积误差导致 TRS 日志刷屏。仅在实验 `MotionLabRetargeter` 的临时姿态缓冲归一化，不修改模型输出、不调整动作角度、不改动日常空间求解器。最终轮自触发起新增该错误 0、C# 编译错误 0、Exception 0，UI Console 错误 0。
- 第二轮仍用了未刷新的旧程序集，并命中语音缓存，排除。首轮及第二轮证据分别保留在 `2026-10-11-WarmGreeting` 和 `2026-10-11-WarmGreeting-v2`，不与最终有效样本混算。
- 实验使用独立运行时 CharacterProfile 和 VoiceProfile 作为缓存身份；不删除旧缓存或用户记录。LLM 配置只读，不记录密钥。
- Python 和 Unity 事件通过本机 UTC 毫秒时间关联；主要端到端数字来自 Unity 单调时钟。loopback 观察代理逐块转发 TTS 返回，记录 HTTP 首字节和完成，不改动 TTS 服务。
- 最终结果是一个有效并发样本，不能代表中位数、P95 或稳定上限；MoMask 并发耗时明显高于此前单独生成，尚不能把差值全部归因于某一种资源竞争。

## 复现入口

`Tools/MotionLab/warm_chain_probe.py --output <全新绝对目录> --config <既有配置文件>`：常驻模型、预热、真实 LLM 和本机 TTS 计时代理（8766，转发既有 8765）。

确认 Unity 导入最新 `MotionWarmChainProbe`，且 TTS 已预热后，在项目 `Library/MotionLab/warm-chain-command.json` 写入 `{"root":"<输出绝对目录>"}`。仅此显式文件触发实验；脚本等 ready.json 后打开独立场景并提交固定问候。结束恢复临时 TTS 配置、释放动作占用并停止本轮 worker。原始事件在输出目录，计时脚本不读取历史和 Memory。
