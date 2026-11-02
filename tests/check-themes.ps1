$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:exe /platform:anycpu /optimize+ /utf8output /warnaserror+ `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/resource:$projectRoot\assets\keyboard.ico,Keyboard.ico" "/win32manifest:$projectRoot\app.manifest" `
    "/resource:$projectRoot\predvolene-texty.txt,PresetTexts.txt" `
    /main:ThemeTests "/out:$output\ThemeTests.exe" "$projectRoot\Keyboard.cs" "$PSScriptRoot\ThemeTests.cs"
if ($LASTEXITCODE -ne 0) { throw "Sestavení testů selhalo: $LASTEXITCODE" }
& "$output\ThemeTests.exe"
if ($LASTEXITCODE -ne 0) { throw "Testy selhaly: $LASTEXITCODE" }
