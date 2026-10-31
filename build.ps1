# Builds DeepTip EpicLoot and packs it for Thunderstore and r2modman ("Import local mod").
#
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-Out <folder>]
#
# Output: <Out>\Gorduan-DeepTip_EpicLoot\ (package folder) and <Out>\Gorduan-DeepTip_EpicLoot-<version>.zip
# Checks: the concept glossary with the DeepTip validator (tools\deeptip-check.mjs of https://github.com/gorduan/DeepTip,
# Node 18+; found via DEEPTIP_REPO or a DeepTip checkout next to this repository), the effect texts (both languages,
# same effects), the manifest description (max 250 chars), no BOM in manifest.json / README.md / CHANGELOG.md.
param([string]$Out = (Join-Path $PSScriptRoot 'build-output'))

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

$plugin = Get-Content (Join-Path $root 'src\Plugin.cs') -Raw
$version = [regex]::Match($plugin, 'const string Version = "([^"]+)"').Groups[1].Value
if (-not $version) { Write-Host 'Version not found in src\Plugin.cs' -ForegroundColor Red; exit 1 }

$texts = @{}
foreach ($locale in 'de', 'en') {
    $file = Join-Path $root "data\effekte.$locale.json"
    if (-not (Test-Path $file)) { Write-Host "missing: data\effekte.$locale.json" -ForegroundColor Red; exit 1 }
    $texts[$locale] = @((Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json).PSObject.Properties.Name)
}
$onlyDe = @($texts['de'] | Where-Object { $texts['en'] -notcontains $_ })
$onlyEn = @($texts['en'] | Where-Object { $texts['de'] -notcontains $_ })
if ($onlyDe.Count -or $onlyEn.Count) { Write-Host "Effect texts differ: only de: $($onlyDe -join ', '); only en: $($onlyEn -join ', ')" -ForegroundColor Red; exit 1 }
Write-Host "Effect texts: $($texts['de'].Count) effects in de and en"

dotnet build (Join-Path $root 'DeepTip_EpicLoot.csproj') -c Release -nologo -v:q | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed' -ForegroundColor Red; exit 1 }

$glossary = @(Get-ChildItem (Join-Path $root 'glossary') -Filter '*.json' -ErrorAction SilentlyContinue)
$candidates = @()
if ($env:DEEPTIP_REPO) { $candidates += Join-Path $env:DEEPTIP_REPO 'tools\deeptip-check.mjs' }
$candidates += @((Join-Path $root '..\DeepTip\tools\deeptip-check.mjs'), (Join-Path $root '..\..\..\..\DeepTip\tools\deeptip-check.mjs'))
$check = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $check) { Write-Host 'DeepTip validator not found (set DEEPTIP_REPO): glossary not checked' -ForegroundColor Yellow }
if ($glossary.Count -gt 0 -and $check -and (Get-Command node -ErrorAction SilentlyContinue)) {
    node $check --base de @($glossary | ForEach-Object { $_.FullName }) | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-Host 'Glossary check failed' -ForegroundColor Red; exit 1 }
}

$description = 'Explains every EpicLoot property in DeepTip windows: what each magic effect really does (checked against the code), value ranges per rarity, where it can appear, rarity, stars, shards, runes, sets, the enchanting table. EN/DE.'
if ($description.Length -gt 250) { Write-Host "Description has $($description.Length) characters, max 250" -ForegroundColor Red; exit 1 }

$folder = Join-Path $Out 'Gorduan-DeepTip_EpicLoot'
if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
New-Item -ItemType Directory -Force -Path $folder | Out-Null
Copy-Item (Join-Path $root 'bin\DeepTip_EpicLoot.dll') $folder
foreach ($file in 'README.md', 'CHANGELOG.md', 'LICENSE', 'icon.png') {
    $source = Join-Path $root $file
    if (-not (Test-Path $source)) { Write-Host "missing: $file" -ForegroundColor Red; exit 1 }
    if ($file -like '*.md') {
        $text = [IO.File]::ReadAllText($source)
        [IO.File]::WriteAllText((Join-Path $folder $file), $text.TrimStart([char]0xFEFF), $utf8)
    } else {
        Copy-Item $source $folder
    }
}
$manifest = [ordered]@{
    name = 'DeepTip_EpicLoot'
    version_number = $version
    website_url = 'https://github.com/gorduan/Valheim_DeepTip_EpicLoot'
    description = $description
    dependencies = @('denikson-BepInExPack_Valheim-5.4.2350', 'Gorduan-DeepTip-1.0.0', 'RandyKnapp-EpicLoot-0.15.4')
}
[IO.File]::WriteAllText((Join-Path $folder 'manifest.json'), ($manifest | ConvertTo-Json -Depth 4), $utf8)

$zip = Join-Path $Out "Gorduan-DeepTip_EpicLoot-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($file in Get-ChildItem $folder -File -Recurse | Sort-Object FullName) {
        $entry = $file.FullName.Substring($folder.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $archive.Dispose() }
Write-Host "DeepTip EpicLoot $version -> $zip"
