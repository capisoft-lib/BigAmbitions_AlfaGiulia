param([string]$SdkRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")))
$ErrorActionPreference = "Stop"
python (Join-Path $PSScriptRoot "build_release.py") --sdk $SdkRoot --mods AlfaGiulia
if ($LASTEXITCODE -ne 0) { throw "Optimized release build failed ($LASTEXITCODE)." }
