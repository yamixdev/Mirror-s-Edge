param(
    [string]$SourceProject = (Join-Path $PSScriptRoot '../..'),
    [string]$ProbeProject = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'MirrorEdgeParkourData/CollisionProbeProject'),
    [string]$UnityExe,
    [ValidateSet('CollisionProbe.Run', 'ReviewProbe.Run', 'GameplayHandsProbe.Run', 'PullUpProbe.Run', 'VaultJumpProbe.Run', 'ParkourObjectsProbe.Run', 'JumpSurvivalProbe.Run', 'SwingProbe.Run', 'ShimmyProbe.Run', 'FullBodyProbe.Run', 'PublishedSceneProbe.Run')][string]$EntryPoint = 'CollisionProbe.Run',
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$ReportName = 'collision-final'
)
$ErrorActionPreference = 'Stop'
$SourceProject = [IO.Path]::GetFullPath($SourceProject).TrimEnd('\')
$ProbeProject = [IO.Path]::GetFullPath($ProbeProject).TrimEnd('\')
if ($ProbeProject -eq $SourceProject -or $ProbeProject.StartsWith($SourceProject + '\', [StringComparison]::OrdinalIgnoreCase) -or $SourceProject.StartsWith($ProbeProject + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The isolated probe and source project must have separate directories.'
}
if (!$UnityExe) {
    $versionLine = Get-Content -LiteralPath (Join-Path $SourceProject 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: (.+)$' } | Select-Object -First 1
    if (!$versionLine) { throw 'Unity version was not found in ProjectVersion.txt.' }
    $UnityExe = Join-Path $env:ProgramFiles ('Unity/Hub/Editor/' + $Matches[1] + '/Editor/Unity.exe')
}
if (!(Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity executable not found: $UnityExe" }
$marker = Join-Path $ProbeProject '.collision-probe-owned'
if ((Test-Path -LiteralPath $ProbeProject) -and !(Test-Path -LiteralPath $marker)) {
    throw 'Existing probe directory has no ownership marker; choose a new -ProbeProject directory.'
}
$activeProbe = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($ProbeProject) }
if ($activeProbe) { throw 'The isolated probe already has a running Unity instance.' }
foreach ($folder in @('Assets/Editor', 'Packages', 'ProjectSettings', 'Evidence')) {
    New-Item -ItemType Directory -Path (Join-Path $ProbeProject $folder) -Force | Out-Null
}
Set-Content -LiteralPath $marker -Value 'Owned by Tools/CollisionProbe; isolated test project.'

# Compile the current production code and use real Unity collider queries.
# Keeping Library allows subsequent runs to reuse imported packages.
$sourceAssets = Join-Path $SourceProject 'Assets'
foreach ($file in Get-ChildItem -LiteralPath $sourceAssets -Recurse -File | Where-Object { $_.Extension -in @('.cs', '.asmdef') }) {
    $relative = $file.FullName.Substring($SourceProject.Length + 1)
    $destination = Join-Path $ProbeProject $relative
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    if (Test-Path -LiteralPath ($file.FullName + '.meta')) {
        Copy-Item -LiteralPath ($file.FullName + '.meta') -Destination ($destination + '.meta') -Force
    }
}
foreach ($relative in @('Packages/manifest.json', 'Packages/packages-lock.json', 'ProjectSettings/ProjectVersion.txt', 'ProjectSettings/ProjectSettings.asset')) {
    Copy-Item -LiteralPath (Join-Path $SourceProject $relative) -Destination (Join-Path $ProbeProject $relative) -Force
}
if ($EntryPoint -in @('GameplayHandsProbe.Run', 'PullUpProbe.Run', 'VaultJumpProbe.Run', 'ParkourObjectsProbe.Run', 'JumpSurvivalProbe.Run', 'SwingProbe.Run', 'ShimmyProbe.Run', 'FullBodyProbe.Run', 'PublishedSceneProbe.Run')) {
    # Render the real first-person model/animations without opening the user's scene.
    Copy-Item -LiteralPath (Join-Path $sourceAssets 'Source/Resources') -Destination (Join-Path $ProbeProject 'Assets/Source') -Recurse -Force
    if (Test-Path -LiteralPath (Join-Path $sourceAssets 'LocalFaithBody')) {
        Copy-Item -LiteralPath (Join-Path $sourceAssets 'LocalFaithBody') -Destination (Join-Path $ProbeProject 'Assets') -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $SourceProject 'ProjectSettings/InputManager.asset') -Destination (Join-Path $ProbeProject 'ProjectSettings/InputManager.asset') -Force
    $parkourAssets = Join-Path $sourceAssets 'Prefabs/Parkour'
    if (Test-Path -LiteralPath $parkourAssets) {
        New-Item -ItemType Directory -Path (Join-Path $ProbeProject 'Assets/Prefabs') -Force | Out-Null
        Copy-Item -LiteralPath $parkourAssets -Destination (Join-Path $ProbeProject 'Assets/Prefabs') -Recurse -Force
        Copy-Item -LiteralPath ($parkourAssets + '.meta') -Destination (Join-Path $ProbeProject 'Assets/Prefabs/Parkour.meta') -Force
    }
}
if ($EntryPoint -eq 'PublishedSceneProbe.Run') {
    foreach ($sceneFile in @('Assets/New Scene.unity', 'Assets/New Scene.unity.meta')) {
        Copy-Item -LiteralPath (Join-Path $SourceProject $sceneFile) -Destination (Join-Path $ProbeProject $sceneFile) -Force
    }
    foreach ($spawnFile in @('Assets/Spawn.prefab', 'Assets/Spawn.prefab.meta')) {
        Copy-Item -LiteralPath (Join-Path $SourceProject $spawnFile) -Destination (Join-Path $ProbeProject $spawnFile) -Force
    }
}
foreach ($probeSource in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File) {
    Copy-Item -LiteralPath $probeSource.FullName -Destination (Join-Path $ProbeProject ('Assets/Editor/' + $probeSource.Name)) -Force
}
$reportPath = Join-Path $ProbeProject ('Evidence/' + $ReportName + '.json')
$logPath = Join-Path $ProbeProject ('Evidence/' + $ReportName + '.log')
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
$previousReportName = $env:COLLISION_PROBE_REPORT
try {
    $env:COLLISION_PROBE_REPORT = $ReportName + '.json'
    $unityArguments = @('-batchmode')
    if ($EntryPoint -notin @('GameplayHandsProbe.Run', 'PullUpProbe.Run', 'VaultJumpProbe.Run', 'ParkourObjectsProbe.Run', 'JumpSurvivalProbe.Run', 'SwingProbe.Run', 'ShimmyProbe.Run', 'FullBodyProbe.Run', 'PublishedSceneProbe.Run')) { $unityArguments += '-nographics' }
    $unityArguments += @('-projectPath', ('"' + $ProbeProject + '"'), '-executeMethod', $EntryPoint, '-logFile', ('"' + $logPath + '"'))
    $probeProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    $null = $probeProcess.Handle
    if (!$probeProcess.WaitForExit(300000)) {
        Stop-Process -Id $probeProcess.Id
        throw 'The isolated Unity probe exceeded five minutes.'
    }
    if (!(Test-Path -LiteralPath $reportPath)) {
        # Full Unity logs may contain licensing arguments. Show only compiler diagnostics.
        Select-String -LiteralPath $logPath -Pattern 'error CS|Scripts have compiler errors|executeMethod.*failed' | ForEach-Object { $_.Line } | Write-Host
        throw "Probe did not produce a report (Unity exit $($probeProcess.ExitCode))."
    }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    foreach ($result in $report.results) {
        $status = if ($result.passed) { 'PASS' } else { 'FAIL' }
        Write-Host "$status $($result.name): $($result.detail)"
    }
    Write-Host "Report: $reportPath"
    if ($probeProcess.ExitCode -ne 0 -or $report.failures -ne 0) { throw "Collision probe failed: $($report.failures) checks; Unity exit $($probeProcess.ExitCode)." }
} finally {
    $env:COLLISION_PROBE_REPORT = $previousReportName
}
