$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /warnaserror+ `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/win32icon:$projectRoot\assets\keyboard.ico" "/resource:$projectRoot\assets\keyboard.ico,Keyboard.ico" `
    "/resource:$projectRoot\predvolene-texty.txt,PresetTexts.txt" `
    /main:IntegrationTests "/win32manifest:$projectRoot\app.manifest" `
    "/out:$output\KeyboardTests.exe" "$projectRoot\Keyboard.cs" "$PSScriptRoot\IntegrationTests.cs"
if ($LASTEXITCODE -ne 0) { throw "Sestavení testů selhalo: $LASTEXITCODE" }
Write-Output "Testovací aplikace: $output\KeyboardTests.exe"
