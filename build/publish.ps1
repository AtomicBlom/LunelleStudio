<#
.SYNOPSIS
  Publishes the desktop studio for one runtime, signs it when given a certificate, and builds its installer on Windows.

.DESCRIPTION
  The studio is published self-contained and ReadyToRun (BinaryVibrance.LunelleStudio.App.csproj) to
  artifacts/publish/<rid>. On Windows, Inno Setup's iscc (on the PATH, or where it installs) then makes
  artifacts/installer/LunelleStudio-<version>-win-<arch>.exe. Elsewhere the published folder is zipped.

  Signing, when -CertificatePath (a .pfx) is given, or the SIGNING_CERTIFICATE (base 64) and
  SIGNING_CERTIFICATE_PASSWORD environment variables are set: the studio's own executable and assemblies, then the
  installer, with signtool from the Windows SDK and a timestamp. Unsigned otherwise, which Windows SmartScreen warns of.

  The engine, the audio, the synth and Vysara come from checkouts beside this repository, or their packages
  (build/Sources.props). The fonts are baked only from a Vysara checkout.

.EXAMPLE
  ./build/publish.ps1 -Runtime win-x64
#>
param(
	[string] $Runtime = "win-x64",
	[string] $Configuration = "Release",
	[string] $Version,
	[string] $CertificatePath,
	[string] $CertificatePassword
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "BinaryVibrance.LunelleStudio.App/BinaryVibrance.LunelleStudio.App.csproj"
$publish = Join-Path $root "artifacts/publish/$Runtime"
$installer = Join-Path $root "artifacts/installer"

if (-not $Version) {
	$Version = (dotnet msbuild $project -getProperty:Version).Trim()
}
Write-Host "Publishing Lunelle Studio $Version for $Runtime"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
# One node: the engine's projects are reached by more than one path, and two nodes building one at once collide
dotnet publish $project -c $Configuration -r $Runtime -o $publish -m:1 -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# The certificate, from a file or from the environment (as CI gives it)
$certificate = $CertificatePath
$password = $CertificatePassword
if (-not $certificate -and $env:SIGNING_CERTIFICATE) {
	$certificate = Join-Path ([IO.Path]::GetTempPath()) "lunelle-studio-signing.pfx"
	[IO.File]::WriteAllBytes($certificate, [Convert]::FromBase64String($env:SIGNING_CERTIFICATE))
	$password = $env:SIGNING_CERTIFICATE_PASSWORD
}

function Find-Tool([string] $name, [string[]] $places) {
	$found = Get-Command $name -ErrorAction SilentlyContinue
	if ($found) { return $found.Source }
	foreach ($place in $places) {
		$match = Get-ChildItem $place -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
		if ($match) { return $match.FullName }
	}
	return $null
}

function Sign([string[]] $files) {
	if (-not $certificate) { return }
	$signtool = Find-Tool "signtool.exe" @("${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe")
	if (-not $signtool) { throw "signtool.exe wasn't found: install the Windows SDK" }
	& $signtool sign /f $certificate /p $password /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $files
	if ($LASTEXITCODE -ne 0) { throw "signing failed" }
}

if ($Runtime.StartsWith("win-")) {
	# The studio's own files; the libraries it carries are their makers' to sign
	Sign (@((Join-Path $publish "lunelle-studio.exe")) + @(Get-ChildItem $publish -Filter "BinaryVibrance.*.dll" | ForEach-Object FullName))

	$iscc = Find-Tool "iscc.exe" @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
	if (-not $iscc) {
		Write-Warning "Inno Setup's iscc wasn't found: the studio is published to $publish, without an installer"
		return
	}
	$arch = $Runtime.Substring(4)
	New-Item $installer -ItemType Directory -Force | Out-Null
	& $iscc "/DAppVersion=$Version" "/DSource=$publish" "/DArch=$arch" "/O$installer" (Join-Path $root "installer/lunelle-studio.iss")
	if ($LASTEXITCODE -ne 0) { throw "the installer couldn't be built" }
	Sign @(Get-ChildItem $installer -Filter "LunelleStudio-$Version-*.exe" | ForEach-Object FullName)
}
else {
	New-Item $installer -ItemType Directory -Force | Out-Null
	$zip = Join-Path $installer "LunelleStudio-$Version-$Runtime.zip"
	if (Test-Path $zip) { Remove-Item $zip }
	Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip
}
Write-Host "Done: $installer"
