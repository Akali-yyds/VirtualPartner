# VirtualPartner Release Packaging Guide

本文档记录从 Unity 导出到生成可迁移 zip 包的完整流程。目标是生成一个可以复制到其他 Windows 机器上运行的绿色版发布包。

## 目录约定

默认使用以下目录：

```text
ProjectRoot:
F:\Project\VirtualPartner-new

UnityProject:
F:\Project\VirtualPartner-new\VirtualPartner

SourceReleaseRoot:
F:\Release\VirtualPartnerRelease

RuntimePayloadRoot:
F:\Release\VirtualPartnerRuntimePayloads

FinalReleaseRoot:
F:\Release\VirtualPartnerReleaseV1

CleanReleaseRoot:
F:\Release\VirtualPartnerReleaseV1_Clean
```

各目录职责：

```text
F:\Release\VirtualPartnerRelease
```

中间发布源目录。放 Unity 导出的 App、服务源码和运行时需要的原始资源。

```text
F:\Release\VirtualPartnerRuntimePayloads
```

Python runtime 压缩包输出目录。由 `Launcher\Build-RuntimePayloads.ps1` 生成。

```text
F:\Release\VirtualPartnerReleaseV1
```

最终发布目录。由 `Launcher\Build-V1Release.ps1` 合成，包含 `Launcher.exe`、`App`、`Services`、`Runtime`。

```text
F:\Release\VirtualPartnerReleaseV1_Clean
```

用于压缩分发的干净包目录。不包含你的 API Key、聊天记录、长期记忆和测试缓存。

## 最终包结构

最终可迁移包应包含：

```text
VirtualPartnerReleaseV1_Clean/
  Launcher.exe
  launcher_config.json

  App/
    VirtualPartner.exe
    VirtualPartner_Data/
    UnityPlayer.dll
    MonoBleedingEdge/
    D3D12/
    UnityCrashHandler64.exe
    UserSettings/
      VirtualPartnerLlmConfig.json
    UserData/

  Services/
    GPT-SoVITS/
    TTS/
    ASR/

  Runtime/
    gpt_tts/
    asr/
```

运行入口是：

```text
Launcher.exe
```

不要让用户直接运行：

```text
App\VirtualPartner.exe
```

因为 TTS、ASR、GPT-SoVITS 本地服务需要由 Launcher 先启动。

## 1. Unity 导出 App

在 Unity 中：

1. 打开 `F:\Project\VirtualPartner-new\VirtualPartner`。
2. 打开主场景：

```text
Assets/Scenes/SampleScene.unity
```

3. 进入 `File > Build Profiles` 或 `File > Build Settings`。
4. 平台选择 Windows。
5. Scenes 中确认包含：

```text
Assets/Scenes/SampleScene.unity
```

6. 取消 `Development Build`。
7. 输出到：

```text
F:\Release\VirtualPartnerRelease\App\VirtualPartner.exe
```

也可以用命令行构建：

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "F:\Project\VirtualPartner-new\VirtualPartner" `
  -buildWindows64Player "F:\Release\VirtualPartnerRelease\App\VirtualPartner.exe"
```

构建后可以删除调试符号目录：

```powershell
Remove-Item -Recurse -Force "F:\Release\VirtualPartnerRelease\App\VirtualPartner_BurstDebugInformation_DoNotShip" -ErrorAction SilentlyContinue
```

## 2. 准备 App 用户目录

Unity Build 运行时，`Application.dataPath` 会指向：

```text
App\VirtualPartner_Data
```

项目中的 LLM 配置、聊天记录和记忆数据会从 `App` 旁边的用户目录读取。因此发布源目录中必须有：

```text
F:\Release\VirtualPartnerRelease\App\UserSettings
F:\Release\VirtualPartnerRelease\App\UserData
```

创建目录：

```powershell
New-Item -ItemType Directory -Force "F:\Release\VirtualPartnerRelease\App\UserSettings"
New-Item -ItemType Directory -Force "F:\Release\VirtualPartnerRelease\App\UserData"
```

写入空 API 配置模板。不要把自己的真实 API Key 放进原始包：

```powershell
@'
{
  "apiKey": "",
  "model": "",
  "chatCompletionsUrl": "",
  "baseUrl": "https://api.openai.com",
  "useJsonResponseFormat": true,
  "supportsDeveloperRole": false,
  "interactionTimeoutSeconds": 10
}
'@ | Set-Content -Encoding UTF8 "F:\Release\VirtualPartnerRelease\App\UserSettings\VirtualPartnerLlmConfig.json"
```

如果要带默认空数据，只保留空目录即可：

