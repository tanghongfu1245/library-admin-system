param(
    [string]$OutputDirectory = '',
    [string]$ZipPath = ''
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Copy-Tree {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path -LiteralPath $Source)) {
        throw "找不到要复制的内容：$Source"
    }
    $null = New-Item -ItemType Directory -Path $Destination -Force
    $null = robocopy $Source $Destination /E /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) {
        throw "复制失败（robocopy 退出码 $LASTEXITCODE）：$Source"
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    if (Test-Path -LiteralPath 'E:\') {
        $OutputDirectory = 'E:\LibraryAdmin-运行包'
    }
    else {
        $OutputDirectory = Join-Path ([Environment]::GetFolderPath('Desktop')) 'LibraryAdmin-运行包'
    }
}
if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    $ZipPath = "$OutputDirectory.zip"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$ZipPath = [System.IO.Path]::GetFullPath($ZipPath)
if ($OutputDirectory.TrimEnd('\') -eq $ProjectRoot.TrimEnd('\')) {
    throw '输出目录不能是项目根目录。'
}

Write-Host '1/6 停止正在运行的应用和 MySQL...' -ForegroundColor Cyan
try {
    & (Join-Path $PSScriptRoot 'stop.ps1')
}
catch {
    Write-Warning "停止服务时出错（如果本来没运行可以忽略）：$($_.Exception.Message)"
}

Write-Host '2/6 重新发布 .publish...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) {
    throw '发布失败。'
}

Write-Host '3/6 生成最新数据库备份...' -ForegroundColor Cyan
try {
    & (Join-Path $PSScriptRoot 'backup.ps1')
}
catch {
    Write-Warning "自动备份失败，将使用 .backups 中最新的一份：$($_.Exception.Message)"
}
$dump = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot '.backups') -Filter 'library_admin_*.sql' -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $dump) {
    throw '没有找到数据库备份，无法把现有图书数据放进运行包。'
}
Write-Host "使用数据备份：$($dump.Name)" -ForegroundColor DarkGray

Write-Host '4/6 组装运行包...' -ForegroundColor Cyan
if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force

Copy-Tree (Join-Path $ProjectRoot '.publish') (Join-Path $OutputDirectory '.publish')
Copy-Tree (Join-Path $ProjectRoot '.tools\dotnet') (Join-Path $OutputDirectory '.tools\dotnet')
Copy-Tree (Join-Path $ProjectRoot '.tools\mysql') (Join-Path $OutputDirectory '.tools\mysql')

$scriptsSource = Join-Path $ProjectRoot 'scripts'
$scriptsTarget = Join-Path $OutputDirectory 'scripts'
$null = New-Item -ItemType Directory -Path $scriptsTarget -Force
$runtimeScripts = @(
    'common.ps1',
    'start.ps1',
    'stop.ps1',
    'create-shortcut.ps1',
    'backup.ps1',
    'test-backup.ps1',
    'schedule-backups.ps1',
    'seed-books.ps1',
    'view-database.ps1',
    'import-data.ps1'
)
foreach ($name in $runtimeScripts) {
    $source = Join-Path $scriptsSource $name
    if (-not (Test-Path -LiteralPath $source)) {
        throw "缺少脚本：$source"
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $scriptsTarget $name) -Force
}

# 运行包不再依赖 E: 盘；使用 C:\Users\Public 下的英文别名。
$commonPath = Join-Path $scriptsTarget 'common.ps1'
$common = Get-Content -LiteralPath $commonPath -Raw
$oldAlias = "`$script:ProjectAlias = 'E:\library-admin-runtime'"
$newAlias = "`$script:ProjectAlias = 'C:\Users\Public\library-admin-runtime'"
if (-not $common.Contains($oldAlias)) {
    throw 'common.ps1 中没有找到预期的 ProjectAlias 行。'
}
$common = $common.Replace($oldAlias, $newAlias)
Write-Utf8NoBom $commonPath $common

# 运行包没有 src，去掉 start.ps1 里的源码时间戳检查。
$startPath = Join-Path $scriptsTarget 'start.ps1'
$start = (Get-Content -LiteralPath $startPath -Raw) -replace "`r`n", "`n"
$oldStart = @'
    $publishedDll = Join-Path $ProjectRoot '.publish\LibraryAdmin.Web.dll'
    $publishRequired = -not (Test-Path -LiteralPath $publishedDll)
    if (-not $publishRequired) {
        $publishTime = (Get-Item -LiteralPath $publishedDll).LastWriteTimeUtc
        $sourceRoot = Join-Path $ProjectRoot 'src\LibraryAdmin.Web'
        $newestSource = Get-ChildItem -LiteralPath $sourceRoot -File -Recurse |
            Where-Object {
                $_.FullName -notmatch '\\(bin|obj)\\' -and
                $_.Extension -in @('.cs', '.cshtml', '.csproj', '.json', '.sql', '.css')
            } |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        $publishRequired = $null -ne $newestSource -and $newestSource.LastWriteTimeUtc -gt $publishTime
    }
