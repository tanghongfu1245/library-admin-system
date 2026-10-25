param(
    [string]$RemoteUrl = '',
    [string]$UserName = '',
    [string]$UserEmail = '',
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $ProjectRoot

function Find-Git {
    $command = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        'E:\codex工作区\图书管理员系统\.tools\visual-studio\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe',
        'C:\Program Files\Git\cmd\git.exe',
        'C:\Program Files (x86)\Git\cmd\git.exe'
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw '没有找到 git.exe，请先安装 Git for Windows。'
}

$git = Find-Git

if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot '.git'))) {
    & $git init -b $Branch
    if ($LASTEXITCODE -ne 0) { throw 'git init 失败。' }
}

if ([string]::IsNullOrWhiteSpace($UserName)) {
    $UserName = Read-Host '请输入 Git 提交用户名（例如你的 GitHub 用户名）'
}
if ([string]::IsNullOrWhiteSpace($UserEmail)) {
    $UserEmail = Read-Host '请输入 Git 提交邮箱（建议使用 GitHub 的 noreply 邮箱）'
}
& $git config user.name $UserName
& $git config user.email $UserEmail

# 安全闸门：这些路径不应该出现在暂存区。
$forbidden = @('.tools/', '.packages/', '.publish/', '.data/', '.runtime/', '.logs/', '.backups/', 'MySQL数据库连接信息.txt')
& $git add -A
$staged = & $git diff --cached --name-only
foreach ($path in $staged) {
    foreach ($prefix in $forbidden) {
        if ($path -eq $prefix.TrimEnd('/') -or $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "暂存区出现不应提交的路径：$path。请检查 .gitignore，必要时执行 git reset。"
        }
    }
}
if (-not $staged) {
    Write-Host '没有需要提交的改动。' -ForegroundColor Yellow
}
else {
    & $git commit -m 'Initial commit: library admin system'
    if ($LASTEXITCODE -ne 0) { throw 'git commit 失败。' }
}

if ([string]::IsNullOrWhiteSpace($RemoteUrl)) {
    Write-Host ''
    Write-Host '本地提交已完成，但还没有远程仓库。' -ForegroundColor Yellow
    Write-Host '请在 GitHub 新建仓库后，重新运行：' -ForegroundColor Yellow
    Write-Host '  .\scripts\publish-github.ps1 -RemoteUrl "https://github.com/你的用户名/仓库名.git" -UserName "..." -UserEmail "..."' -ForegroundColor Cyan
    Write-Host '或者手动执行：' -ForegroundColor Yellow
    Write-Host '  git remote add origin <仓库地址>' -ForegroundColor Cyan
    Write-Host "  git push -u origin $Branch" -ForegroundColor Cyan
    exit 0
}

$existingRemote = & $git remote get-url origin 2>$null
if ($LASTEXITCODE -eq 0) {
    & $git remote set-url origin $RemoteUrl
}
else {
    & $git remote add origin $RemoteUrl
}

& $git push -u origin $Branch
if ($LASTEXITCODE -ne 0) {
    throw 'git push 失败。请确认仓库地址、分支和 GitHub 登录状态。'
}

Write-Host '已推送到 GitHub。' -ForegroundColor Green