```powershell
Remove-Item -Recurse -Force "F:\Release\VirtualPartnerRelease\App\UserData" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "F:\Release\VirtualPartnerRelease\App\UserData"
```

## 3. 准备本地服务

发布源目录中需要：

```text
F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS
F:\Release\VirtualPartnerRelease\Services\TTS
F:\Release\VirtualPartnerRelease\Services\ASR
```

复制 TTS：

```powershell
robocopy `
  "F:\Project\VirtualPartner-new\VirtualPartner\LocalServices\TTS" `
  "F:\Release\VirtualPartnerRelease\Services\TTS" `
  /MIR /XD __pycache__ .venv logs outputs
```

复制 ASR：

```powershell
robocopy `
  "F:\Project\VirtualPartner-new\VirtualPartner\LocalServices\ASR" `
  "F:\Release\VirtualPartnerRelease\Services\ASR" `
  /MIR /XD __pycache__ .venv logs outputs
```

ASR 的 `models` 目录必须保留：

```text
F:\Release\VirtualPartnerRelease\Services\ASR\models
```

复制 GPT-SoVITS。目标目录必须满足 Launcher 配置中的路径：

```text
F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS\api_v2.py
F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS\GPT_SoVITS\configs\tts_infer.yaml
```

如果你的 GPT-SoVITS 是 portable 包结构，入口在：

```text
Services\GPT-SoVITS\app\api_v2.py
Services\GPT-SoVITS\app\GPT_SoVITS\configs\tts_infer.yaml
```

则需要把 `app` 里的内容复制到 `GPT-SoVITS` 根目录：

```powershell
# 必填：改成你本机 GPT-SoVITS 的源目录。
# 这个目录可以直接包含 api_v2.py，也可以是 portable 包根目录，里面包含 app\api_v2.py。
$gptSourceRoot = "F:\Project\TTS\GPT-SoVITS"
$gptDestRoot = "F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS"

if (!(Test-Path $gptSourceRoot)) {
  throw "GPT-SoVITS source directory not found. Please edit `$gptSourceRoot: $gptSourceRoot"
}

if (Test-Path (Join-Path $gptSourceRoot "api_v2.py")) {
  $gptCopyRoot = $gptSourceRoot
} elseif (Test-Path (Join-Path $gptSourceRoot "app\api_v2.py")) {
  $gptCopyRoot = Join-Path $gptSourceRoot "app"
} else {
  throw "GPT-SoVITS source is invalid. Expected api_v2.py or app\api_v2.py under: $gptSourceRoot"
}

New-Item -ItemType Directory -Force $gptDestRoot | Out-Null
robocopy $gptCopyRoot $gptDestRoot /MIR /XD .git __pycache__ logs output outputs TEMP temp runtime venv .venv
if ($LASTEXITCODE -gt 7) {
  throw "robocopy failed: $gptCopyRoot -> $gptDestRoot"
}

if (!(Test-Path (Join-Path $gptDestRoot "api_v2.py"))) {
  throw "GPT-SoVITS api_v2.py missing after copy."
}
if (!(Test-Path (Join-Path $gptDestRoot "GPT_SoVITS\configs\tts_infer.yaml"))) {
  throw "GPT-SoVITS tts_infer.yaml missing after copy."
}
```

检查服务关键路径：

```powershell
$required = @(
  "F:\Release\VirtualPartnerRelease\Services\TTS\tts_service.py",
  "F:\Release\VirtualPartnerRelease\Services\TTS\config.json",
  "F:\Release\VirtualPartnerRelease\Services\TTS\voices",
  "F:\Release\VirtualPartnerRelease\Services\ASR\asr_service.py",
  "F:\Release\VirtualPartnerRelease\Services\ASR\config.json",
  "F:\Release\VirtualPartnerRelease\Services\ASR\models",
  "F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS\api_v2.py",
  "F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS\GPT_SoVITS\configs\tts_infer.yaml"
)

$check = foreach ($path in $required) {
  [PSCustomObject]@{
    Path = $path
    Exists = Test-Path $path
  }
}

$check | Format-Table -AutoSize
```

所有 `Exists` 都应为 `True`。

## 4. 复制运行时加载的 UI 原始资源

当前 Momotalk 的部分图标和纹理是运行时用磁盘路径加载的：

```text
Application.dataPath + VirtualPartner/UI/Momotalk/...
```

在 Build 中，目标路径是：

```text
App\VirtualPartner_Data\VirtualPartner\UI\Momotalk
```

因此必须复制 Momotalk UI 原始资源，否则图标会变成白色方块：

```powershell
robocopy `
  "F:\Project\VirtualPartner-new\VirtualPartner\Assets\VirtualPartner\UI\Momotalk" `
  "F:\Release\VirtualPartnerRelease\App\VirtualPartner_Data\VirtualPartner\UI\Momotalk" `
  /MIR /XF *.meta
```