'@
$oldStart = $oldStart -replace "`r`n", "`n"
$newStart = @'
    $publishedDll = Join-Path $ProjectRoot '.publish\LibraryAdmin.Web.dll'
    $publishRequired = -not (Test-Path -LiteralPath $publishedDll)
'@
$newStart = $newStart -replace "`r`n", "`n"
if (-not $start.Contains($oldStart)) {
    throw 'start.ps1 中没有找到预期的源码检查块，请检查脚本是否被修改。'
}
$start = $start.Replace($oldStart, $newStart)
Write-Utf8NoBom $startPath $start

$dataDirectory = Join-Path $OutputDirectory '数据'
$null = New-Item -ItemType Directory -Path $dataDirectory -Force
Copy-Item -LiteralPath $dump.FullName -Destination (Join-Path $dataDirectory 'library_admin.sql') -Force

$startCmd = @'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\start.ps1"
pause
'@
$stopCmd = @'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\stop.ps1"
pause
'@
$importCmd = @'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\import-data.ps1"
pause
'@
Write-Utf8NoBom (Join-Path $OutputDirectory '启动.cmd') $startCmd
Write-Utf8NoBom (Join-Path $OutputDirectory '停止.cmd') $stopCmd
Write-Utf8NoBom (Join-Path $OutputDirectory '导入数据.cmd') $importCmd

$readme = @'
图书管理员系统 - 运行包

一、首次使用
1. 解压到短路径，例如 C:\LibraryAdmin，路径尽量不要有空格和中文。
2. 双击“导入数据.cmd”，等待提示“现有图书数据已导入”。
3. 双击“启动.cmd”，浏览器会打开 http://127.0.0.1:8080。
4. 初始账号 admin，密码 123456；登录后请立即修改密码。

二、日常使用
- 启动：双击“启动.cmd”
- 停止：双击“停止.cmd”
- 数据目录：.data；不要删除
- 数据库密码和运行状态：.runtime；不要外传

三、注意事项
- 首次启动会自动创建数据库，不需要单独安装 MySQL。
- 3306 和 8080 端口不能被其他程序占用；如果机器上已有 MySQL，请先停止它的服务。
- 第一次启动后不要移动文件夹；如果移动了，删除 C:\Users\Public\library-admin-runtime 后重新启动。
- “导入数据.cmd”只需要在首次使用或需要恢复数据时运行；重复运行会用备份覆盖当前数据。
- 需要每天自动备份，右键以管理员身份运行 scripts\schedule-backups.ps1。
'@
Write-Utf8NoBom (Join-Path $OutputDirectory '使用说明.txt') $readme

Write-Host '5/6 压缩运行包...' -ForegroundColor Cyan
if (Test-Path -LiteralPath $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}
$sevenZip = @(
    'C:\Program Files\7-Zip\7z.exe',
    'C:\Program Files (x86)\7-Zip\7z.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if ($sevenZip) {
    & $sevenZip a -mx=5 -tzip $ZipPath $OutputDirectory
    if ($LASTEXITCODE -ge 2) {
        throw "7-Zip 压缩失败，退出码 $LASTEXITCODE。"
    }
}
else {
    $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path -LiteralPath $tar)) {
        throw '没有找到 7-Zip 或 tar.exe，无法压缩。'
    }
    & $tar -a -c -f $ZipPath -C (Split-Path -Parent $OutputDirectory) (Split-Path -Leaf $OutputDirectory)
    if ($LASTEXITCODE -ne 0) {
        throw "tar 压缩失败，退出码 $LASTEXITCODE。"
    }
}

$directorySize = (Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | Measure-Object Length -Sum).Sum
$zipSize = (Get-Item -LiteralPath $ZipPath).Length

Write-Host '6/6 完成。' -ForegroundColor Green
Write-Host ("运行包目录：{0}（{1:N0} MB）" -f $OutputDirectory, ($directorySize / 1MB))
Write-Host ("压缩包：{0}（{1:N0} MB）" -f $ZipPath, ($zipSize / 1MB))
