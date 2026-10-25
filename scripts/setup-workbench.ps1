. (Join-Path $PSScriptRoot 'common.ps1')

$viewerFile = Join-Path $RuntimeRoot 'workbench-viewer.json'
if (Test-Path -LiteralPath $viewerFile) {
    $viewer = Get-Content -LiteralPath $viewerFile -Raw | ConvertFrom-Json
}
else {
    $viewer = [PSCustomObject]@{
        host = '127.0.0.1'
        port = $MySqlPort
        database = $DatabaseName
        user = 'library_viewer'
        password = New-RandomPassword
    }
    Write-Utf8NoBom -Path $viewerFile -Content ($viewer | ConvertTo-Json)
}

$databaseState = Initialize-MySqlDatabase
try {
    $grantSql = @"
CREATE USER IF NOT EXISTS '$($viewer.user)'@'127.0.0.1' IDENTIFIED BY '$($viewer.password)';
ALTER USER '$($viewer.user)'@'127.0.0.1' IDENTIFIED BY '$($viewer.password)';
REVOKE ALL PRIVILEGES, GRANT OPTION FROM '$($viewer.user)'@'127.0.0.1';
GRANT SELECT ON ``$DatabaseName``.* TO '$($viewer.user)'@'127.0.0.1';
FLUSH PRIVILEGES;
"@
    $null = Invoke-MySql -Sql $grantSql -OptionFile $RootOptionFile
}
finally {
    if ($databaseState.Started) {
        Stop-MySqlServer
    }
}

$workbenchPath = Join-Path (Join-Path $ToolRoot 'mysql-workbench') 'MySQL Workbench.exe'
if (-not (Test-Path -LiteralPath $workbenchPath)) {
    throw "未找到 MySQL Workbench：$workbenchPath"
}

$shell = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'MySQL Workbench.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $workbenchPath
$shortcut.WorkingDirectory = Split-Path -Parent $workbenchPath
$shortcut.Description = 'MySQL Workbench - 图书管理员系统数据库'
$shortcut.Save()

$connectionText = @"
MySQL Workbench 连接信息

Connection Name: 图书管理员系统
Hostname: $($viewer.host)
Port: $($viewer.port)
Username: $($viewer.user)
Password: $($viewer.password)
Default Schema: $($viewer.database)

用途：只读查看图书管理员系统的 MySQL 数据库。
"@
$connectionInfoPath = Join-Path $ProjectRoot 'MySQL数据库连接信息.txt'
Write-Utf8NoBom -Path $connectionInfoPath -Content $connectionText

Write-Host 'MySQL Workbench 已配置。' -ForegroundColor Green
Write-Host "桌面快捷方式：$shortcutPath"
Write-Host "连接信息：$connectionInfoPath"
