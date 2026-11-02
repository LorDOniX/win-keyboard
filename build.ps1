$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Chybí kompilátor .NET Framework 4.x.' }
$output = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output /warnaserror+ `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/win32icon:$projectRoot\assets\keyboard.ico" "/resource:$projectRoot\assets\keyboard.ico,Keyboard.ico" `
    "/resource:$projectRoot\predvolene-texty.txt,PresetTexts.txt" `
    "/win32manifest:$projectRoot\app.manifest" "/out:$output\CeskaKlavesnice.exe" "$projectRoot\Keyboard.cs"
if ($LASTEXITCODE -ne 0) { throw "Sestavení selhalo: $LASTEXITCODE" }
Write-Output "Hotovo: $output\CeskaKlavesnice.exe"
