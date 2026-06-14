param(
    [string]$OutputRoot = "F:\Release\VirtualPartnerRuntimePayloads",
    [string]$BuildCacheRoot = "F:\Release\VirtualPartnerBuildCache",
    [string]$GptSoVitsSource = "F:\Release\VirtualPartnerRelease\Services\GPT-SoVITS",
    [string]$AsrSource = "F:\Release\VirtualPartnerRelease\Services\ASR",
    [switch]$PackExistingGptEnv,
    [switch]$PackPortableGptRuntime,
    [switch]$ReuseExistingAsrEnv,
    [switch]$SkipGptPack,
    [switch]$SkipAsrPack,
    [string]$ExistingGptEnv = "G:\Conda\envs\GPTSoVits",
    [string]$PortableGptRuntimeRoot = "F:\Project\TTS\GPT-SoVITS\Runtime"
)

$ErrorActionPreference = "Stop"

$GptEnv = Join-Path $BuildCacheRoot "envs\vp-gpt-tts-cpu"
$AsrEnv = Join-Path $BuildCacheRoot "envs\vp-asr"
$SupportDllEnv = Join-Path $BuildCacheRoot "envs\vp-runtime-dlls"
$TempRoot = Join-Path $BuildCacheRoot "temp"
$GptReq = Join-Path $TempRoot "gpt-requirements-cpu.txt"

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Body
    )

    Write-Host ""
    Write-Host "[Runtime] $Label"
    & $Body
}

function Invoke-Native {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

function Ensure-CondaPack {
    $Found = $false
    & conda run -n base conda-pack --version | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $Found = $true
    }

    if (!$Found) {
        Invoke-Native "conda" @("install", "-n", "base", "-y", "-c", "conda-forge", "conda-pack")
    }
}

function Remove-EnvIfExists {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Recurse -Force $Path
    }
}

