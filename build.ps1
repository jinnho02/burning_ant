# Builds the Windows game without opening the Unity editor.
# Easiest: double-click build.bat. Output: Builds\Windows\FX_master.exe
# First pulls the latest version of the current branch from GitHub (skip with -NoUpdate).
# Uses the Unity version in ProjectSettings\ProjectVersion.txt. Set UNITY_PATH to override the Unity.exe location.
param([switch]$NoUpdate)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Update-ToLatest {
    if (-not (Get-Command git -ErrorAction SilentlyContinue) -or -not (Test-Path (Join-Path $root '.git'))) {
        Write-Host 'Not a git checkout - skipping update.' -ForegroundColor Yellow
        return
    }
    Push-Location $root
    try {
        $branch = git rev-parse --abbrev-ref HEAD
        # Local edits could be lost or conflict, so never update over them.
        if (git status --porcelain) {
            Write-Host "Local changes found on '$branch' - skipping update, building your current files." -ForegroundColor Yellow
            return
        }
        Write-Host "Updating '$branch' to the latest version from GitHub ..."
        git pull --ff-only --quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Could not update automatically (no network, or your branch has commits not on GitHub). Building current files." -ForegroundColor Yellow
            return
        }
        git lfs pull  # character art etc. are stored in Git LFS
        Write-Host "Now at: $(git log -1 --format='%h %s')"
    }
    finally { Pop-Location }
}

if (-not $NoUpdate) { Update-ToLatest }

$version = (Select-String -Path "$root\ProjectSettings\ProjectVersion.txt" -Pattern 'm_EditorVersion: (.+)').Matches[0].Groups[1].Value.Trim()
$unity = if ($env:UNITY_PATH) { $env:UNITY_PATH } else { "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path $unity)) {
    Write-Host "Unity $version not found: $unity" -ForegroundColor Red
    Write-Host "Install Unity $version (with Windows Build Support) from Unity Hub, or set UNITY_PATH."
    exit 1
}

$log = Join-Path $root 'Logs\build.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
Write-Host "Building with Unity $version ... (first build takes a few minutes; close the Unity editor for this project first)"

$p = Start-Process -FilePath $unity -Wait -PassThru -WindowStyle Hidden -ArgumentList @(
    '-batchmode', '-quit',
    '-projectPath', "`"$root`"",
    '-executeMethod', 'BurningAnt.Editor.BuildScript.BuildWindows',
    '-logFile', "`"$log`""
)

if ($p.ExitCode -ne 0) {
    Write-Host "Build failed (exit $($p.ExitCode)). Full log: $log" -ForegroundColor Red
    Select-String -Path $log -Pattern 'error CS|Exception|another Unity instance|\[BuildScript\]' | Select-Object -Last 15 | ForEach-Object { $_.Line }
    exit $p.ExitCode
}

$exe = Get-ChildItem (Join-Path $root 'Builds\Windows') -Filter *.exe | Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1
Write-Host "Build done: $($exe.FullName)" -ForegroundColor Green
