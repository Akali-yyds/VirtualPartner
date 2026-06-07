# VirtualPartner 后续优化事项

更新时间：2026-06-07

本文记录已识别但暂未实施的优化点。每项包含背景、根因、建议方案和当前状态，便于后续按优先级排期。

## ASR 标点恢复（识别结果加标点）

- 状态：待办（已确认方案，暂缓实施）。
- 现象：连续说两句话、中间有换气停顿时，识别结果把两句拼成一串无标点的连续文本，缺少应有的逗号/句号。
- 根因：当前 ASR 模型 `sherpa-onnx-streaming-zipformer-ctc-zh-int8-2025-06-30` 的 `tokens.txt` 共 2002 个 token，其中标点 token 为 0。该 CTC 模型从设计上只输出连续汉字文本，不产生任何标点。换气停顿若短于 silero VAD 的 `min_silence_duration`（0.8s），VAD 会正确地把整段话当成一个语音段（期望行为），叠加模型无标点，最终就是无标点连续文本。
- 建议方案：增加独立的标点恢复（punctuation restoration）后处理。
  - 模型：`sherpa-onnx-punct-ct-transformer-zh-en-vocab272727-2024-04-12`（CT-Transformer，约数百 MB）。
  - API（已在 venv `sherpa-onnx==1.13.2` 验证可用）：`OfflinePunctuation` + `OfflinePunctuationModelConfig(ct_transformer=...)`，方法 `add_punctuation(text)`。
  - 集成点：
    1. `download_models` 脚本增加下载到 `models/punct/`。
    2. `config.json` 增加 `punctuation` 段（`model_path` + `enabled` 开关）。
    3. 服务启动时与识别器一起后台预热标点模型；`decode_session_async` 解码出文本后调用 `add_punctuation` 再发布结果。
    4. 优雅降级：模型缺失或 `enabled:false` 时，按现状返回无标点文本，不报错。
- 代价/注意：新增数百 MB 模型下载、约 1-2s 额外预热；标点是基于文本语法推断，不是直接按声学停顿，但通常与自然停顿吻合。

## TTS 模块拆分（架构重构）

- 状态：进行中。阶段一已完成（2026-06-07）；阶段二待办。
- 背景：`TtsManager` 约千行，单类同时承担 Mock provider、真实 buffered、真实流式、text fallback、健康检查、缓存、WAV 写盘、PCM ring buffer、嘴型协调、Inspector 状态展示，违反单一职责。
- 阶段一（已完成，Unity 编译通过）：
  - `StreamingPcmBuffer` + `StreamingPcmDownloadHandler` 提取到 `Runtime/TtsStreamingPcmBuffer.cs`，改为可单测的顶层公共类。
  - PCM16 WAV 写入提取到 `Runtime/TtsWavWriter.cs`（`TtsWavWriter.WritePcm16Wav`）。
  - `TtsManager` 移除上述嵌套类与 WAV 字节写入私有方法。
- 阶段二·缓存提取（已完成，Unity 编译通过，2026-06-07）：
  - 新增 `Runtime/TtsCache.cs`：`TtsCacheInfo` 结构 + `TtsCache` 静态类，承担缓存 key/path 推导（`BuildInfo`）和原子写盘（`TryWriteBytes` / `TryWritePcm16Wav`）。
  - `TtsManager` 的 `BuildCacheInfo` 改为薄包装委托给 `TtsCache.BuildInfo`；移除 `Hash` / `SanitizePathSegment` / `GetProjectUserDataRoot` / `TryWriteCacheFile` / `TryWritePcm16WavCacheFile` 及 `System.Security.Cryptography` 引用。
  - 行为保持：缓存 key/path 推导逻辑逐字迁移，既有缓存文件仍命中。
- 阶段二·剩余（**暂缓，按需触发**）：把 Mock / GptSoVits / fallback 行为拆到 `ITtsProvider` 策略实现，并抽出 `TtsAudioStreamPlayer`（封装 streaming AudioClip 播放与欠载处理）。
  - 暂缓理由（2026-06-07 评估）：这是纯架构重构，不改变运行时行为（不提速、不加功能、不修 bug），收益仅在可维护性/可测性/可扩展性。当前只有一个真实引擎 GptSoVits（+ Mock + fallback），策略抽象属于为尚未发生的扩展提前买单；而该改动需重组约 30 个状态字段和 3 条协程流，风险最高且仅编译通过不足以保证行为正确，必须 Play Mode 全回归。高风险 + 低当前收益。
  - 触发条件：当确实要新增第二个 TTS 引擎（如换本地模型或接云端 TTS），或该类的维护成本变得明显时，再以分阶段 spec（requirements → design → tasks，参照 `request-lifecycle` 做法）推进。
  - 注：阶段一与阶段二·缓存提取已经拿到大部分现实收益（buffer/wav/cache 已独立可测），核心状态机暂留在 `TtsManager`。

## UnityWebRequest 失效路径显式 Dispose

- 状态：已完成（2026-06-07，Unity 编译通过）。
- 背景：`TtsManager` 协程被 `StopCoroutine` 中断时，`using` 的 `Dispose` 不执行。
- 实施：`AbortActiveRequest` 现在 Abort 后显式 `Dispose` 并置空。abort 仅在 StopCoroutine 中断（using 不会再跑）或无在途请求时发生，因此不会与正常完成路径的 using 双重 Dispose。
- 待 Play Mode 确认：连续发声、打断、清空记忆等场景下 TTS 行为正常，无异常日志。

## ASR VAD 阈值自适应（可选）

- 状态：观察中。
- 背景：silero VAD 已接入，分段质量取决于 `config.json` 的 `vad.threshold` / `min_silence_seconds` / `min_speech_seconds`。当前为固定值。
- 建议方案：若在不同麦克风/噪声环境下分段不稳，再考虑加入底噪自适应或暴露更易调的参数。先以实际使用反馈驱动，不提前优化。