function Assert-CondaPackageBinaryFiles {
    param(
        [string]$Label,
        [string]$EnvRoot
    )

    $MetaRoot = Join-Path $EnvRoot "conda-meta"
    if (!(Test-Path $MetaRoot)) {
        throw "$Label conda metadata missing: $MetaRoot"
    }

    $Missing = New-Object System.Collections.Generic.List[object]
    Get-ChildItem -Path $MetaRoot -Filter "*.json" | ForEach-Object {
        $Package = Get-Content $_.FullName -Raw | ConvertFrom-Json
        foreach ($RelativePath in @($Package.files)) {
            if ([string]::IsNullOrWhiteSpace($RelativePath)) {
                continue
            }

            $NormalizedPath = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            if ($NormalizedPath -notmatch "\.(dll|exe|pyd)$") {
                continue
            }

            $FullPath = Join-Path $EnvRoot $NormalizedPath
            if (!(Test-Path $FullPath)) {
                $Missing.Add([pscustomobject]@{
                    Package = [string]$Package.name
                    Version = [string]$Package.version
                    RelativePath = $NormalizedPath
                }) | Out-Null
            }
        }
    }

    if ($Missing.Count -gt 0) {
        $Preview = $Missing |
            Select-Object -First 20 |
            ForEach-Object { "$($_.Package)-$($_.Version): $($_.RelativePath)" }
        throw "$Label runtime has missing conda package files. Rebuild or repair the environment before packing:`n$($Preview -join "`n")"
    }

    Write-Host "[Runtime] $Label conda package files validated."
}

function Invoke-CleanRuntimeCommand {
    param(
        [string]$Label,
        [string]$RuntimeRoot,
        [string]$FilePath,
        [string[]]$Arguments
    )

    if (!(Test-Path $FilePath)) {
        throw "$Label executable missing: $FilePath"
    }

    $PathParts = @(
        $RuntimeRoot,
        (Join-Path $RuntimeRoot "Scripts"),
        (Join-Path $RuntimeRoot "Library\mingw-w64\bin"),
        (Join-Path $RuntimeRoot "Library\usr\bin"),
        (Join-Path $RuntimeRoot "Library\bin")
    ) | Where-Object { Test-Path $_ }

    $OldPath = $env:PATH
    try {
        $env:PATH = $PathParts -join [System.IO.Path]::PathSeparator
        & $FilePath @Arguments | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "$Label smoke test failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        $env:PATH = $OldPath
    }
}

function Repair-KnownRuntimeDlls {
    param(
        [string]$RuntimeRoot,
        [string[]]$CandidateRuntimeRoots
    )

    $KnownDlls = @("Library\bin\liblzma.dll")
    foreach ($RelativePath in $KnownDlls) {
        $TargetPath = Join-Path $RuntimeRoot $RelativePath
        if (Test-Path $TargetPath) {
            continue
        }

        $SourcePath = $null
        foreach ($CandidateRoot in @($CandidateRuntimeRoots)) {
            if ([string]::IsNullOrWhiteSpace($CandidateRoot)) {
                continue
            }

            $CandidatePath = Join-Path $CandidateRoot $RelativePath
            if (Test-Path $CandidatePath) {
                $SourcePath = $CandidatePath
                break
            }
        }

        if (!$SourcePath) {
            continue
        }

        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $TargetPath) | Out-Null
        Copy-Item -Force $SourcePath $TargetPath
        Write-Host "[Runtime] Repaired portable runtime file:" $RelativePath
    }
}

function Pack-PortableRuntime {
    param(
        [string]$SourceRuntimeRoot,
        [string]$StageRuntimeRoot,
        [string]$OutputArchive,
        [string[]]$CandidateRuntimeRoots
    )

    if (!(Test-Path (Join-Path $SourceRuntimeRoot "python.exe"))) {
        throw "Portable GPT runtime python.exe missing: $SourceRuntimeRoot"
    }

    if (Test-Path $StageRuntimeRoot) {
        Remove-Item -Recurse -Force $StageRuntimeRoot
    }
    New-Item -ItemType Directory -Force -Path $StageRuntimeRoot | Out-Null

    & robocopy $SourceRuntimeRoot $StageRuntimeRoot /MIR /XD __pycache__ logs output outputs TEMP temp | Out-Null
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed: $SourceRuntimeRoot -> $StageRuntimeRoot"
    }

    Repair-KnownRuntimeDlls -RuntimeRoot $StageRuntimeRoot -CandidateRuntimeRoots $CandidateRuntimeRoots

    Invoke-CleanRuntimeCommand `
        -Label "Portable GPT-SoVITS/TTS Python" `
        -RuntimeRoot $StageRuntimeRoot `
        -FilePath (Join-Path $StageRuntimeRoot "python.exe") `
        -Arguments @("-V")

    $Ffmpeg = Join-Path $StageRuntimeRoot "Library\bin\ffmpeg.exe"
    if (Test-Path $Ffmpeg) {
        Invoke-CleanRuntimeCommand `
            -Label "Portable GPT-SoVITS/TTS ffmpeg" `
            -RuntimeRoot $StageRuntimeRoot `
            -FilePath $Ffmpeg `
            -Arguments @("-version")
    }

    if (Test-Path $OutputArchive) {
        Remove-Item -Force $OutputArchive
    }

    Invoke-Native "tar" @("-a", "-cf", $OutputArchive, "-C", $StageRuntimeRoot, ".")
}

function Ensure-PortableRuntimeDllCandidates {
    param(
        [string]$SourceRuntimeRoot,
        [string[]]$CandidateRuntimeRoots
    )

    $RequiredRelativePath = "Library\bin\liblzma.dll"
    if (Test-Path (Join-Path $SourceRuntimeRoot $RequiredRelativePath)) {
        return $CandidateRuntimeRoots
    }

    foreach ($CandidateRoot in @($CandidateRuntimeRoots)) {
        if (![string]::IsNullOrWhiteSpace($CandidateRoot) -and (Test-Path (Join-Path $CandidateRoot $RequiredRelativePath))) {
            return $CandidateRuntimeRoots
        }
    }

    Invoke-Step "Build runtime DLL support environment" {
        Remove-EnvIfExists $SupportDllEnv
        Invoke-Native "conda" @("create", "-y", "-p", $SupportDllEnv, "-c", "conda-forge", "liblzma")
    }

    return @($CandidateRuntimeRoots + @($SupportDllEnv))
}

New-Item -ItemType Directory -Force -Path $OutputRoot, $BuildCacheRoot, $TempRoot | Out-Null

if (!(Test-Path $GptSoVitsSource)) {
    throw "GPT-SoVITS source missing: $GptSoVitsSource"
}
if (!(Test-Path $AsrSource)) {
    throw "ASR source missing: $AsrSource"
}
if ($PackPortableGptRuntime -and !(Test-Path $PortableGptRuntimeRoot)) {
    throw "Portable GPT runtime missing: $PortableGptRuntimeRoot"
}
if (!$PackPortableGptRuntime -and !$PackExistingGptEnv) {
    $SourceReq = Join-Path $GptSoVitsSource "requirements.txt"
    $SourceExtraReq = Join-Path $GptSoVitsSource "extra-req.txt"
    if (!(Test-Path $SourceReq) -or !(Test-Path $SourceExtraReq)) {
        throw "GPT-SoVITS requirements missing. Use a source checkout with requirements.txt/extra-req.txt, or rerun with -PackPortableGptRuntime -PortableGptRuntimeRoot `"$PortableGptRuntimeRoot`"."
    }
}

Invoke-Step "Ensure conda-pack" {
    Ensure-CondaPack
}

