param(
    [int]$RetentionDays = 30
)

. (Join-Path $PSScriptRoot 'common.ps1')

$backupRoot = Join-Path $ProjectRoot '.backups'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
$databaseState = Initialize-MySqlDatabase
try {
    $config = $databaseState.Config
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupFile = Join-Path $backupRoot "library_admin_$timestamp.sql"
    $mysqldump = Join-Path $MysqlHome 'bin\mysqldump.exe'
    $arguments = @(
        "--defaults-extra-file=$($RootOptionFile -replace '\\','/')",
        "--result-file=$($backupFile -replace '\\','/')",
        '--single-transaction',
        '--skip-triggers',
        '--skip-events',
        '--no-tablespaces',
        '--default-character-set=utf8mb4',
        $config.database
    )
    & $mysqldump @arguments
    if ($LASTEXITCODE -ne 0) {
        throw 'mysqldump 执行失败。'
    }

    $backupItem = Get-Item -LiteralPath $backupFile
    if ($backupItem.Length -lt 1000) {
        throw "备份文件异常过小：$backupFile"
    }

    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -LiteralPath $backupRoot -Filter 'library_admin_*.sql' -File |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

    $message = "[{0:yyyy-MM-dd HH:mm:ss}] 备份成功：{1} ({2:N0} bytes)" -f (Get-Date), $backupItem.Name, $backupItem.Length
    Write-Host $message -ForegroundColor Green
    Add-Content -LiteralPath (Join-Path $LogRoot 'backup.log') -Value $message -Encoding UTF8
}
finally {
    if ($databaseState.Started) {
        Stop-MySqlServer
    }
}
