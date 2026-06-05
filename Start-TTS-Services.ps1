param(
    [int]$GptPort = 9880,
    [int]$WrapperPort = 8765,
    [int]$StartupTimeoutSeconds = 240,
    [int]$PrewarmTimeoutSeconds = 180,
    [switch]$SkipPrewarm
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$GptRoot = "F:\Project\TTS\GPT-SoVITS"
$GptStartScript = Join-Path $GptRoot "start.ps1"
$RuntimePython = Join-Path $GptRoot "Runtime\python.exe"
$WrapperRoot = Join-Path $ProjectRoot "VirtualPartner\LocalServices\TTS"
$WrapperScript = Join-Path $WrapperRoot "tts_service.py"
$WrapperLogs = Join-Path $WrapperRoot "logs"
$WrapperOutLog = Join-Path $WrapperLogs "tts_wrapper.out.log"
$WrapperErrLog = Join-Path $WrapperLogs "tts_wrapper.err.log"
$GptHealthUrl = "http://127.0.0.1:$GptPort/docs"
$WrapperHealthUrl = "http://127.0.0.1:$WrapperPort/health"
$WrapperTtsUrl = "http://127.0.0.1:$WrapperPort/tts"

function Get-PortListenerPids {
    param([int]$Port)

    return @(Get-NetTCPConnection -LocalAddress 127.0.0.1 -LocalPort $Port -ErrorAction SilentlyContinue |
        Where-Object { $_.State -eq "Listen" -and $_.OwningProcess -gt 0 } |
        Select-Object -ExpandProperty OwningProcess -Unique)
}

function Stop-PortListeners {
    param([int]$Port)

    foreach ($listenerPid in Get-PortListenerPids -Port $Port) {
        Write-Host "[TTS] Stop port $Port PID $listenerPid"
        Stop-Process -Id $listenerPid -Force -ErrorAction SilentlyContinue
    }
}

function Test-HttpOk {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Get-WrapperHealth {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $WrapperHealthUrl -TimeoutSec 5
        return $response.Content | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Wait-GptReady {
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-HttpOk -Url $GptHealthUrl) {
            Write-Host "[TTS] GPT-SoVITS ready: $GptHealthUrl"
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Timeout waiting for GPT-SoVITS: $GptHealthUrl"
}

function Wait-WrapperReady {
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $health = Get-WrapperHealth
        if ($null -ne $health) {
            $upstreamOk = $health.upstream -and $health.upstream.ok
            $voiceOk = $false
            if ($health.voices) {
                foreach ($voice in $health.voices) {
                    if ($voice.voiceId -eq "toki_default" -and $voice.ok) {
                        $voiceOk = $true
                        break
                    }
                }
            }

            if ($health.wrapper.ok -and $upstreamOk -and $voiceOk) {
                Write-Host "[TTS] Wrapper ready: wrapper ok; upstream ok; voice ok"
                return
            }

            Write-Host "[TTS] Waiting wrapper health: wrapper=$($health.wrapper.ok) upstream=$upstreamOk voice=$voiceOk"
        }

        Start-Sleep -Seconds 1
    }

    if (Test-Path $WrapperErrLog) {
        Write-Host "[TTS] Wrapper stderr tail:"
        Get-Content -LiteralPath $WrapperErrLog -Tail 40
    }

    throw "Timeout waiting for TTS wrapper: $WrapperHealthUrl"
}

function Invoke-Prewarm {
    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue

    $prewarmText = [string]::Concat(
        [char]0x9884,
        [char]0x70ed,
        [char]0x6a21,
        [char]0x578b,
        [char]0x3002)

    $payload = [ordered]@{
        voiceId = "toki_default"
        text = $prewarmText
        emotion = "neutral"
        speed = 1.0
        format = "raw"
        stream = $true
        streamingMode = 1
    }

    $json = $payload | ConvertTo-Json -Compress
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds($PrewarmTimeoutSeconds)
    $content = [System.Net.Http.StringContent]::new($json, [Text.Encoding]::UTF8, "application/json")

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $bytes = 0
    try {
        $response = $client.PostAsync($WrapperTtsUrl, $content).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            throw "Prewarm failed: HTTP $([int]$response.StatusCode) $body"
        }

        $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $buffer = New-Object byte[] 8192
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $bytes += $read
        }
    }
    finally {
        $sw.Stop()
        $content.Dispose()
        $client.Dispose()
    }

    Write-Host "[TTS] Prewarm done: $bytes bytes in $([math]::Round($sw.Elapsed.TotalSeconds, 2))s"
}

if (!(Test-Path $GptStartScript)) {
    throw "Missing GPT-SoVITS start script: $GptStartScript"
}
if (!(Test-Path $RuntimePython)) {
    throw "Missing portable python: $RuntimePython"
}
if (!(Test-Path $WrapperScript)) {
    throw "Missing TTS wrapper script: $WrapperScript"
}

New-Item -ItemType Directory -Force -Path $WrapperLogs | Out-Null

Write-Host "===== VirtualPartner TTS Startup ====="
Write-Host "[TTS] Project: $ProjectRoot"
Write-Host "[TTS] GPT root: $GptRoot"
Write-Host "[TTS] Wrapper: $WrapperScript"

Stop-PortListeners -Port $WrapperPort
Stop-PortListeners -Port $GptPort

Write-Host "[TTS] Starting GPT-SoVITS..."
& $GptStartScript -Restart -Port $GptPort
Wait-GptReady

Write-Host "[TTS] Starting TTS wrapper..."
if (Test-Path $WrapperOutLog) { Remove-Item -LiteralPath $WrapperOutLog -Force }
if (Test-Path $WrapperErrLog) { Remove-Item -LiteralPath $WrapperErrLog -Force }

$wrapperProcess = Start-Process `
    -FilePath $RuntimePython `
    -ArgumentList @($WrapperScript) `
    -WorkingDirectory $WrapperRoot `
    -RedirectStandardOutput $WrapperOutLog `
    -RedirectStandardError $WrapperErrLog `
    -WindowStyle Hidden `
    -PassThru

Write-Host "[TTS] Wrapper PID=$($wrapperProcess.Id)"
Wait-WrapperReady

if (-not $SkipPrewarm) {
    Write-Host "[TTS] Prewarming TTS stream..."
    Invoke-Prewarm
}
else {
    Write-Host "[TTS] Prewarm skipped."
}

Write-Host "===== TTS Services Ready ====="
Write-Host "[TTS] GPT health: $GptHealthUrl"
Write-Host "[TTS] Wrapper health: $WrapperHealthUrl"
Write-Host "[TTS] Keep Unity open and test Real TTS now."