if ($PackPortableGptRuntime) {
    Write-Host "[Runtime] Using portable GPT runtime:" $PortableGptRuntimeRoot
} elseif ($PackExistingGptEnv) {
    if (!(Test-Path $ExistingGptEnv)) {
        throw "Existing GPT environment missing: $ExistingGptEnv"
    }
    $GptEnv = $ExistingGptEnv
} else {
    Invoke-Step "Build clean GPT-SoVITS/TTS CPU environment" {
        Remove-EnvIfExists $GptEnv
        Invoke-Native "conda" @("create", "-y", "-p", $GptEnv, "python=3.10")
        Invoke-Native "conda" @("install", "-y", "-p", $GptEnv, "-c", "conda-forge", "ffmpeg=6.1.1", "liblzma", "cmake")

        $SourceReq = Join-Path $GptSoVitsSource "requirements.txt"
        Get-Content $SourceReq |
            Where-Object { $_ -notmatch "^\s*onnxruntime-gpu" } |
            Set-Content -Encoding UTF8 $GptReq
        Add-Content -Encoding UTF8 $GptReq "onnxruntime"

        Invoke-Native "conda" @("run", "-p", $GptEnv, "python", "-m", "pip", "install", "--upgrade", "pip", "wheel", "setuptools")
        Invoke-Native "conda" @("run", "-p", $GptEnv, "python", "-m", "pip", "install", "torch==2.5.1+cpu", "torchaudio==2.5.1+cpu", "--index-url", "https://download.pytorch.org/whl/cpu")
        Invoke-Native "conda" @("run", "-p", $GptEnv, "python", "-m", "pip", "install", "-r", $GptReq)
        Invoke-Native "conda" @("run", "-p", $GptEnv, "python", "-m", "pip", "install", "-r", (Join-Path $GptSoVitsSource "extra-req.txt"))
    }
}

Invoke-Step "Build clean ASR environment" {
    if ($ReuseExistingAsrEnv -and (Test-Path $AsrEnv)) {
        Write-Host "[Runtime] Reusing existing ASR env:" $AsrEnv
    } else {
        Remove-EnvIfExists $AsrEnv
        Invoke-Native "conda" @("create", "-y", "-p", $AsrEnv, "python=3.11")
        Invoke-Native "conda" @("run", "-p", $AsrEnv, "python", "-m", "pip", "install", "--upgrade", "pip", "wheel", "setuptools")
        Invoke-Native "conda" @("run", "-p", $AsrEnv, "python", "-m", "pip", "install", "-r", (Join-Path $AsrSource "requirements.txt"))
    }
}

Invoke-Step "Pack GPT-SoVITS/TTS runtime" {
    if ($SkipGptPack) {
        Write-Host "[Runtime] Skipping GPT-SoVITS/TTS pack."
        return
    }

    $Out = Join-Path $OutputRoot "gpt_tts_runtime.zip"
    if (Test-Path $Out) {
        Remove-Item -Force $Out
    }
    if ($PackPortableGptRuntime) {
        $CandidateRuntimeRoots = Ensure-PortableRuntimeDllCandidates `
            -SourceRuntimeRoot $PortableGptRuntimeRoot `
            -CandidateRuntimeRoots @($GptEnv, $AsrEnv)
        Pack-PortableRuntime `
            -SourceRuntimeRoot $PortableGptRuntimeRoot `
            -StageRuntimeRoot (Join-Path $BuildCacheRoot "portable\gpt_tts_runtime") `
            -OutputArchive $Out `
            -CandidateRuntimeRoots $CandidateRuntimeRoots
        return
    }

    Assert-CondaPackageBinaryFiles -Label "GPT-SoVITS/TTS" -EnvRoot $GptEnv

    $PackArgs = @("run", "-n", "base", "conda-pack", "-p", $GptEnv, "-o", $Out, "--format", "zip", "--force")
    if ($PackExistingGptEnv) {
        $PackArgs += "--ignore-missing-files"
    }
    Invoke-Native "conda" $PackArgs
}

Invoke-Step "Pack ASR runtime" {
    if ($SkipAsrPack) {
        Write-Host "[Runtime] Skipping ASR pack."
        return
    }

    Assert-CondaPackageBinaryFiles -Label "ASR" -EnvRoot $AsrEnv

    $Out = Join-Path $OutputRoot "asr_runtime.zip"
    if (Test-Path $Out) {
        Remove-Item -Force $Out
    }
    Invoke-Native "conda" @("run", "-n", "base", "conda-pack", "-p", $AsrEnv, "-o", $Out, "--format", "zip", "--force", "--ignore-missing-files")
}

"v1-20260517-003" | Set-Content -Encoding UTF8 (Join-Path $OutputRoot "runtimeVersion.txt")
Write-Host ""
Write-Host "[Runtime] Payloads ready:" $OutputRoot
