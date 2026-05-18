param(
    [string]$SourceReleaseRoot = "F:\Release\VirtualPartnerRelease",
    [string]$OutputRoot = "F:\Release\VirtualPartnerReleaseV1",
    [string]$PayloadRoot = "F:\Release\VirtualPartnerRuntimePayloads",
    [Alias("SkipPayloadCopy")]
    [switch]$SkipRuntimeBuild
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Join-Path $ScriptRoot "VirtualPartnerLauncher"
$ConfigTemplate = Join-Path $ScriptRoot "launcher_config.v1.json"
$PublishRoot = Join-Path $ScriptRoot "publish"

function Copy-DirectoryMirror {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (!(Test-Path $Source)) {
        throw "Source directory missing: $Source"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    & robocopy $Source $Destination /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed: $Source -> $Destination"
    }
}

function Reset-Directory {
    param([string]$Path)

    $FullPath = [System.IO.Path]::GetFullPath($Path)
    if ($FullPath.EndsWith(":\")) {
        throw "Refusing to remove drive root: $FullPath"
    }

    if (Test-Path $FullPath) {
        Remove-Item -Recurse -Force $FullPath
    }

    New-Item -ItemType Directory -Force -Path $FullPath | Out-Null
}

function Invoke-Native {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

function Expand-RuntimeArchive {
    param(
        [string]$ArchivePath,
        [string]$Destination
    )

    if (!(Test-Path $ArchivePath)) {
        throw "Runtime archive missing: $ArchivePath"
    }

    if (Test-Path $Destination) {
        Remove-Item -Recurse -Force $Destination
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Write-Host "[V1] Expanding runtime archive:" $ArchivePath
    Invoke-Native "tar" @("-xf", $ArchivePath, "-C", $Destination) (Get-Location).Path
}

function Repair-HuggingFaceHubFileDownload {
    param(
        [string]$ArchivePath,
        [string]$RuntimeRoot
    )

    $EntryName = "Lib/site-packages/huggingface_hub/file_download.py"
    $TargetPath = Join-Path $RuntimeRoot "Lib\site-packages\huggingface_hub\file_download.py"
    if (!(Test-Path $TargetPath)) {
        throw "Expected file missing after unpack: $TargetPath"
    }

    Write-Host "[V1] Restoring clean huggingface_hub/file_download.py"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entry = $archive.GetEntry($EntryName)
        if ($null -eq $entry) {
            $entry = $archive.GetEntry($EntryName.Replace('\', '/'))
        }
        if ($null -eq $entry) {
            throw "Archive entry missing: $EntryName"
        }

        $directory = Split-Path -Parent $TargetPath
        New-Item -ItemType Directory -Force -Path $directory | Out-Null

        $source = $entry.Open()
        try {
            $target = [System.IO.File]::Create($TargetPath)
            try {
                $source.CopyTo($target)
            }
            finally {
                $target.Dispose()
            }
        }
        finally {
            $source.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Repair-And-Validate-GptRuntime {
    param(
        [string]$ArchivePath,
        [string]$RuntimeRoot
    )

    Repair-HuggingFaceHubFileDownload -ArchivePath $ArchivePath -RuntimeRoot $RuntimeRoot

    $Python = Join-Path $RuntimeRoot "python.exe"
    if (!(Test-Path $Python)) {
        throw "Python missing from runtime: $Python"
    }

    $TargetFile = Join-Path $RuntimeRoot "Lib\site-packages\huggingface_hub\file_download.py"
    Invoke-Native $Python @("-m", "py_compile", $TargetFile) $RuntimeRoot
}

if (!(Test-Path $SourceReleaseRoot)) {
    throw "Source release root missing: $SourceReleaseRoot"
}

if (!(Test-Path $ConfigTemplate)) {
    throw "Launcher config template missing: $ConfigTemplate"
}

Write-Host "[V1] Preparing output:" $OutputRoot
Reset-Directory $OutputRoot

Copy-DirectoryMirror (Join-Path $SourceReleaseRoot "App") (Join-Path $OutputRoot "App")
Copy-DirectoryMirror (Join-Path $SourceReleaseRoot "Services") (Join-Path $OutputRoot "Services")

Write-Host "[V1] Publishing WPF launcher..."
if (Test-Path $PublishRoot) {
    Remove-Item -Recurse -Force $PublishRoot
}

dotnet publish $ProjectRoot `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishRoot

$PublishedExe = Join-Path $PublishRoot "VirtualPartnerLauncher.exe"
if (!(Test-Path $PublishedExe)) {
    throw "Published launcher missing: $PublishedExe"
}

Copy-Item -Force $PublishedExe (Join-Path $OutputRoot "Launcher.exe")
Copy-Item -Force $ConfigTemplate (Join-Path $OutputRoot "launcher_config.json")

if (!$SkipRuntimeBuild) {
    $RuntimeOutputRoot = Join-Path $OutputRoot "Runtime"
    $GptRuntimeArchive = Join-Path $PayloadRoot "gpt_tts_runtime.zip"
    $AsrRuntimeArchive = Join-Path $PayloadRoot "asr_runtime.zip"

    Expand-RuntimeArchive -ArchivePath $GptRuntimeArchive -Destination (Join-Path $RuntimeOutputRoot "gpt_tts")
    Expand-RuntimeArchive -ArchivePath $AsrRuntimeArchive -Destination (Join-Path $RuntimeOutputRoot "asr")
    Repair-And-Validate-GptRuntime -ArchivePath $GptRuntimeArchive -RuntimeRoot (Join-Path $RuntimeOutputRoot "gpt_tts")
}

Write-Host "[V1] Done:" $OutputRoot
