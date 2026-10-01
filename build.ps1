$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework 4.x compiler not found.' }
$refs = @('/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll')
& $compiler /nologo /utf8output /target:winexe /platform:anycpu /optimize+ /out:EdgeDirect.exe /win32manifest:app.manifest $refs src\EdgeDirect.cs src\PopUi.cs
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
New-Item -ItemType Directory -Path artifacts -Force | Out-Null
& $compiler /nologo /utf8output /target:exe /platform:anycpu /optimize+ /main:EdgeDirect.Tests /out:artifacts\CoreTests.exe $refs src\EdgeDirect.cs src\PopUi.cs tests\CoreTests.cs tests\NativeUiTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& .\artifacts\CoreTests.exe
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
Get-Item -LiteralPath 'EdgeDirect.exe' | Select-Object FullName,Length
