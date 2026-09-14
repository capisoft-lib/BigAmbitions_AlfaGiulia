param([string]$ReuseProject,[string]$BuildRoot)
$ErrorActionPreference='Stop'
$modRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectRoot=[IO.Path]::GetFullPath((Join-Path $modRoot '../../..'))
$workspaceRoot=Split-Path $projectRoot -Parent
if($env:BA_MOD_BUILD_CLI) { throw 'Unset BA_MOD_BUILD_CLI before output-only build.' }
$scratchRoot=Join-Path $workspaceRoot '.analysis/alfagiulia/builds'
if($BuildRoot) { $scratchRoot=[IO.Path]::GetFullPath($BuildRoot).TrimEnd('\') }
$build=Join-Path $scratchRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
if($ReuseProject) {
    $candidate=(Resolve-Path -LiteralPath $ReuseProject).Path
    if(!$candidate.StartsWith($scratchRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Reuse must be a AlfaGiulia scratch project.' }
    $build=$candidate
}
New-Item -ItemType Directory -Path $build -Force | Out-Null
if(!$ReuseProject) { foreach($dir in @('Packages','ProjectSettings')) { Copy-Item -LiteralPath (Join-Path $projectRoot $dir) -Destination $build -Recurse -Force } }
$assets=Join-Path $build 'Assets'; New-Item -ItemType Directory -Path $assets -Force | Out-Null
if(!$ReuseProject) { foreach($dir in @('Editor','_BaDependencies')) { Copy-Item -LiteralPath (Join-Path $projectRoot ('Assets/'+$dir)) -Destination $assets -Recurse -Force } }
$mods=Join-Path $assets 'Mods'; New-Item -ItemType Directory -Path $mods -Force | Out-Null
if(!$ReuseProject) { Copy-Item -LiteralPath $modRoot -Destination $mods -Recurse -Force }
else {
    Get-ChildItem -LiteralPath $modRoot -File -Recurse | ForEach-Object {
        $relative=$_.FullName.Substring($modRoot.Length+1)
        $destination=Join-Path $mods ('AlfaGiulia/'+$relative)
        if(!(Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
            New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }
    }
}
$fixture=Join-Path $mods 'Example-Vehicle'; New-Item -ItemType Directory -Path $fixture -Force | Out-Null
if(!$ReuseProject) { foreach($name in @('TurboHonza.prefab','TurboHonza.prefab.meta','TurboHonza.asset','TurboHonza.asset.meta','Models','Models.meta','Audio','Audio.meta')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('Assets/Mods/Example-Vehicle/'+$name)) -Destination $fixture -Recurse -Force
} }
$log=Join-Path $build 'unity.log'
Write-Host "AlfaGiulia build project=$build"
# ModBuildCli finishes asynchronously after script reload. Unity CLI beta.5 injects
# -quit; follow the established mod build runner for this phase instead.
. (Join-Path $projectRoot 'scripts/_project.ps1')
$arguments=@('-batchmode','-disable-assembly-updater','-job-worker-count','2','-projectPath',('"'+$build+'"'),'-executeMethod','AlfaGiulia.Editor.AlfaGiuliaBuild.Run','-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
Wait-Process -Id $process.Id -Timeout 1200
$process.Refresh()
if($process.ExitCode -ne 0) { throw "Build failed: $log" }
if(!(Select-String -LiteralPath $log -SimpleMatch '[AlfaGiuliaValidation] PASS')) { throw "Missing validation marker: $log" }
if(!(Select-String -LiteralPath $log -SimpleMatch '[ModBuildCli] Build succeeded:')) { throw "Missing package success: $log" }
$packageLog=Join-Path $build 'package-validation.log'
& unity --json run $build -- -disable-assembly-updater -job-worker-count 2 -executeMethod AlfaGiulia.Editor.AlfaGiuliaBuild.VerifyPackage -logFile $packageLog
if($LASTEXITCODE -ne 0 -or !(Select-String -LiteralPath $packageLog -SimpleMatch '[AlfaGiuliaPackage] PASS')) { throw "Bundle verification failed: $packageLog" }
$compiled=Join-Path $build 'Output/AlfaGiulia'
Copy-Item -LiteralPath (Join-Path $build 'Assets/Mods/AlfaGiulia/ModManifest.asset') -Destination $compiled -Force
foreach($name in @('README.md','VALIDATION.md','SOURCE_ASSET.md','Harmony.LICENSE','Harmony-Mono-retargeting.txt')) {
    Copy-Item -LiteralPath (Join-Path $modRoot $name) -Destination $compiled -Force
}
$output=Join-Path $projectRoot 'Output/AlfaGiulia'; New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -Path (Join-Path $compiled '*') -Destination $output -Recurse -Force
foreach($name in @('Generated','AlfaGiuliaVisual.prefab','AlfaGiuliaVisual.prefab.meta','AlfaGiulia.asset','AlfaGiulia.asset.meta','ModManifest.asset','ModManifest.asset.meta')) {
    Copy-Item -LiteralPath (Join-Path $build ('Assets/Mods/AlfaGiulia/'+$name)) -Destination $modRoot -Recurse -Force
}
$builtMod=Join-Path $build 'Assets/Mods/AlfaGiulia'
Get-ChildItem -LiteralPath $builtMod -Recurse -File -Filter '*.meta' | ForEach-Object {
    $relative=$_.FullName.Substring($builtMod.Length+1)
    $destination=Join-Path $modRoot $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $build 'Assets/Mods/AlfaGiulia.meta') -Destination (Split-Path $modRoot -Parent) -Force
Get-ChildItem -LiteralPath $output -Recurse -File | ForEach-Object {
    [pscustomobject]@{File=$_.FullName.Substring($output.Length+1);Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
} | ConvertTo-Json | Set-Content (Join-Path $build 'package.json')
[pscustomobject]@{Output=$output;Build=$build;Log=$log;Installed=$false} | ConvertTo-Json
