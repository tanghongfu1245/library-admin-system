. (Join-Path $PSScriptRoot 'common.ps1')

$config = Get-OrCreateDatabaseConfig
$databaseState = Initialize-MySqlDatabase
try {
    $appOptionFile = New-AppOptionFile -Config $config
    $summarySql = @"
USE ``$($config.database)``;
SHOW TABLES;
SELECT 'books' AS table_name, COUNT(*) AS row_count FROM books
UNION ALL SELECT 'book_copies', COUNT(*) FROM book_copies
UNION ALL SELECT 'user_accounts', COUNT(*) FROM user_accounts
UNION ALL SELECT 'loans', COUNT(*) FROM loans
UNION ALL SELECT 'admin_applications', COUNT(*) FROM admin_applications;
"@

    Write-Host ''
    Write-Host '图书管理员系统数据库概览' -ForegroundColor Green
    Write-Host '========================' -ForegroundColor DarkGreen
    Invoke-MySql -Sql $summarySql -OptionFile $appOptionFile
    Write-Host ''
    Write-Host '可以直接输入以下 SQL：' -ForegroundColor Cyan
    Write-Host '  SHOW TABLES;'
    Write-Host '  DESCRIBE books;'
    Write-Host '  SELECT * FROM books LIMIT 10;'
    Write-Host '  SELECT * FROM book_copies LIMIT 10;'
    Write-Host '  SELECT * FROM user_accounts;'
    Write-Host '  exit'
    Write-Host ''
    Write-Host '注意：当前使用应用数据库账号，请优先执行只读 SELECT 查询。' -ForegroundColor Yellow
    Write-Host ''

    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $mysql = Join-Path $MysqlHome 'bin\mysql.exe'
    & $mysql `
        "--defaults-extra-file=$($appOptionFile -replace '\\','/')" `
        "--database=$($config.database)" `
        '--default-character-set=utf8mb4'
}
finally {
    if ($databaseState.Started) {
        Stop-MySqlServer
    }
}
