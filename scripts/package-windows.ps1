#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'AvatarPackBuilder.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$artifactRoot = Join-Path $root 'artifacts'
$packageName = "AvatarPackBuilder-v$version-win-x64"
$publish = Join-Path $artifactRoot $packageName
if (Test-Path -LiteralPath $publish) { throw "Output already exists: $publish. Move it aside before packaging again." }
dotnet restore $project --locked-mode -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -o $publish -p:DebugType=none -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if (Get-ChildItem -LiteralPath $publish -Recurse -Filter *.pdb) { throw 'Debug symbols must not be distributed.' }
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD_PARTY_NOTICES.md'), (Join-Path $root 'README.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $root 'legal') -Destination $publish -Recurse
$archive = Join-Path $artifactRoot "$packageName.zip"
if (Test-Path -LiteralPath $archive) { throw "Archive already exists: $archive" }
Compress-Archive -LiteralPath $publish -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Value "$hash  $packageName.zip" -Encoding utf8NoBOM
Write-Output "Created $archive"
