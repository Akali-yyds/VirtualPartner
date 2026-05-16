# VirtualPartner 第二阶段 README

更新时间：2026-05-16

本文档用于存档第二阶段的完成态，方便后续快速恢复上下文。更细的阶段推进过程见 `DevelopmentTODO_Stage2.md`，开发协作原则仍以 `ReadFirst.md` 为准。

## 当前状态

- 第二阶段主线实现已完成，等待本轮手动验收确认后写回 TODO。
- 当前阶段重点已经从实现转为归档和回看。
- 继续开发时仍以 `DevelopmentTODO_Stage2.md` 作为现场记录，未验收前不回写完成勾选。

## 阶段成果

- StagePlan 2.0 已成为第二阶段主格式。
- Momotalk 已接入文本聊天、气泡显示、聊天历史与基础 UI 外壳。
- TTS 已接入本地服务包装器，并支持真实服务与降级模式。
- ASR 已接入本地语音识别服务，并支持文本填充和自动发送。
- Memory 已接入 Markdown 长期记忆链路，并注入当前角色 core / high 记忆。
- 统一 Runtime Debug 已整合 LLM、StagePlan、TTS、ASR、Memory、Momotalk、Character、Expr/Mouth 等状态观察入口。

## 目录说明

```text
VirtualPartner/
  Assets/
    Scenes/
      SampleScene.unity
    Character/
    VirtualPartner/
      Runtime/
      Profiles/
      Prompts/
      Timelines/
  LocalServices/
    TTS/
    ASR/
  UserSettings/
    VirtualPartnerLlmConfig.json
```

关键目录：

- `Runtime/`：核心运行时代码。
- `Profiles/`：角色与行为配置资产。
- `Prompts/`：LLM prompt 模块化文档。
- `Timelines/`：本地样例与阶段演示 JSON。
- `LocalServices/TTS/`：TTS 本地包装器与启动脚本。
- `LocalServices/ASR/`：ASR 本地服务与启动脚本。
- `UserSettings/`：本地私有配置目录，不提交真实 API key。

## 核心系统

### StagePlan 2.0

第二阶段的主执行格式是 StagePlan 2.0。LLM 只输出 StagePlan JSON，Unity 先校验，再按 stage 顺序执行 speech、bonePose、animation、facing、locomotion 等动作。

### Momotalk

Momotalk 负责用户文本输入、聊天历史、未读提示、联系人列表与对话页。它是第二阶段正式交互入口，不再把 timeline 当作直接聊天接口。

### TTS

TTS 通过本地包装器接入真实语音服务，speech action 可等待真实音频播放完成；失败时可降级到文本估算模式。

### ASR

ASR 通过本地服务接入真实语音识别，识别结果可填入 Momotalk 输入框，也可按配置自动发送给 LLM。

### Memory

Memory 使用 Markdown 长期记忆文件，按角色分类保存，并在后续对话中注入 core / high 记忆。普通寒暄不会自动写入。

### Runtime Debug

统一调试面板入口为 `VirtualPartner Runtime Debug`。当前包含：

- `Overview`
- `LLM`
- `StagePlan`
- `Momotalk`
- `TTS`
- `ASR`
- `Memory`
- `Character`
- `FSM`
- `Root`
- `Bone`
- `Expr/Mouth`

## 启动与验证

### Unity 侧

1. 打开 `SampleScene`。
2. 进入 Play。
3. 确认右侧出现 Momotalk 入口按钮。
4. 打开统一 Runtime Debug，依次确认各页状态正常。

### TTS

真实服务优先验证顺序：

1. 先启动 GPT-SoVITS `api_v2.py`，默认地址是 `127.0.0.1:9880`。
2. 再启动 `VirtualPartner/LocalServices/TTS/tts_service.py`，默认包装器地址是 `127.0.0.1:8765`。
3. 参考 [`VirtualPartner/LocalServices/TTS/README.md`](./VirtualPartner/LocalServices/TTS/README.md)。

### ASR

真实服务优先验证顺序：

1. 在 `VirtualPartner/LocalServices/ASR/` 运行 `start_asr_service.bat`。
2. 确认 `http://127.0.0.1:8766/health` 可用。
3. 参考 [`VirtualPartner/LocalServices/ASR/README.md`](./VirtualPartner/LocalServices/ASR/README.md)。

### 最终验收清单

- Play 后可以打开 Momotalk。
- 文本输入后 LLM 返回 StagePlan 2.0。
- StagePlan 按 stage 执行，speech 顺序正确。
- Toki 可以切换基础表情，嘴型可以随真实 TTS 或文本 fallback 开合。
- ASR 可以识别语音并填入输入框或自动发送。
- Memory 可以写入 Markdown，并在后续 Prompt 中注入当前角色记忆。
- Runtime Debug 能看到 Momotalk、Character、StagePlan、TTS、ASR、Memory 的关键状态。

## 备注

- 本文档是阶段归档，不替代阶段中的实时 TODO。
- 如果后续需要回看实现细节，优先看 `DevelopmentTODO_Stage2.md` 和对应 Runtime 脚本。
