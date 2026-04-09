# api-user

POC em .NET 8 para observabilidade com OpenTelemetry.

## Estrutura de observabilidade

A aplicacao suporta dois modos de execucao:

- modo manual: a instrumentacao e registrada no codigo em [OpenTelemetryConfig.cs](src/Telemetry/OpenTelemetryConfig.cs)
- modo auto-instrumentado: a instrumentacao automatica e ativada no bootstrap do processo por [run-otel-auto.ps1](scripts/run-otel-auto.ps1)

No modo auto-instrumentado, a aplicacao detecta a presenca do profiler/startup hook e nao registra a pipeline manual para evitar spans, metricas e logs duplicados.

## Pre-requisitos

- .NET 8 SDK
- Docker Desktop ou Docker Engine
- PowerShell
- um endpoint OTLP disponivel em `http://localhost:4317` ou outro endpoint compativel

## Banco de dados

Para subir o SQL Server localmente com Docker Compose:

```powershell
docker compose up -d sqlserver
```

O banco fica exposto em `localhost,1433`.

Connection string local:

```text
Server=localhost,1433;Database=UserDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;
```

Arquivo relacionado:

- [docker-compose.yml](docker-compose.yml)

## Rodando em modo manual

Esse modo usa a configuracao em codigo e e o fluxo normal de `dotnet run`.

1. Suba o SQL Server:

```powershell
docker compose up -d sqlserver
```

2. Exporte o endpoint OTLP no shell atual:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"
```

3. Rode a aplicacao:

```powershell
dotnet run
```

Nesse modo, a API usa:

- traces via `AddAspNetCoreInstrumentation` e `AddHttpClientInstrumentation`
- metrics via `AddAspNetCoreInstrumentation` e `AddRuntimeInstrumentation`
- logs via `builder.Logging.AddOpenTelemetry(...)`
- spans customizados como `ApiUser.EntityFramework`

## Rodando em modo auto-instrumentado

Esse modo usa `OpenTelemetry.AutoInstrumentation` e precisa iniciar o processo com `instrument.cmd`.

### Por que o bootstrap e necessario no .NET

Em aplicacoes .NET, a auto-instrumentacao do OpenTelemetry nao funciona apenas adicionando um pacote e chamando metodos no `Program.cs`.

Ela precisa atuar antes do codigo da aplicacao comecar a executar, porque o objetivo e:

- injetar o profiler do .NET no processo
- registrar `startup hooks` antes da inicializacao da aplicacao
- habilitar instrumentacoes automaticas para bibliotecas como ASP.NET Core, `HttpClient`, `SqlClient` e outras sem depender de configuracao manual no codigo

Esse comportamento acontece no bootstrap do processo, nao dentro do pipeline normal da aplicacao.

Por isso o pacote disponibiliza o `instrument.cmd` e configura variaveis como:

- `CORECLR_ENABLE_PROFILING`
- `CORECLR_PROFILER`
- `DOTNET_STARTUP_HOOKS`
- `OTEL_DOTNET_AUTO_HOME`

Sem esse bootstrap, o runtime sobe a aplicacao normalmente, mas a auto-instrumentacao nao consegue anexar o profiler nem registrar os hooks no momento correto.

Na pratica, isso significa:

- `dotnet run`: executa a API com a configuracao manual registrada em codigo
- `instrument.cmd dotnet api-user.dll`: executa a API com o bootstrap necessario para a auto-instrumentacao

Neste projeto, o bootstrap foi encapsulado em [run-otel-auto.ps1](scripts/run-otel-auto.ps1) para padronizar a execucao e evitar configurar essas variaveis manualmente toda vez.

### Diagrama de inicializacao

```text
Modo manual

dotnet run
   |
   v
.NET sobe a aplicacao
   |
   v
Program.cs / OpenTelemetryConfig.cs
   |
   v
Pipeline manual de logs, traces e metrics
   |
   v
Exportacao OTLP


Modo auto-instrumentado

run-otel-auto.ps1
   |
   v
instrument.cmd
   |
   v
Define variaveis de bootstrap
(CORECLR_ENABLE_PROFILING, CORECLR_PROFILER,
 DOTNET_STARTUP_HOOKS, OTEL_DOTNET_AUTO_HOME)
   |
   v
.NET sobe com profiler + startup hook
   |
   v
OpenTelemetry AutoInstrumentation intercepta bibliotecas suportadas
   |
   v
Aplicacao inicia
   |
   v
Program.cs detecta auto-instrumentation ativa
   |
   v
Pipeline manual nao e registrada
   |
   v
Exportacao OTLP
```

1. Suba o SQL Server:

```powershell
docker compose up -d sqlserver
```

2. Execute o script:

```powershell
.\scripts\run-otel-auto.ps1
```

Exemplo com endpoint OTLP customizado:

```powershell
.\scripts\run-otel-auto.ps1 -OtlpEndpoint http://localhost:4317
```

O script:

- compila o projeto com `RuntimeIdentifier=win-x64`
- usa o `instrument.cmd` gerado pelo pacote `OpenTelemetry.AutoInstrumentation`
- define as variaveis `OTEL_*` necessarias para traces, metrics e logs
- inclui os sources customizados em `OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES`

Variaveis relevantes definidas pelo script:

- `OTEL_SERVICE_NAME=users-api`
- `OTEL_EXPORTER_OTLP_ENDPOINT`
- `OTEL_EXPORTER_OTLP_PROTOCOL=grpc`
- `OTEL_TRACES_EXPORTER=otlp`
- `OTEL_METRICS_EXPORTER=otlp`
- `OTEL_LOGS_EXPORTER=otlp`
- `OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES=ApiUser.UsersController,ApiUser.UserService,ApiUser.EntityFramework`
- `OTEL_DOTNET_AUTO_METRICS_ADDITIONAL_SOURCES=ApiUser.Metrics`

## Diferenca entre os modos

- `dotnet run`: usa somente a instrumentacao registrada no codigo
- `.\scripts\run-otel-auto.ps1`: usa auto-instrumentation no processo e desabilita a pipeline manual da aplicacao

Se voce executar apenas `dotnet run`, a auto-instrumentation nao sera ativada.

## Observacoes

- O pacote `OpenTelemetry.AutoInstrumentation` exigiu alinhar as dependencias OpenTelemetry para `1.15.0`
- O script de auto-instrumentation atual foi preparado para Windows
- O banco e criado automaticamente pela aplicacao com `context.Database.EnsureCreated()`
