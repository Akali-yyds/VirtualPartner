# VirtualPartner

VirtualPartner 是一个 Unity 桌面虚拟陪伴角色原型项目。当前主角色是 Toki，项目目标是让用户通过类 Momotalk 的手机界面与角色进行文本和语音交流，并由 LLM 输出结构化 StagePlan 来驱动角色说话、动作、表情、嘴型、语音播放和长期记忆。

当前仓库包含 Unity Runtime、本地 TTS/ASR 包装服务、阶段开发文档和验收记录。第二阶段主线已经完成到“正式交互入口 + 语音输入输出 + 基础表情嘴型 + Markdown 长期记忆 + 统一 Debug 面板”的可体验版本。

## 当前能力

- **Momotalk 交互入口**：右侧手机入口、联系人列表、Toki 聊天页、文本输入、typing indicator、未读提示和聊天记录恢复。
- **StagePlan 2.0 执行模型**：LLM 只返回 JSON StagePlan，Unity 校验后按 stage 顺序执行 `speech`、`bonePose`、`animation`、`facing`、`locomotion`、`expression`。
- **角色动作与调度**：保留第一阶段的骨骼控制、Root 朝向、locomotion、预设动画、FSM 自主行为和 ActionCoordinator owner 机制。
- **嘴型与基础表情**：支持 8x8 mouth index 切换、基础 expression 白名单、文本 fallback 嘴型和真实音频 RMS 嘴型。
- **TTS 语音输出**：Unity 通过本地 TTS wrapper 调用 GPT-SoVITS；成功时播放角色语音并等待音频完成，失败时降级到文本时长和文本嘴型。
- **ASR 语音输入**：Unity 通过本地 ASR service 使用 sherpa-onnx 识别系统麦克风输入；结果可填入输入框或自动发送给 LLM。
- **Markdown 长期记忆**：角色可在 StagePlan 对话完成后自动判断是否写入长期记忆，并在后续 prompt 中注入当前角色 `core/high` 记忆。
- **统一 Runtime Debug**：集中查看 LLM、StagePlan、Momotalk、TTS、ASR、Memory、Character、FSM、Root、Bone、Expression/Mouth 等状态。

## 核心流程

```text
User text / ASR text
-> MomotalkConversationController
-> LlmRelay
-> LLM returns StagePlan 2.0 JSON
-> StagePlanValidator
-> StagePlanPlayer
-> Runtime executors
   -> speech bubble / Momotalk bubble
   -> TTS / fallback speech timing
   -> mouth driver / expression
   -> bone / animation / facing / locomotion
-> MemoryJudge after completed LLM StagePlan
-> Markdown memory + future prompt injection
```

LLM 不直接控制 Unity Transform，也不直接写骨骼或 Root。所有行为都必须通过 Runtime 支持的结构化 action、白名单配置和本地校验。

## 仓库结构

```text
.
├─ VirtualPartner/                     Unity project
│  ├─ Assets/VirtualPartner/Runtime/    Core runtime scripts
│  ├─ Assets/VirtualPartner/Profiles/   Character and behavior profiles
│  ├─ Assets/VirtualPartner/Prompts/    Prompt modules
│  ├─ Assets/Scenes/SampleScene.unity   Main test scene
│  ├─ LocalServices/TTS/                GPT-SoVITS wrapper service
│  ├─ LocalServices/ASR/                sherpa-onnx ASR service
│  ├─ UserSettings/                     Local private settings
│  └─ UserData/                         Runtime data, ignored by git
├─ Archive/                             Archived references and old timeline material
├─ README_Stage1.md                     Stage 1 archive
├─ README_Stage2.md                     Stage 2 archive
├─ DevelopmentTODO.md                   Stage 1 TODO and acceptance record
├─ DevelopmentTODO_Stage2.md            Stage 2 TODO and acceptance record
└─ ReadFirst.md                         Collaboration and development rules
```

## Requirements