检查：

```powershell
Test-Path "F:\Release\VirtualPartnerRelease\App\VirtualPartner_Data\VirtualPartner\UI\Momotalk\Icons\back_icon.png"
Test-Path "F:\Release\VirtualPartnerRelease\App\VirtualPartner_Data\VirtualPartner\UI\Momotalk\Textures\loading_icon.png"
```

两项应为 `True`。

Prompt 文件也会作为运行时 fallback 资源使用，尤其是 `named-gestures.md` 里的比心等精确姿势库。手动准备发布源目录时复制：

```powershell
robocopy `
  "F:\Project\VirtualPartner-new\VirtualPartner\Assets\VirtualPartner\Prompts" `
  "F:\Release\VirtualPartnerRelease\App\VirtualPartner_Data\VirtualPartner\Prompts" `
  /MIR /XF *.meta
```

检查：

```powershell
Test-Path "F:\Release\VirtualPartnerRelease\App\VirtualPartner_Data\VirtualPartner\Prompts\named-gestures.md"
```

应为 `True`。

注意：`Launcher\Build-V1Release.ps1` 也会自动把项目内的 `Assets\VirtualPartner\Prompts` 复制进最终 V1 包。这里手动复制主要用于检查发布源目录结构或直接运行 Unity Build 时的 fallback。

## 5. 生成 Python Runtime Payloads

如果 `F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS` 是源码版 GPT-SoVITS，并且包含 `requirements.txt`、`extra-req.txt`，运行：

```powershell
cd F:\Project\VirtualPartner-new

powershell -ExecutionPolicy Bypass -File .\Launcher\Build-RuntimePayloads.ps1
```

如果你使用的是 portable 版 GPT-SoVITS，也就是源目录类似：

```text
F:\Project\TTS\GPT-SoVITS\
  app\
  Runtime\
```

并且 `app` 里没有 `requirements.txt`，则运行：

```powershell
cd F:\Project\VirtualPartner-new

powershell -ExecutionPolicy Bypass -File .\Launcher\Build-RuntimePayloads.ps1 `
  -PackPortableGptRuntime `
  -PortableGptRuntimeRoot "F:\Project\TTS\GPT-SoVITS\Runtime"
```

成功后应生成：

```text
F:\Release\VirtualPartnerRuntimePayloads\gpt_tts_runtime.zip
F:\Release\VirtualPartnerRuntimePayloads\asr_runtime.zip
F:\Release\VirtualPartnerRuntimePayloads\runtimeVersion.txt
```

检查：

```powershell
Test-Path "F:\Release\VirtualPartnerRuntimePayloads\gpt_tts_runtime.zip"
Test-Path "F:\Release\VirtualPartnerRuntimePayloads\asr_runtime.zip"
```

如果遇到 `gdk-pixbuf` / `librsvg` / `UnicodeDecodeError('gbk', ...)` 之类错误，先确认 `Launcher\Build-RuntimePayloads.ps1` 里已经使用 `ffmpeg=6.1.1`，然后删除失败的临时环境重跑：

```powershell
cd F:\Project\VirtualPartner-new

Remove-Item -Recurse -Force "F:\Release\VirtualPartnerBuildCache\envs\vp-gpt-tts-cpu" -ErrorAction SilentlyContinue

powershell -ExecutionPolicy Bypass -File .\Launcher\Build-RuntimePayloads.ps1
```

如果仍然失败，再使用已有 GPTSoVits 环境打包：

```powershell
powershell -ExecutionPolicy Bypass -File .\Launcher\Build-RuntimePayloads.ps1
  -PackExistingGptEnv `
  -ExistingGptEnv "G:\Conda\envs\GPTSoVits"
```

## 6. 生成最终 V1 发布目录

运行：

```powershell
cd F:\Project\VirtualPartner-new

powershell -ExecutionPolicy Bypass -File .\Launcher\Build-V1Release.ps1 `
  -SourceReleaseRoot "F:\Release\VirtualPartnerRelease" `
  -OutputRoot "F:\Release\VirtualPartnerReleaseV1" `
  -PayloadRoot "F:\Release\VirtualPartnerRuntimePayloads"
