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
$RepoRoot = Split-Path -Parent $ScriptRoot
$ProjectRoot = Join-Path $ScriptRoot "VirtualPartnerLauncher"
$ConfigTemplate = Join-Path $ScriptRoot "launcher_config.v1.json"
$PublishRoot = Join-Path $ScriptRoot "publish"
$PromptSourceRoot = Join-Path $RepoRoot "VirtualPartner\Assets\VirtualPartner\Prompts"

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

function Copy-DirectoryMirrorFiltered {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludedFiles = @()
    )

    if (!(Test-Path $Source)) {
        throw "Source directory missing: $Source"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $arguments = @($Source, $Destination, "/MIR", "/NFL", "/NDL", "/NJH", "/NJS", "/NP")
    if ($ExcludedFiles.Length -gt 0) {
        $arguments += "/XF"
        $arguments += $ExcludedFiles
    }

    & robocopy @arguments | Out-Null
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
        Write-Host "[V1] huggingface_hub/file_download.py not present; skipping restore."
        return
    }

    Write-Host "[V1] Restoring clean huggingface_hub/file_download.py"
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entry = $archive.Entries |
            Where-Object { ($_.FullName -replace '^\./', '') -eq $EntryName } |
            Select-Object -First 1
        if ($null -eq $entry) {
            Write-Host "[V1] Archive entry missing; keeping unpacked huggingface_hub/file_download.py."
            return
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

function Get-CondaDeclaredBinaryFiles {
    param(
        [string]$RuntimeRoot
    )

    $MetaRoot = Join-Path $RuntimeRoot "conda-meta"
    if (!(Test-Path $MetaRoot)) {
        return @()
    }

    $Items = New-Object System.Collections.Generic.List[object]
    Get-ChildItem -Path $MetaRoot -Filter "*.json" | ForEach-Object {
        try {
            $Package = Get-Content $_.FullName -Raw | ConvertFrom-Json
            foreach ($RelativePath in @($Package.files)) {
                if ([string]::IsNullOrWhiteSpace($RelativePath)) {
                    continue
                }

                $NormalizedPath = $RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
                if ($NormalizedPath -notmatch "\.(dll|exe|pyd)$") {
                    continue
                }

                $Items.Add([pscustomobject]@{
                    Package = [string]$Package.name
                    Version = [string]$Package.version
                    RelativePath = $NormalizedPath
                    FullPath = Join-Path $RuntimeRoot $NormalizedPath
                }) | Out-Null
            }
        }
        catch {
            throw "Failed to inspect conda package metadata: $($_.FullName). $($_.Exception.Message)"
        }
    }

    return $Items.ToArray()
}

function Repair-MissingCondaBinaryFiles {
    param(
        [string]$RuntimeName,
        [string]$RuntimeRoot,
        [string[]]$CandidateRuntimeRoots
    )

    $Missing = New-Object System.Collections.Generic.List[object]
    foreach ($Item in Get-CondaDeclaredBinaryFiles -RuntimeRoot $RuntimeRoot) {
        if (Test-Path $Item.FullPath) {
            continue
        }

        $Source = $null
        foreach ($CandidateRoot in @($CandidateRuntimeRoots)) {
            if ([string]::IsNullOrWhiteSpace($CandidateRoot)) {
                continue
            }

            $CandidatePath = Join-Path $CandidateRoot $Item.RelativePath
            if (Test-Path $CandidatePath) {
                $Source = $CandidatePath
                break
            }
        }

        if ($Source) {
            $TargetDirectory = Split-Path -Parent $Item.FullPath
            New-Item -ItemType Directory -Force -Path $TargetDirectory | Out-Null
            Copy-Item -Force $Source $Item.FullPath
            Write-Host "[V1] Repaired $RuntimeName runtime file:" $Item.RelativePath
            continue
        }

        $Missing.Add($Item) | Out-Null
    }

    if ($Missing.Count -gt 0) {
        $Preview = $Missing |
            Select-Object -First 20 |
            ForEach-Object { "$($_.Package)-$($_.Version): $($_.RelativePath)" }
        throw "$RuntimeName runtime has missing conda package files:`n$($Preview -join "`n")"
    }
}

function Assert-CondaRuntimeBinaryFiles {
    param(
        [string]$RuntimeName,
        [string]$RuntimeRoot
    )

    $Missing = Get-CondaDeclaredBinaryFiles -RuntimeRoot $RuntimeRoot |
        Where-Object { !(Test-Path $_.FullPath) }

    if (@($Missing).Count -gt 0) {
        $Preview = $Missing |
            Select-Object -First 20 |
            ForEach-Object { "$($_.Package)-$($_.Version): $($_.RelativePath)" }
        throw "$RuntimeName runtime still has missing conda package files:`n$($Preview -join "`n")"
    }

    Write-Host "[V1] $RuntimeName conda runtime files validated."
}

function Invoke-CleanRuntimeCommand {
    param(
        [string]$RuntimeName,
        [string]$RuntimeRoot,
        [string]$FilePath,
        [string[]]$Arguments
    )

    if (!(Test-Path $FilePath)) {
        throw "$RuntimeName smoke test executable missing: $FilePath"
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
            throw "$RuntimeName smoke test failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
        }
    }
    finally {
        $env:PATH = $OldPath
    }
}

