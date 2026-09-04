param(
  [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
  $OutputDirectory = Join-Path $artifactRoot "win-x64"
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$artifactPrefix = $artifactRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($artifactPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
  throw "The publish output must stay below $artifactRoot."
}

if (Test-Path -LiteralPath $resolvedOutput) {
  Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedOutput | Out-Null

$project = Join-Path $projectRoot "ReuploadMapToNadeo.csproj"
$pathMap = "$projectRoot=/src/script-tm_Reupload-Map-To-Nadeo"
dotnet publish $project `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishAot=true `
  -p:StripSymbols=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -p:Deterministic=true `
  -p:ContinuousIntegrationBuild=true `
  "-p:PathMap=$pathMap" `
  --output $resolvedOutput
if ($LASTEXITCODE -ne 0) {
  throw "NativeAOT publish failed."
}

$requiredFiles = @("ReuploadMapToNadeo.exe", "liblzo2.dll")
foreach ($fileName in $requiredFiles) {
  $filePath = Join-Path $resolvedOutput $fileName
  if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
    throw "Publish did not produce $fileName."
  }
  $file = Get-Item -LiteralPath $filePath
  $hash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
  Write-Output "$fileName | $($file.Length) bytes | sha256:$hash"
}
