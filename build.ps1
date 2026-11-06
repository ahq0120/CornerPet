param([ValidateSet('x86','x64')][string]$Architecture = 'x86')
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('Native.cs','CornerPet.cs','SelfTest.cs','ImageAsset.cs','LibraryStore.cs','LibraryServer.cs','LibraryClient.cs','LibraryWindow.cs','HostProgram.cs','LibraryTests.cs','ScreenPlacement.cs','GlobalShortcuts.cs','StartupRegistration.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$refs = @('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Xml.dll','/reference:System.Security.dll','/reference:System.Net.Http.dll','/reference:System.Web.Extensions.dll')
& $compiler /nologo /target:winexe /main:Program /platform:$Architecture /optimize+ "/out:$PSScriptRoot\CornerPet.exe" $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
& $compiler /nologo /target:winexe /main:HostProgram /platform:$Architecture /optimize+ "/out:$PSScriptRoot\CornerPet-LibraryHost.exe" $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'Host build failed' }
Write-Host 'Built CornerPet.exe and CornerPet-LibraryHost.exe'
