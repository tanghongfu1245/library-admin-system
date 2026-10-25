. (Join-Path $PSScriptRoot 'common.ps1')

$dump = Join-Path (Split-Path -Parent $PSScriptRoot) '数据\library_admin.sql'
if (-not (Test-Path -LiteralPath $dump)) {
    throw "找不到数据文件：$dump"
}

# 首次运行时创建数据库、应用账号和 root 凭据；如果 MySQL 已在运行则直接复用。
$null = Initialize-MySqlDatabase

$mysql = Join-Path $MysqlHome 'bin\mysql.exe'
$mysqlArguments = @(
    "--defaults-extra-file=$($RootOptionFile -replace '\\','/')",
    $DatabaseName
)
$quotedArguments = ($mysqlArguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
$commandLine = '"{0}" {1} < "{2}"' -f $mysql, $quotedArguments, $dump

& $env:ComSpec /c $commandLine
if ($LASTEXITCODE -ne 0) {
    throw '数据导入失败。请确认 MySQL 已启动，且数据文件完整。'
}

Write-Host '现有图书数据已导入。' -ForegroundColor Green