function Repair-And-Validate-CondaRuntime {
    param(
        [string]$RuntimeName,
        [string]$RuntimeRoot,
        [string[]]$CandidateRuntimeRoots,
        [switch]$ValidateFfmpeg
    )

    Repair-MissingCondaBinaryFiles `
        -RuntimeName $RuntimeName `
        -RuntimeRoot $RuntimeRoot `
        -CandidateRuntimeRoots $CandidateRuntimeRoots
    Assert-CondaRuntimeBinaryFiles -RuntimeName $RuntimeName -RuntimeRoot $RuntimeRoot

    Invoke-CleanRuntimeCommand `
        -RuntimeName $RuntimeName `
        -RuntimeRoot $RuntimeRoot `
        -FilePath (Join-Path $RuntimeRoot "python.exe") `
        -Arguments @("-V")

    if ($ValidateFfmpeg) {
        Invoke-CleanRuntimeCommand `
            -RuntimeName $RuntimeName `
            -RuntimeRoot $RuntimeRoot `
            -FilePath (Join-Path $RuntimeRoot "Library\bin\ffmpeg.exe") `
            -Arguments @("-version")
    }

    Write-Host "[V1] $RuntimeName clean PATH smoke test passed."
}

function Repair-And-Validate-GptRuntime {
    param(
        [string]$ArchivePath,
        [string]$RuntimeRoot,
        [string[]]$CandidateRuntimeRoots
    )

    Repair-HuggingFaceHubFileDownload -ArchivePath $ArchivePath -RuntimeRoot $RuntimeRoot
    Repair-And-Validate-CondaRuntime `
        -RuntimeName "GPT-SoVITS/TTS" `
        -RuntimeRoot $RuntimeRoot `
        -CandidateRuntimeRoots $CandidateRuntimeRoots `
        -ValidateFfmpeg

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
Copy-DirectoryMirrorFiltered `
    $PromptSourceRoot `
    (Join-Path $OutputRoot "App\VirtualPartner_Data\VirtualPartner\Prompts") `
    @("*.meta")

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
    $GptRuntimeRoot = Join-Path $RuntimeOutputRoot "gpt_tts"
    $AsrRuntimeRoot = Join-Path $RuntimeOutputRoot "asr"

    Expand-RuntimeArchive -ArchivePath $GptRuntimeArchive -Destination $GptRuntimeRoot
    Expand-RuntimeArchive -ArchivePath $AsrRuntimeArchive -Destination $AsrRuntimeRoot
    Repair-And-Validate-GptRuntime `
        -ArchivePath $GptRuntimeArchive `
        -RuntimeRoot $GptRuntimeRoot `
        -CandidateRuntimeRoots @($AsrRuntimeRoot)
    Repair-And-Validate-CondaRuntime `
        -RuntimeName "ASR" `
        -RuntimeRoot $AsrRuntimeRoot `
        -CandidateRuntimeRoots @($GptRuntimeRoot)
}

Write-Host "[V1] Done:" $OutputRoot
