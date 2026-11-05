param([string]$Year = '2020')

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$clientDir = Join-Path $root "Client$Year"
$setupDir = Join-Path $root 'api\public\setup'
$launcherExe = Join-Path $PSScriptRoot 'bin\RocketLauncher.exe'
$clientExe = Join-Path $clientDir 'RocketPlayerBeta.exe'

if (-not (Test-Path -LiteralPath $clientDir)) { throw "Can't find $clientDir" }

$clientZip = Join-Path $clientDir 'RocketPlayerBeta.zip'
if (-not (Test-Path -LiteralPath $clientExe) -and (Test-Path -LiteralPath $clientZip)) {
    Write-Host "Unpacking RocketPlayerBeta.exe..."
    Expand-Archive -LiteralPath $clientZip -DestinationPath $clientDir -Force
}
if (-not (Test-Path -LiteralPath $clientExe)) { throw "Can't find $clientExe" }

New-Item -ItemType Directory -Force -Path $setupDir | Out-Null

$skipNames = @('OZPlayerBeta.exe', 'RocketPlayerBeta.zip', '.DS_Store', 'Thumbs.db', 'desktop.ini')
$skipExtensions = @('.ps1', '.bat', '.py', '.oz', '.bak')

$zipPath = Join-Path $setupDir "Client$Year.zip"
$tmpZip = "$zipPath.tmp"
if (Test-Path -LiteralPath $tmpZip) { Remove-Item -LiteralPath $tmpZip -Force }

Write-Host "Zipping $clientDir ..."
$prefix = $clientDir.TrimEnd('\') + '\'
$count = 0
$zip = [System.IO.Compression.ZipFile]::Open($tmpZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $clientDir -Recurse -File -Force) {
        if ($skipNames -contains $file.Name) { continue }
        if ($skipExtensions -contains $file.Extension.ToLowerInvariant()) { continue }
        $entryName = $file.FullName.Substring($prefix.Length).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        $count++
    }
}
finally {
    $zip.Dispose()
}
Move-Item -LiteralPath $tmpZip -Destination $zipPath -Force

$version = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')
Set-Content -LiteralPath (Join-Path $setupDir "Client$Year.version") -Value $version -NoNewline -Encoding ASCII

if (Test-Path -LiteralPath $launcherExe) {
    Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $setupDir 'RocketLauncher.exe') -Force
} else {
    Write-Warning "Launcher\bin\RocketLauncher.exe not found - run Launcher\build.bat first"
}

$sizeMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Done: Client$Year.zip ($count files, $sizeMb MB), version $version"
Write-Host "Players get it from https://rockrev.de/setup/ the next time they press Play."
