. (Join-Path $PSScriptRoot 'common.ps1')

Set-ProjectDotnetEnvironment
$database = Initialize-MySqlDatabase
$startedMysql = $database.Started
try {
    $testConnection = Get-LibraryConnectionString -Config $database.Config
    $env:ConnectionStrings__LibraryAdmin = $testConnection.Replace(
        "Database=$($database.Config.database);",
        "Database=$($database.Config.database)_test;")
    dotnet test (Join-Path $ProjectRoot 'LibraryAdmin.slnx') -c Release
    if ($LASTEXITCODE -ne 0) {
        throw '测试失败。'
    }
}
finally {
    if ($startedMysql) {
        Stop-MySqlServer
    }
}
