. (Join-Path $PSScriptRoot 'common.ps1')

if (-not (Test-Path -LiteralPath (Join-Path $DotnetHome 'dotnet.exe'))) {
    throw "未找到 .NET SDK：$DotnetHome"
}
if (-not (Test-Path -LiteralPath (Join-Path $MysqlHome 'bin\mysqld.exe'))) {
    throw "未找到 MySQL：$MysqlHome"
}

$database = Initialize-MySqlDatabase
try {
    Set-ProjectDotnetEnvironment
    & (Join-Path $PSScriptRoot 'build.ps1')
    & (Join-Path $PSScriptRoot 'create-shortcut.ps1')
    & (Join-Path $PSScriptRoot 'schedule-backups.ps1')
    Write-Host ''
    Write-Host '安装检查完成。双击桌面快捷方式即可启动。' -ForegroundColor Green
}
finally {
    if ($database.Started) {
        Stop-MySqlServer
    }
}
