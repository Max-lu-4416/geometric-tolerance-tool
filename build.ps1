param([string]$Dotnet = 'dotnet', [string]$OutputDir = (Join-Path $PSScriptRoot 'artifacts/geometric-tolerance-tool-v1.3.7'))
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
& $Dotnet run --project (Join-Path $projectRoot 'Tests/Tolerance.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Query tests failed.' }
& $Dotnet publish (Join-Path $projectRoot 'App/Tolerance.App.csproj') -c Release -r win-x64 --self-contained true -o $OutputDir
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $outputDir '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'VALIDATION.md') -Destination (Join-Path $outputDir '验证记录.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tests/data-width-notes.csv') -Destination (Join-Path $outputDir '收录宽度差异.csv') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $outputDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'NOTICE.md') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $outputDir -Recurse -Force
$symbols = Join-Path $outputDir 'Tolerance.Core.pdb'
if (Test-Path -LiteralPath $symbols) { Remove-Item -LiteralPath $symbols }
Write-Output $outputDir