- Unity `6000.3.12f1`
- Windows local development environment
- A Chat Completions compatible LLM endpoint
- Optional for real TTS: GPT-SoVITS `api_v2.py`
- Optional for real ASR: Python environment for `sherpa-onnx`, `sounddevice`, `numpy`

Mock/fallback paths exist for parts of the runtime, but the current full experience expects the Unity scene, LLM config, TTS wrapper and ASR service to be configured locally.

## Quick Start

1. Open `VirtualPartner/` with Unity `6000.3.12f1`.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Create local LLM config at `VirtualPartner/UserSettings/VirtualPartnerLlmConfig.json`.
4. Enter Play Mode.
5. Click the Momotalk button on the right side of the screen.
6. Open Toki chat and send a message.
7. Use `VirtualPartner Runtime Debug` to inspect LLM, StagePlan, TTS, ASR, Memory and character state.

Local private files under `VirtualPartner/UserSettings/` and generated runtime files under `VirtualPartner/UserData/` are ignored by git.

### LLM Config Example

```json
{
  "apiKey": "YOUR_API_KEY",
  "model": "YOUR_MODEL",
  "baseUrl": "https://your-compatible-endpoint.example",
  "useJsonResponseFormat": true,
  "supportsDeveloperRole": true,
  "interactionTimeoutSeconds": 10
}
```

You can also use `chatCompletionsUrl` instead of `baseUrl` if your endpoint does not follow `/v1/chat/completions`.

## Local Services

### TTS

Stage 2 uses a small local wrapper so Unity does not depend on GPT-SoVITS API details directly.

1. Start GPT-SoVITS `api_v2.py` on `127.0.0.1:9880`.
2. Start the wrapper in `VirtualPartner/LocalServices/TTS/`.

```bat
cd VirtualPartner\LocalServices\TTS
python tts_service.py
```

Wrapper default URL:

```text
http://127.0.0.1:8765
```

More details: [`VirtualPartner/LocalServices/TTS/README.md`](./VirtualPartner/LocalServices/TTS/README.md)

### ASR

Stage 2 uses a local sherpa-onnx service for microphone recording, VAD and recognition.

```bat
cd VirtualPartner\LocalServices\ASR
setup_venv.bat
download_models.bat
start_asr_service.bat
```

Service default URL:

```text
http://127.0.0.1:8766
```

More details: [`VirtualPartner/LocalServices/ASR/README.md`](./VirtualPartner/LocalServices/ASR/README.md)

## Runtime Data

Generated user/runtime data is kept out of git:

```text
VirtualPartner/UserData/ChatHistory/{characterId}.json
VirtualPartner/UserData/Memory/{characterId}/*.md
VirtualPartner/UserData/TTSCache/{characterId}/*.wav
```

Chat history is a chronological log. Markdown memory is filtered long-term information selected by MemoryJudge. TTS cache contains generated audio files.

## Development Notes

Project development follows [`ReadFirst.md`](./ReadFirst.md):

- simple, efficient, direct
- discuss and confirm before implementing a stage
- keep changes scoped to the current goal
- prefer high cohesion and low coupling
- verify manually through Play Mode, Inspector, Debug panels and logs

Stage history:

- [`README_Stage1.md`](./README_Stage1.md): first-stage archive
- [`README_Stage2.md`](./README_Stage2.md): second-stage archive
- [`DevelopmentTODO.md`](./DevelopmentTODO.md): first-stage TODO and acceptance
- [`DevelopmentTODO_Stage2.md`](./DevelopmentTODO_Stage2.md): second-stage TODO and acceptance

## Current Status

The Stage 2 Unity experience is implemented and archived. The project currently supports the complete text/voice Momotalk loop with StagePlan 2.0, TTS, ASR, expression/mouth feedback, Markdown memory and unified runtime debugging.

Future candidates include richer multi-character scheduling, stronger memory retrieval, more expressive facial/body animation, improved ASR quality, and a standalone launcher/app shell.
