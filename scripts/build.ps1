. (Join-Path $PSScriptRoot 'common.ps1')

Set-ProjectDotnetEnvironment
$publishDirectory = Join-Path $ProjectRoot '.publish'
$project = Join-Path $ProjectRoot 'src\LibraryAdmin.Web\LibraryAdmin.Web.csproj'

dotnet publish $project -c Release -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw '项目发布失败。'
}

Write-Host '发布完成。' -ForegroundColor Green
