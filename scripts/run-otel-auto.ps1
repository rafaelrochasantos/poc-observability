param(
    [string]$Configuration = "Debug",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OtlpEndpoint = "http://localhost:4317",
    [string]$ServiceName = "users-api",
    [string]$ServiceVersion = "1.0.0",
    [string]$EnvironmentName = "Development"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "api-user.csproj"

dotnet build $projectFile -c $Configuration -r $RuntimeIdentifier | Out-Host

$outputPath = Join-Path $projectRoot "bin\$Configuration\net8.0\$RuntimeIdentifier"
$instrumentScript = Join-Path $outputPath "instrument.cmd"
$applicationDll = Join-Path $outputPath "api-user.dll"

if (-not (Test-Path $instrumentScript)) {
    throw "instrument.cmd nao foi encontrado em '$instrumentScript'. O pacote OpenTelemetry.AutoInstrumentation nao foi copiado para a pasta de output."
}

if (-not (Test-Path $applicationDll)) {
    throw "api-user.dll nao foi encontrado em '$applicationDll'."
}

$env:ASPNETCORE_ENVIRONMENT = $EnvironmentName
$env:OTEL_SERVICE_NAME = $ServiceName
$env:OTEL_EXPORTER_OTLP_ENDPOINT = $OtlpEndpoint
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
$env:OTEL_TRACES_EXPORTER = "otlp"
$env:OTEL_METRICS_EXPORTER = "otlp"
$env:OTEL_LOGS_EXPORTER = "otlp"
$env:OTEL_RESOURCE_ATTRIBUTES = "service.version=$ServiceVersion,deployment.environment=$EnvironmentName"
$env:OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES = "ApiUser.UsersController,ApiUser.UserService,ApiUser.EntityFramework"
$env:OTEL_DOTNET_AUTO_METRICS_ADDITIONAL_SOURCES = "ApiUser.Metrics"

Write-Host "Executando a API com OpenTelemetry auto-instrumentation..." -ForegroundColor Cyan
Write-Host "Output: $outputPath"
Write-Host "OTLP Endpoint: $OtlpEndpoint"

Push-Location $outputPath
try {
    & $instrumentScript dotnet $applicationDll
}
finally {
    Pop-Location
}
