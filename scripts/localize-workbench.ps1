param(
    [switch]$Restore
)

. (Join-Path $PSScriptRoot 'common.ps1')

$workbenchRoot = Join-Path $ToolRoot 'mysql-workbench'
$payloadRoot = Join-Path $ToolRoot 'mysql-workbench-zh'
$resourcesApp = Join-Path $workbenchRoot 'resources\app'
$indexPath = Join-Path $resourcesApp 'frontend\build\index.html'
$mainPath = Join-Path $resourcesApp 'src\main.cjs'
$translationDir = Join-Path $resourcesApp 'frontend\build\i18n-zh'
$translationMain = Join-Path $resourcesApp 'src\i18n-zh.cjs'
$backupRoot = Join-Path $workbenchRoot 'localization-zh-backup'
$indexBackup = Join-Path $backupRoot 'index.html'
$mainBackup = Join-Path $backupRoot 'main.cjs'
$manifestPath = Join-Path $backupRoot 'manifest.json'

if (Get-Process -Name 'MySQL Workbench' -ErrorAction SilentlyContinue) {
    throw '请先完全退出 MySQL Workbench，再执行汉化或恢复。'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Set-FileWritable {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path
        if ($item.IsReadOnly) {
            $item.IsReadOnly = $false
        }
    }
}

if ($Restore) {
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "未找到汉化备份清单：$manifestPath"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $indexHash = (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash
    $mainHash = (Get-FileHash -LiteralPath $mainPath -Algorithm SHA256).Hash
    if ($indexHash -ne $manifest.indexPatchedHash -or $mainHash -ne $manifest.mainPatchedHash) {
        throw 'Workbench 界面文件在本补丁安装后发生过改变，已停止恢复以避免覆盖新版本。'
    }

    Set-FileWritable $indexPath
    Set-FileWritable $mainPath
    Copy-Item -LiteralPath $indexBackup -Destination $indexPath -Force
    Copy-Item -LiteralPath $mainBackup -Destination $mainPath -Force
    if (Test-Path -LiteralPath $translationDir) {
        Remove-Item -LiteralPath $translationDir -Recurse -Force
    }
    Remove-FileIfExists -Path $translationMain
    Remove-FileIfExists -Path $manifestPath
    Write-Host 'MySQL Workbench 已恢复为原始英文界面。' -ForegroundColor Green
    exit 0
}

if (-not (Test-Path -LiteralPath $indexPath) -or -not (Test-Path -LiteralPath $mainPath)) {
    throw "未找到 MySQL Workbench 界面文件：$resourcesApp"
}
if (-not (Test-Path -LiteralPath (Join-Path $payloadRoot 'renderer-zh.js')) -or
    -not (Test-Path -LiteralPath (Join-Path $payloadRoot 'i18n-zh.cjs'))) {
    throw "未找到汉化载荷：$payloadRoot"
}

New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $indexBackup)) {
    Copy-Item -LiteralPath $indexPath -Destination $indexBackup -Force
}
if (-not (Test-Path -LiteralPath $mainBackup)) {
    Copy-Item -LiteralPath $mainPath -Destination $mainBackup -Force
}

Set-FileWritable $indexPath
Set-FileWritable $mainPath

New-Item -ItemType Directory -Path $translationDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $payloadRoot 'renderer-zh.js') -Destination $translationDir -Force
Copy-Item -LiteralPath (Join-Path $payloadRoot 'zh-CN.json') -Destination $translationDir -Force
Copy-Item -LiteralPath (Join-Path $payloadRoot 'i18n-zh.cjs') -Destination $translationMain -Force

$indexHtml = [System.IO.File]::ReadAllText($indexPath)
$indexChanged = $false
if ($indexHtml.Contains('<html lang="en">')) {
    $indexHtml = $indexHtml.Replace('<html lang="en">', '<html lang="zh-CN">')
    $indexChanged = $true
}
if ($indexHtml -notmatch 'i18n-zh/renderer-zh\.js') {
    $marker = '<script type="module"'
    $position = $indexHtml.IndexOf($marker, [System.StringComparison]::Ordinal)
    if ($position -lt 0) {
        throw '无法定位 Workbench 前端入口，未修改 index.html。'
    }
    $indexHtml = $indexHtml.Insert(
        $position,
        '  <script src="i18n-zh/renderer-zh.js"></script>' + "`r`n  ")
    $indexChanged = $true
}
if ($indexChanged) {
    [System.IO.File]::WriteAllText($indexPath, $indexHtml, $utf8NoBom)
}

$mainCode = [System.IO.File]::ReadAllText($mainPath)
$requireAnchor = 'const { buildNativeMenuTemplate, resolvePositioningItem, translatePopupCoordinates } = require("./native-menu.cjs");'
$menuReturnAnchor = 'return Menu.buildFromTemplate(template);'
$menuConstAnchor = 'const menu = Menu.buildFromTemplate(template);'
if (-not $mainCode.Contains('require("./i18n-zh.cjs")')) {
    foreach ($anchor in @($requireAnchor, $menuReturnAnchor, $menuConstAnchor)) {
        if (-not $mainCode.Contains($anchor)) {
            throw "Workbench 26.10 的主进程锚点不存在：$anchor"
        }
    }

    $mainCode = $mainCode.Replace(
        $requireAnchor,
        $requireAnchor + "`r`n`r`n" +
        'const { translateLabel, translateMenuTemplate } = require("./i18n-zh.cjs");' + "`r`n" +
        'app.commandLine.appendSwitch("lang", "zh-CN");')
    $mainCode = $mainCode.Replace(
        $menuReturnAnchor,
        'return Menu.buildFromTemplate(translateMenuTemplate(template));')
    $mainCode = $mainCode.Replace(
        $menuConstAnchor,
        'const menu = Menu.buildFromTemplate(translateMenuTemplate(template));')
    $mainCode = $mainCode.Replace(
        'dialog.showErrorBox("Save Failed"',
        'dialog.showErrorBox(translateLabel("Save Failed")')
    $mainCode = $mainCode.Replace(
        '"Frontend Build Missing"',
        'translateLabel("Frontend Build Missing")')
    [System.IO.File]::WriteAllText($mainPath, $mainCode, $utf8NoBom)
}

$node = (Get-Command node -ErrorAction Stop).Source
& $node --check $mainPath
if ($LASTEXITCODE -ne 0) {
    throw '汉化后的 main.cjs 语法检查失败。'
}

$manifest = [PSCustomObject]@{
    workbenchVersion = (Get-Content -LiteralPath (Join-Path $resourcesApp 'package.json') -Raw | ConvertFrom-Json).version
    sourceRepository = 'https://github.com/wsnjwb/MySQL-Workbench-chinese'
    sourceCommit = 'c6d9dc40d3c912998c30ef908e2298e6448ae3de'
    installedAt = (Get-Date).ToString('o')
    indexPatchedHash = (Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash
    mainPatchedHash = (Get-FileHash -LiteralPath $mainPath -Algorithm SHA256).Hash
}
Write-Utf8NoBom -Path $manifestPath -Content ($manifest | ConvertTo-Json)

Write-Host 'MySQL Workbench 汉化已安装。' -ForegroundColor Green
Write-Host "原始文件备份：$backupRoot"
Write-Host '重新启动 MySQL Workbench 后生效。'
