# MoMask 临时输入试验

2026-10-11。用于人工测试动作效果与暖运行延迟，不是日常 Momotalk 接入。

## 使用

Unity 菜单 `VirtualPartner > Motion Lab > Open MoMask playground` 打开独立 `MotionLab` 场景并进入 Play Mode。当前场景有未保存修改时先保存；若正在日常场景运行，先退出 Play Mode。

首次启动加载模型并预热 2、4、8 秒长度，等待服务显示 `READY`。左侧输入指令，选择长度与种子，再点击“生成并演出”。角色与源骨架并排显示。“中断 / 复位”立即停止播放并使待完成结果失效；“重播”复用最后成功样本，不重新调用模型。退出 Play Mode 结束本面板启动的模型进程。

- 默认中文模式：使用现有本机 LLM 配置，把当前指令转换为英文动作描述，再调用 MoMask。面板展示实际英文输入；转换耗时单独计时。
- 取消中文模式：直接输入英文动作描述，不调用 LLM。中文输入会明确报错。
- 固定种子便于对比描述，随机种子便于观察稳定性。时长是动作片段长度，不是生成等待时间。
- 不调用 TTS，不发送历史、不写长期记忆、不保存或修改服务配置。普通问候在中文模式下会转成适合的简单身体回应。

## 计时与文件

`F:/Project/MotionExperiments/Evidence/Playground/<session>/jobs/<request>/` 保存输入、英文描述、原始动作、转换动作和诊断。面板“打开结果目录”可直接访问。加载与预热在 `warmup` 下，不计入请求耗时。

- `llm.json`：当前独立提示与原始回复、首 token、转换完成耗时；不含密钥。
- `generation.json`：官方模型推理细分；18 步，20 FPS 输出，本面板不执行额外 BVH 拟合。
- `result.json`：LLM 和 MoMask 耗时；MoMask 数值含暖模型调用、坐标转换及导出，非纯 GPU 推理。
- `interactive-timing.json`：Unity 提交到动作协调器实际相关骨骼首次变化的时间；不把 Idle 或等待姿态算作正式动作。
- `playback-*.json`：角色播放与重定向诊断。重播不会覆盖首次生成的端到端时间。

中断正在生成的请求会禁止其迟到结果播放；模型当前计算可能仍需完成，然后服务恢复 READY。退出 Play Mode 会终止本面板拥有的 Python 进程树。失败结果保留，不以预设动作替换。

## 本次检查

- Python 输入边界与既有导出契约共 6 项测试通过；Unity 编译及 Console 无新增错误。
- 英文直接路径真实运行：MoMask 0.566 秒，提交至首次身体动作 0.635 秒。
- 中文路径真实运行：LLM 0.993 秒、MoMask 0.710 秒、首次身体动作 1.764 秒；修复面板显示后复测分别为 0.668 / 0.616 / 1.369 秒。
- 以上是少量暖运行样本，不是延迟保证。未启动 TTS，不能与语音并发测试直接比较。
- 检查提交立即取消、真实 LLM 请求中取消、重播及进程常驻；取消结果为 CANCELLED，未发生迟到播放。请求中退出 Play Mode 后已确认实际 Python worker 退出。Game View 检查修复了相机仅清除右侧视口导致的 IMGUI 文字残影。
- 动作意图、自然度、中文输入法和不同 Game View 大小下的操作体验仍需用户实机确认。现有重定向的脚滑、掌心扭转等限制仍存在。

## 实现位置与边界

新增 `MoMaskPlayground.cs` 临时运行面板、`MoMaskPlaygroundLauncher.cs` 菜单、`momask_playground.py` 常驻文件队列 worker 与 `test_playground.py` 输入边界测试。面板只在编辑器独立实验场景中临时挂载，不保存到日常场景；仍复用既有 `MotionLabDriver`、MoMask 适配器及 `ActionCoordinator`。Momotalk、LlmRelay、StagePlan、Memory、TTS/ASR 的生产链路未改变。
