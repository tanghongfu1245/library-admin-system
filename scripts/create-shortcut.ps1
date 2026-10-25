. (Join-Path $PSScriptRoot 'common.ps1')

$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop '图书管理员系统.lnk'
$powershell = (Get-Command powershell.exe).Source
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $powershell
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSScriptRoot\start.ps1`""
$shortcut.WorkingDirectory = $ProjectRoot
$shortcut.Description = '启动本地图书管理员系统'
$shortcut.IconLocation = "$powershell,0"
$shortcut.Save()
Write-Host "快捷方式已创建：$shortcutPath" -ForegroundColor Green

$databaseShortcutPath = Join-Path $desktop '查看图书数据库.lnk'
$databaseShortcut = $shell.CreateShortcut($databaseShortcutPath)
$databaseShortcut.TargetPath = $powershell
$databaseShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSScriptRoot\view-database.ps1`""
$databaseShortcut.WorkingDirectory = $ProjectRoot
$databaseShortcut.Description = '查看图书管理员系统的 MySQL 数据库'
$databaseShortcut.IconLocation = "$powershell,0"
$databaseShortcut.Save()
Write-Host "数据库查看快捷方式已创建：$databaseShortcutPath" -ForegroundColor Green

$workbenchPath = Join-Path (Join-Path $ToolRoot 'mysql-workbench') 'MySQL Workbench.exe'
if (Test-Path -LiteralPath $workbenchPath) {
    $workbenchShortcutPath = Join-Path $desktop 'MySQL Workbench.lnk'
    $workbenchShortcut = $shell.CreateShortcut($workbenchShortcutPath)
    $workbenchShortcut.TargetPath = $workbenchPath
    $workbenchShortcut.WorkingDirectory = Split-Path -Parent $workbenchPath
    $workbenchShortcut.Description = 'MySQL Workbench - 图书管理员系统数据库'
    $workbenchShortcut.Save()
    Write-Host "MySQL Workbench 快捷方式已创建：$workbenchShortcutPath" -ForegroundColor Green
}

$visualStudioPath = Join-Path $ToolRoot 'visual-studio\Common7\IDE\devenv.exe'
if (Test-Path -LiteralPath $visualStudioPath) {
    $visualStudioShortcutPath = Join-Path $desktop 'Visual Studio 2026.lnk'
    $visualStudioShortcut = $shell.CreateShortcut($visualStudioShortcutPath)
    $visualStudioShortcut.TargetPath = $visualStudioPath
    $visualStudioShortcut.WorkingDirectory = Split-Path -Parent $visualStudioPath
    $visualStudioShortcut.Description = 'Visual Studio Community 2026'
    $visualStudioShortcut.Save()

    $solutionShortcutPath = Join-Path $desktop '图书管理员系统（Visual Studio）.lnk'
    $solutionShortcut = $shell.CreateShortcut($solutionShortcutPath)
    $solutionShortcut.TargetPath = $visualStudioPath
    $solutionShortcut.Arguments = "`"$(Join-Path $ProjectRoot 'LibraryAdmin.slnx')`""
    $solutionShortcut.WorkingDirectory = $ProjectRoot
    $solutionShortcut.Description = '使用 Visual Studio 打开图书管理员系统'
    $solutionShortcut.Save()
    Write-Host "Visual Studio 快捷方式已创建：$visualStudioShortcutPath" -ForegroundColor Green
}

