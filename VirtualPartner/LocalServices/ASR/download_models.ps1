$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModelName = "sherpa-onnx-streaming-zipformer-ctc-zh-int8-2025-06-30"
$AsrUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/$ModelName.tar.bz2"
$VadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad.onnx"

$AsrRoot = Join-Path $Root "models\asr"
$VadRoot = Join-Path $Root "models\vad"
$Archive = Join-Path $AsrRoot "$ModelName.tar.bz2"
$ModelDir = Join-Path $AsrRoot $ModelName
$VadPath = Join-Path $VadRoot "silero_vad.onnx"

New-Item -ItemType Directory -Force -Path $AsrRoot | Out-Null
New-Item -ItemType Directory -Force -Path $VadRoot | Out-Null

if (!(Test-Path $ModelDir)) {
    if (!(Test-Path $Archive)) {
        Write-Host "Downloading ASR model:" $AsrUrl
        Invoke-WebRequest -Uri $AsrUrl -OutFile $Archive
    }

    Write-Host "Extracting ASR model..."
    & tar -xjf $Archive -C $AsrRoot
}

if (!(Test-Path $VadPath)) {
    Write-Host "Downloading VAD model:" $VadUrl
    Invoke-WebRequest -Uri $VadUrl -OutFile $VadPath
}

$Required = @(
    (Join-Path $ModelDir "model.int8.onnx"),
    (Join-Path $ModelDir "tokens.txt"),
    $VadPath
)

foreach ($Path in $Required) {
    if (!(Test-Path $Path)) {
        throw "Required model file missing: $Path"
    }
}

Write-Host "ASR models are ready."