```

成功后应得到：

```text
F:\Release\VirtualPartnerReleaseV1\Launcher.exe
F:\Release\VirtualPartnerReleaseV1\launcher_config.json
F:\Release\VirtualPartnerReleaseV1\App
F:\Release\VirtualPartnerReleaseV1\Services
F:\Release\VirtualPartnerReleaseV1\Runtime
```

检查：

```powershell
$required = @(
  "F:\Release\VirtualPartnerReleaseV1\Launcher.exe",
  "F:\Release\VirtualPartnerReleaseV1\launcher_config.json",
  "F:\Release\VirtualPartnerReleaseV1\App\VirtualPartner.exe",
  "F:\Release\VirtualPartnerReleaseV1\App\UserSettings\VirtualPartnerLlmConfig.json",
  "F:\Release\VirtualPartnerReleaseV1\App\UserData",
  "F:\Release\VirtualPartnerReleaseV1\App\VirtualPartner_Data\VirtualPartner\UI\Momotalk",
  "F:\Release\VirtualPartnerReleaseV1\Services\GPT-SoVITS\api_v2.py",
  "F:\Release\VirtualPartnerReleaseV1\Services\TTS\tts_service.py",
  "F:\Release\VirtualPartnerReleaseV1\Services\ASR\asr_service.py",
  "F:\Release\VirtualPartnerReleaseV1\Runtime\gpt_tts\python.exe",
  "F:\Release\VirtualPartnerReleaseV1\Runtime\asr\python.exe"
)

$check = foreach ($path in $required) {
  [PSCustomObject]@{
    Path = $path
    Exists = Test-Path $path
  }
}

$check | Format-Table -AutoSize
```

所有 `Exists` 都应为 `True`。

## 7. 本机测试

先不要压缩。直接运行：

```powershell
F:\Release\VirtualPartnerReleaseV1\Launcher.exe
```

验证：

1. Launcher 可以启动。
2. GPT-SoVITS、TTS、ASR 服务可以启动。
3. Unity 游戏可以打开。
4. Momotalk 图标不是白方块。
5. Debug 面板的 `API` 标签页可以填写、测试、保存配置。
6. 发送一条文本消息，确认 LLM 返回、角色动作和气泡正常。
7. 如果需要测试语音，确认 TTS / ASR 正常。

注意：本机测试会写入 API 配置、聊天记录、记忆、TTS 缓存。测试过的 `VirtualPartnerReleaseV1` 不应直接压缩分发。

## 8. 生成干净发布包目录

从测试后的 V1 复制一份干净目录：

```powershell
$Src = "F:\Release\VirtualPartnerReleaseV1"
$Dst = "F:\Release\VirtualPartnerReleaseV1_Clean"

if (Test-Path $Dst) {
  Remove-Item -Recurse -Force $Dst
}

robocopy $Src $Dst /MIR /XD logs outputs
```

清理用户数据：

```powershell
Remove-Item -Recurse -Force "$Dst\App\UserData" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$Dst\App\UserData"

Remove-Item -Recurse -Force "$Dst\App\UserSettings" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$Dst\App\UserSettings"
```

写入空 API 配置模板：

```powershell
@'
{
  "apiKey": "",
  "model": "",
  "chatCompletionsUrl": "",
  "baseUrl": "https://api.openai.com",
  "useJsonResponseFormat": true,
  "supportsDeveloperRole": false,
  "interactionTimeoutSeconds": 10
}
'@ | Set-Content -Encoding UTF8 "$Dst\App\UserSettings\VirtualPartnerLlmConfig.json"
```

清理服务日志和输出：

```powershell
Remove-Item -Recurse -Force "$Dst\Services\GPT-SoVITS\logs" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$Dst\Services\GPT-SoVITS\outputs" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$Dst\Services\TTS\logs" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$Dst\Services\TTS\outputs" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$Dst\Services\ASR\logs" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$Dst\Services\ASR\outputs" -ErrorAction SilentlyContinue
```

最终检查干净包：

```powershell
$required = @(
  "$Dst\Launcher.exe",
  "$Dst\launcher_config.json",
  "$Dst\App\VirtualPartner.exe",
  "$Dst\App\UserSettings\VirtualPartnerLlmConfig.json",
  "$Dst\App\UserData",
  "$Dst\App\VirtualPartner_Data\VirtualPartner\UI\Momotalk",
  "$Dst\Services\GPT-SoVITS\api_v2.py",
  "$Dst\Services\TTS\tts_service.py",
  "$Dst\Services\ASR\asr_service.py",
  "$Dst\Runtime\gpt_tts\python.exe",
  "$Dst\Runtime\asr\python.exe"
)

$check = foreach ($path in $required) {
  [PSCustomObject]@{
    Path = $path
    Exists = Test-Path $path
  }
}

