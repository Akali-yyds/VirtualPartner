$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Python = Join-Path $Root ".venv\Scripts\python.exe"

if (!(Test-Path $Python)) {
    throw "ASR venv is missing. Run setup_venv.bat first."
}

Set-Location $Root
& $Python (Join-Path $Root "asr_service.py")