$check | Format-Table -AutoSize
```

## 9. 压缩为可迁移 zip

发布包通常较大，推荐使用 7-Zip：

```powershell
& "C:\Program Files\7-Zip\7z.exe" a -tzip `
  "F:\Release\VirtualPartnerReleaseV1_Clean.zip" `
  "F:\Release\VirtualPartnerReleaseV1_Clean\*"
```

如果没有 7-Zip，可以尝试 PowerShell：

```powershell
Compress-Archive `
  -Path "F:\Release\VirtualPartnerReleaseV1_Clean\*" `
  -DestinationPath "F:\Release\VirtualPartnerReleaseV1_Clean.zip" `
  -Force
```

如果 PowerShell 压缩大包失败，安装 7-Zip 后重新压缩。

## 10. 迁移测试

在另一台 Windows 机器上：

1. 解压 `VirtualPartnerReleaseV1_Clean.zip`。
2. 进入解压后的目录。
3. 运行：

```text
Launcher.exe
```

4. 等待服务启动。
5. 游戏打开后，进入 Debug 面板的 `API` 标签页。
6. 填写目标机器用户自己的 API 配置。
7. 点击 `Test`。
8. 成功后点击 `Save`。
9. 打开 Momotalk，发送测试消息。

如果端口被占用，检查：

```text
127.0.0.1:9880
127.0.0.1:8765
127.0.0.1:8766
```

如果 Momotalk 图标变成白方块，检查：

```text
App\VirtualPartner_Data\VirtualPartner\UI\Momotalk
```

如果 LLM 不工作，检查：

```text
App\UserSettings\VirtualPartnerLlmConfig.json
```

如果 TTS 不工作，检查 Launcher 日志和服务健康状态：

```text
%LOCALAPPDATA%\VirtualPartner\V1\logs
```

## 不要打包的目录

以下目录不要放进最终 zip：

```text
F:\Project\VirtualPartner-new
F:\Release\VirtualPartnerRelease
F:\Release\VirtualPartnerRuntimePayloads
F:\Release\VirtualPartnerBuildCache
F:\Release\VirtualPartnerReleaseV1
```

最终只压：

```text
F:\Release\VirtualPartnerReleaseV1_Clean
```

## 常见问题

### 1. API 配置和聊天记录被带到新机器

原因：压缩了测试后的 `VirtualPartnerReleaseV1`。

解决：按第 8 步生成 `VirtualPartnerReleaseV1_Clean`，再压缩 clean 目录。

### 2. Momotalk 图标变白方块

原因：运行时磁盘加载的 Momotalk UI 原始 PNG 没复制到 Build 数据目录。

解决：复制：

```powershell
robocopy `
  "F:\Project\VirtualPartner-new\VirtualPartner\Assets\VirtualPartner\UI\Momotalk" `
  "F:\Release\VirtualPartnerRelease\App\VirtualPartner_Data\VirtualPartner\UI\Momotalk" `
  /MIR /XF *.meta
```

然后重新生成 V1 发布目录。

### 3. GPT-SoVITS 找不到 api_v2.py

原因：GPT-SoVITS 使用 portable 布局，源码在 `app` 子目录。

解决：

```powershell
# 回到第 3 步，执行 GPT-SoVITS 复制代码块。
# 重点是把 $gptSourceRoot 改成你本机真实的 GPT-SoVITS 源目录。
```

### 4. `Build-RuntimePayloads.ps1` 的 conda install 失败

如果出现 `gdk-pixbuf`、`librsvg`、`UnicodeDecodeError('gbk', ...)`，说明 conda 解析到了有问题的新版 `ffmpeg` 依赖链。
当前 `Build-RuntimePayloads.ps1` 已固定 `ffmpeg=6.1.1` 并显式安装 `liblzma`。先删除失败的临时环境，再重跑：

```powershell
cd F:\Project\VirtualPartner-new

Remove-Item -Recurse -Force "F:\Release\VirtualPartnerBuildCache\envs\vp-gpt-tts-cpu" -ErrorAction SilentlyContinue

powershell -ExecutionPolicy Bypass -File .\Launcher\Build-RuntimePayloads.ps1 `
```

如果仍然失败，再退回到已有 GPTSoVits 环境打包：

```powershell
powershell -ExecutionPolicy Bypass -File .\Launcher\Build-RuntimePayloads.ps1 `
  -PackExistingGptEnv `
  -ExistingGptEnv "G:\Conda\envs\GPTSoVits"
```

### 5. 直接运行 Unity exe 没有语音服务

原因：没有通过 Launcher 启动服务。

解决：运行根目录的：

```text
Launcher.exe
```
