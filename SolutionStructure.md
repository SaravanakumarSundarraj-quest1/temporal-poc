# OmsTemporal Solution Structure

Complete guide to the Order Management System solution architecture, project organization, and dependency management.

---

## Solution Overview

**Solution File**: `OmsTemporal.sln`

**Target Framework**: .NET 8

**Architecture Pattern**: Domain-Driven Design (DDD) with layered architecture

**Build Configuration**: Debug | Release (x64)

---

## Project Organization

### Solution Layers

```
OmsTemporal.sln
├── Domain Layer
│   └── Oms.Domain (Class Library)
├── Application Layer
│   └── Oms.Application (Class Library)
├── Infrastructure Layer
│   ├── Oms.Infrastructure (Class Library)
│   ├── Oms.Temporal (Class Library)
│   └── Oms.Contracts (Class Library)
├── Presentation Layer
│   └── Oms.Api (ASP.NET Core Web API)
├── Worker Layer
│   └── Oms.Worker (Worker Service / Console App)
├── Shared Layer
│   └── Oms.Shared (Class Library)
└── Test Layer
    ├── Oms.Tests (Unit/Integration Tests)
    └── Oms.ReplayTests (Replay Safety Tests)
```

---

## Folder Structure

### Root Level

```
OmsTemporal/
├── OmsTemporal.sln
├── Architecture.md                 # Architecture blueprint
├── SolutionStructure.md            # This file
├── ProjectSetup.md                 # Setup instructions
├── solution-manifest.json          # Machine-readable config
├── Directory.Build.props            # Shared build properties
├── Directory.Packages.props         # Centralized package versions
├── .github/
│   └── workflows/
│       ├── build.yml
│       ├── test.yml
│       └── deploy.yml
└── [Project Folders - see below]
```

### Oms.Domain

**Purpose**: Core business entities and domain logic

```
Oms.Domain/
├── Oms.Domain.csproj
├── Aggregates/
│   ├── Order/
│   │   ├── Order.cs                    # Aggregate root
│   │   ├── OrderStatus.cs              # Value object
│   │   ├── IOrderRepository.cs         # Repository interface
│   │   └── OrderEventHandler.cs
│   ├── Customer/
│   │   ├── Customer.cs
│   │   ├── Address.cs
│   │   └── CustomerSegment.cs
│   └── Payment/
│       ├── Payment.cs
│       ├── PaymentStatus.cs
│       ├── PaymentTransaction.cs
│       └── IPaymentRepository.cs
├── ValueObjects/
│   ├── RiskData.cs
│   ├── RiskLevel.cs
│   ├── RiskIndicator.cs
│   ├── Money.cs
│   ├── ProductCode.cs
│   └── OrderNumber.cs
├── DomainEvents/
│   ├── IDomainEvent.cs
│   ├── OrderCreatedEvent.cs
│   ├── OrderStatusChangedEvent.cs
│   ├── PaymentAuthorizedEvent.cs
│   └── RiskAssessmentCompletedEvent.cs
├── Exceptions/
│   ├── DomainException.cs
│   ├── InvalidOrderStateException.cs
│   ├── PaymentRejectedException.cs
│   └── RiskRejectedException.cs
└── Enums/
    ├── OrderStatus.cs
    ├── PaymentStatus.cs
    ├── RiskLevel.cs
    └── CustomerSegment.cs
```

### Oms.Application

**Purpose**: Application services and business use cases

```
Oms.Application/
├── Oms.Application.csproj
├── Services/
│   ├── OrderService.cs              # Main order use case service
│   ├── OrderQueryService.cs         # Query-side service
│   ├── WorkflowOrchestrator.cs      # Workflow coordination
│   └── NotificationService.cs
├── DTOs/
│   ├── OrderDto.cs
│   ├── OrderItemDto.cs
│   ├── CreateOrderRequest.cs
│   ├── CustomerDto.cs
│   ├── PaymentDto.cs
│   └── EnrichedOrderDto.cs
├── Mappers/
│   ├── OrderMapper.cs               # Domain ↔ DTO mapping
│   ├── PaymentMapper.cs
│   └── AutoMapperProfile.cs
├── CompensationStrategies/
│   ├── ICompensationStrategy.cs
│   ├── RefundCompensation.cs
│   ├── CancellationCompensation.cs
│   └── CompensationOrchestrator.cs
├── Exceptions/
│   ├── ApplicationException.cs
│   ├── OrderNotFoundException.cs
│   └── WorkflowException.cs
└── Validators/
    ├── CreateOrderRequestValidator.cs
    └── OrderRequestValidator.cs
```

### Oms.Infrastructure

**Purpose**: External system integration and cross-cutting concerns

```
Oms.Infrastructure/
├── Oms.Infrastructure.csproj
├── ExternalServices/
│   ├── PaymentGateway/
│   │   ├── IPaymentGatewayClient.cs
│   │   ├── StripePaymentClient.cs
│   │   ├── PaymentGatewayResponse.cs
│   │   └── PaymentGatewayConfig.cs
│   ├── RiskEngine/
│   │   ├── IRiskEngineClient.cs
│   │   ├── RiskEngineHttpClient.cs
│   │   ├── RiskAssessmentResponse.cs
│   │   └── RiskEngineConfig.cs
│   └── PIM/
│       ├── IPimClient.cs
│       ├── PimHttpClient.cs
│       ├── ProductEnrichmentResponse.cs
│       └── PimConfig.cs
├── Messaging/
│   ├── Kafka/
│   │   ├── IKafkaProducer.cs
│   │   ├── KafkaProducerImpl.cs
│   │   ├── FulfillmentEventPublisher.cs
│   │   └── KafkaConfig.cs
│   └── Events/
│       ├── IEventBus.cs
│       └── EventBusImpl.cs
├── Persistence/
│   ├── Database/
│   │   ├── OrderDbContext.cs
│   │   ├── OrderRepository.cs
│   │   ├── PaymentRepository.cs
│   │   └── Migrations/
│   │       └── [EF Core migrations]
│   └── Cache/
│       ├── ICacheService.cs
│       └── RedisCacheService.cs
├── HttpClients/
│   ├── HttpClientFactory.cs
│   ├── RetryPolicies.cs
│   ├── CircuitBreakerPolicies.cs
│   └── HttpClientBuilderExtensions.cs
├── Security/
│   ├── VaultProvider.cs             # Secrets management
│   ├── IEncryptionService.cs
│   └── AesEncryptionService.cs
└── Configuration/
    ├── ConfigurationProvider.cs
    ├── FeatureFlagProvider.cs
    └── appsettings.*.json
```

### Oms.Temporal

**Purpose**: Temporal-specific implementations

```
Oms.Temporal/
├── Oms.Temporal.csproj
├── Workflows/
│   ├── OrderProcessingWorkflow.cs   # Main workflow definition
│   ├── WorkflowOptions.cs
│   └── WorkflowSignalHandlers.cs
├── Activities/
│   ├── IActivityBase.cs             # Base interface
│   ├── ValidateCommerceActivity.cs
│   ├── CollectRiskActivity.cs
│   ├── ValidateRiskActivity.cs
│   ├── ValidatePaymentActivity.cs
│   ├── EnrichOrderActivity.cs
│   ├── UpdateDashboardActivity.cs
│   ├── PublishFulfillmentActivity.cs
│   ├── RefundPaymentActivity.cs
│   └── CompensatingActivities/
│       ├── RollbackPaymentActivity.cs
│       └── CancellationHandlerActivity.cs
├── Options/
│   ├── ActivityOptions.cs           # Retry, timeout config
│   ├── WorkflowOptions.cs
│   ├── RetryPolicy.cs
│   └── TimeoutPolicy.cs
├── Contracts/
│   ├── ActivityInputs/
│   │   ├── ValidateCommerceInput.cs
│   │   ├── CollectRiskInput.cs
│   │   └── [Other activity inputs]
│   ├── ActivityOutputs/
│   │   ├── ValidateCommerceOutput.cs
│   │   ├── CollectRiskOutput.cs
│   │   └── [Other activity outputs]
│   └── WorkflowSignals/
│       ├── CancelOrderSignal.cs
│       ├── RequestCorrectionSignal.cs
│       └── ApproveRiskSignal.cs
├── Clients/
│   ├── ITemporalClient.cs
│   ├── TemporalClientImpl.cs
│   ├── WorkflowClientFactory.cs
│   └── TemporalConnectionOptions.cs
├── Codecs/
│   ├── IPayloadCodec.cs
│   ├── AesGcmPayloadCodec.cs        # AES-256-GCM encryption
│   ├── PayloadCodecOptions.cs
│   └── KeyManagement/
│       ├── IKeyProvider.cs
│       └── VaultKeyProvider.cs
├── SearchAttributes/
│   ├── SearchAttributeMapper.cs
│   ├── OrderSearchAttributes.cs
│   └── SearchAttributeConstants.cs
├── Configuration/
│   ├── TemporalServerOptions.cs
│   ├── TemporalNamespaceOptions.cs
│   └── TemporalWorkerOptions.cs
└── Instrumentation/
    ├── TemporalMetricsCollector.cs
    ├── TemporalTracingContext.cs
    └── TemporalInterceptors.cs
```

### Oms.Contracts

**Purpose**: Serializable data contracts

```
Oms.Contracts/
├── Oms.Contracts.csproj
├── ActivityInputOutputs/
│   ├── ValidateCommerceActivity.cs
│   ├── CollectRiskActivity.cs
│   ├── ValidatePaymentActivity.cs
│   ├── EnrichOrderActivity.cs
│   └── [Other contracts]
├── WorkflowSignals/
│   ├── CancelOrderRequest.cs
│   ├── RequestCorrectionRequest.cs
│   └── ApproveRiskRequest.cs
├── WorkflowQueries/
│   ├── GetOrderStatusQuery.cs
│   ├── GetOrderDetailsQuery.cs
│   └── GetPaymentStatusQuery.cs
├── ExternalServices/
│   ├── PaymentGateway/
│   │   ├── PaymentAuthorizationRequest.cs
│   │   ├── PaymentAuthorizationResponse.cs
│   │   └── RefundRequest.cs
│   ├── RiskEngine/
│   │   ├── RiskAssessmentRequest.cs
│   │   └── RiskAssessmentResponse.cs
│   └── PIM/
│       ├── ProductEnrichmentRequest.cs
│       └── ProductEnrichmentResponse.cs
├── Kafka/
│   ├── FulfillmentOrderEvent.cs
│   ├── OrderStatusChangedEvent.cs
│   └── PaymentProcessedEvent.cs
└── Shared/
    ├── Address.cs
    ├── Money.cs
    ├── ApiErrorResponse.cs
    └── ApiSuccessResponse.cs
```

### Oms.Api

**Purpose**: REST API entry point

```
Oms.Api/
├── Oms.Api.csproj
├── Program.cs                        # Application entry point
├── Controllers/
│   ├── OrderController.cs            # Order endpoints
│   ├── OrderQueryController.cs       # Query endpoints
│   ├── OrderWebhookController.cs     # Webhook ingestion
│   ├── HealthController.cs
│   └── StatusController.cs
├── Middleware/
│   ├── ErrorHandlingMiddleware.cs
│   ├── TracingMiddleware.cs
│   ├── LoggingMiddleware.cs
│   └── AuthenticationMiddleware.cs
├── Filters/
│   ├── ValidateModelFilter.cs
│   └── ExceptionFilter.cs
├── Extensions/
│   ├── ControllerExtensions.cs
│   ├── DependencyInjectionExtensions.cs
│   └── OpenApiExtensions.cs
├── Responses/
│   ├── CreateOrderResponse.cs
│   ├── OrderStatusResponse.cs
│   └── ErrorResponse.cs
├── Startup/
│   ├── ServiceCollectionExtensions.cs # DI setup
│   ├── ApplicationBuilderExtensions.cs # Middleware
│   └── OpenApiConfiguration.cs
├── Requests/
│   ├── CreateOrderRequest.cs
│   ├── CancelOrderRequest.cs
│   └── RequestCorrectionRequest.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
└── appsettings.Staging.json
```

### Oms.Worker

**Purpose**: Background worker for activity execution

```
Oms.Worker/
├── Oms.Worker.csproj
├── Program.cs                        # Worker entry point
├── HostedServices/
│   ├── TemporalWorkerHostedService.cs
│   ├── HealthCheckHostedService.cs
│   └── MetricsCollectorHostedService.cs
├── Configuration/
│   ├── WorkerOptions.cs
│   ├── TaskQueueConfiguration.cs
│   └── WorkerStartupConfiguration.cs
├── TaskQueues/
│   ├── OmsQueueWorker.cs
│   ├── CommerceQueueWorker.cs
│   └── FulfillmentQueueWorker.cs
├── Handlers/
│   ├── ActivityErrorHandler.cs
│   ├── ActivityTimeoutHandler.cs
│   └── HeartbeatHandler.cs
├── Startup/
│   ├── ServiceCollectionExtensions.cs # DI setup
│   └── WorerBuilderExtensions.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Production.json
```

### Oms.Shared

**Purpose**: Cross-cutting concerns

```
Oms.Shared/
├── Oms.Shared.csproj
├── Logging/
│   ├── ILogger.cs
│   ├── LoggerFactory.cs
│   └── OpenTelemetryLogging.cs
├── Tracing/
│   ├── ITracingContext.cs
│   ├── TracingContextProvider.cs
│   └── TraceIdMiddleware.cs
├── Metrics/
│   ├── MetricsCollector.cs
│   ├── MetricsExporter.cs
│   └── MetricDefinitions.cs
├── Exceptions/
│   ├── BaseException.cs
│   ├── TemporalException.cs
│   ├── ExternalServiceException.cs
│   └── ConfigurationException.cs
├── Constants/
│   ├── OrderConstants.cs
│   ├── TemporalConstants.cs
│   ├── ExternalServiceConstants.cs
│   └── SearchAttributeNames.cs
├── Extensions/
│   ├── StringExtensions.cs
│   ├── DateTimeExtensions.cs
│   ├── EnumExtensions.cs
│   └── JsonSerializationExtensions.cs
├── Configuration/
│   ├── IConfigurationProvider.cs
│   ├── AppConfigurationProvider.cs
│   └── ConfigurationBuilder.cs
└── Utilities/
    ├── IdGenerator.cs
    ├── JsonSerializer.cs
    └── ValidationHelper.cs
```

### Oms.Tests

**Purpose**: Unit and integration tests

```
Oms.Tests/
├── Oms.Tests.csproj
├── Unit/
│   ├── Domain/
│   │   ├── OrderAggregateTests.cs
│   │   ├── PaymentAggregateTests.cs
│   │   └── RiskDataTests.cs
│   ├── Application/
│   │   ├── OrderServiceTests.cs
│   │   └── WorkflowOrchestratorTests.cs
│   └── Infrastructure/
│       ├── PaymentGatewayClientTests.cs
│       └── RiskEngineClientTests.cs
├── Integration/
│   ├── Api/
│   │   ├── OrderControllerTests.cs
│   │   └── WebhookControllerTests.cs
│   └── Temporal/
│       ├── OrderProcessingWorkflowTests.cs
│       ├── ActivityExecutionTests.cs
│       └── SignalHandlerTests.cs
├── Fixtures/
│   ├── TemporalTestFixture.cs
│   ├── OrderTestDataBuilder.cs
│   ├── PaymentTestDataBuilder.cs
│   └── RiskDataBuilder.cs
├── Mocks/
│   ├── MockPaymentGateway.cs
│   ├── MockRiskEngine.cs
│   └── MockPimClient.cs
├── TestBase.cs                       # Base test class
├── Helpers/
│   ├── AssertionHelper.cs
│   └── TestDataGenerator.cs
└── appsettings.test.json
```

### Oms.ReplayTests

**Purpose**: Replay safety verification

```
Oms.ReplayTests/
├── Oms.ReplayTests.csproj
├── EventHistories/
│   ├── v1-happy-path.json
│   ├── v1-payment-failure.json
│   ├── v1-cancellation.json
│   ├── v1-expiration.json
│   └── v1-risk-rejection.json
├── ReplayTestRunner.cs               # Main replay runner
├── HistoryCaptureRecorder.cs         # Record from staging
├── ReplayTestCases.cs
├── ReplayAssertions.cs
├── VersionCompatibilityTests.cs
└── ReplayTestConfiguration.cs
```

---

## NuGet Packages

### Core Framework Packages

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Microsoft.NET.Sdk** | 8.0 | All | .NET 8 SDK |
| **Microsoft.AspNetCore.App** | 8.0 | Api | ASP.NET Core runtime |
| **Microsoft.Extensions.DependencyInjection** | 8.0.0 | All | DI container |
| **Microsoft.Extensions.Configuration** | 8.0.0 | All, Api, Worker | Configuration management |
| **Microsoft.Extensions.Logging** | 8.0.0 | All | Logging abstraction |
| **Microsoft.Extensions.Hosting** | 8.0.0 | Api, Worker | Host builder |

### Temporal Packages

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Temporalio** | 1.2.0+ | Temporal, Api, Worker | Temporal .NET SDK |
| **Temporalio.Extensions.OpenTelemetry** | 1.2.0+ | Temporal, Shared | OpenTelemetry support |
| **Temporalio.Worker** | 1.2.0+ | Worker | Worker-specific features |

### OpenTelemetry & Observability

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **OpenTelemetry** | 1.9.0 | Shared | Core OTel SDK |
| **OpenTelemetry.Instrumentation.AspNetCore** | 1.9.0 | Api | ASP.NET Core instrumentation |
| **OpenTelemetry.Instrumentation.Http** | 1.9.0 | Infrastructure | HTTP client tracing |
| **OpenTelemetry.Exporter.Otlp** | 1.9.0 | Shared | gRPC OTLP exporter |
| **OpenTelemetry.Instrumentation.SqlClient** | 1.9.0 | Infrastructure | SQL tracing |
| **Serilog** | 3.1.0 | Shared | Structured logging |
| **Serilog.Extensions.Logging** | 8.0.0 | Shared | Serilog integration |
| **Serilog.Sinks.OpenTelemetry** | 1.0.0 | Shared | OTel sink |
| **Prometheus.Client** | 4.4.0 | Shared | Prometheus metrics |

### Data Access & Persistence

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Microsoft.EntityFrameworkCore** | 8.0.0 | Infrastructure | ORM framework |
| **Microsoft.EntityFrameworkCore.SqlServer** | 8.0.0 | Infrastructure | SQL Server provider |
| **Microsoft.EntityFrameworkCore.Design** | 8.0.0 | Infrastructure | EF Core tools |
| **Microsoft.EntityFrameworkCore.Migrations** | 8.0.0 | Infrastructure | EF Core migrations |
| **StackExchange.Redis** | 2.7.0 | Infrastructure | Redis client |

### External Integrations

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **Confluent.Kafka** | 2.3.0 | Infrastructure | Kafka producer |
| **Stripe.net** | 42.0.0 | Infrastructure | Stripe payment SDK |
| **Polly** | 8.2.0 | Infrastructure | Resilience policies |
| **Refit** | 7.0.0 | Infrastructure | HTTP client generation |
| **Newtonsoft.Json** | 13.0.3 | All | JSON serialization |
| **System.Text.Json** | 8.0.0 | All | Modern JSON |

### Encryption & Security

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **System.Security.Cryptography** | 4.3.1 | Shared, Temporal | Cryptography APIs |
| **Azure.Identity** | 1.11.0 | Infrastructure | Azure authentication |
| **Azure.Security.KeyVault.Secrets** | 4.5.0 | Infrastructure | Azure Key Vault |

### Validation & Mapping

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **FluentValidation** | 11.8.0 | Application, Api | Validation framework |
| **AutoMapper** | 13.0.0 | Application | Object mapping |
| **AutoMapper.Extensions.Microsoft.DependencyInjection** | 12.0.0 | Application | AutoMapper DI |

### Testing Packages

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **xUnit** | 2.6.0 | Tests, ReplayTests | Test framework |
| **Moq** | 4.20.0 | Tests | Mocking library |
| **FluentAssertions** | 6.12.0 | Tests | Assertion library |
| **Microsoft.AspNetCore.Mvc.Testing** | 8.0.0 | Tests | API testing |
| **Temporalio.Testing** | 1.2.0+ | Tests, ReplayTests | Temporal test framework |

### Utilities

| Package | Version | Projects | Purpose |
|---------|---------|----------|---------|
| **GuardClauses** | 4.2.0 | All | Guard patterns |
| **MediatR** | 12.1.1 | Application | CQRS/Mediator |
| **ValueOf** | 1.0.24 | Domain | Value object builder |

---

## Dependency Injection Setup

### Oms.Api Startup Configuration

**File**: `Oms.Api/Startup/ServiceCollectionExtensions.cs`

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Temporal Client
        services.AddSingleton(sp =>
        {
            var options = new TemporalClientConnectOptions
            {
                TargetHost = configuration["Temporal:Host"] ?? "localhost",
                TargetPort = int.Parse(configuration["Temporal:Port"] ?? "7233"),
                Namespace = configuration["Temporal:Namespace"] ?? "default"
            };
            return new TemporalClient(options);
        });

        // Domain Services
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // Application Services
        services.AddScoped<OrderService>();
        services.AddScoped<OrderQueryService>();
        services.AddScoped<WorkflowOrchestrator>();

        // Infrastructure Services
        services.AddHttpClients(configuration);
        services.AddKafkaProducer(configuration);
        services.AddDatabaseContext(configuration);
        services.AddCaching(configuration);

        // AutoMapper
        services.AddAutoMapper(typeof(AutoMapperProfile));

        // Validation
        services.AddValidation();

        // Observability
        services.AddOpenTelemetry(configuration);
        services.AddLogging(configuration);

        return services;
    }

    private static IServiceCollection AddHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddRefitClient<IPaymentGatewayClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(configuration["ExternalServices:PaymentGateway:BaseUrl"]);
                c.DefaultRequestHeaders.Add("Authorization", 
                    $"Bearer {configuration["ExternalServices:PaymentGateway:ApiKey"]}");
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services
            .AddRefitClient<IRiskEngineClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(configuration["ExternalServices:RiskEngine:BaseUrl"]);
            })
            .AddPolicyHandler(GetRetryPolicy());

        services
            .AddRefitClient<IPimClient>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(configuration["ExternalServices:PIM:BaseUrl"]);
            })
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IServiceCollection AddKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IKafkaProducer>(sp =>
        {
            var kafkaConfig = configuration.GetSection("Kafka");
            return new KafkaProducerImpl(
                kafkaConfig["BootstrapServers"],
                kafkaConfig["Topic"]);
        });

        return services;
    }

    private static IServiceCollection AddDatabaseContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        return services;
    }

    private static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });

        services.AddScoped<ICacheService, RedisCacheService>();
        return services;
    }

    private static IServiceCollection AddValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
        return services;
    }

    private static IServiceCollection AddOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otelBuilder = services
            .AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddTemporalInstrumentation()
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(
                            configuration["Observability:OtlpExporter:Endpoint"] 
                            ?? "http://otel-collector:4317");
                    });
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddPrometheusExporter();
            });

        return services;
    }

    private static IServiceCollection AddLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddLogging(builder =>
        {
            builder
                .ClearProviders()
                .AddSerilog(new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "Oms.Api")
                    .WriteTo.Console()
                    .WriteTo.OpenTelemetry()
                    .CreateLogger());
        });

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));
}
```

### Oms.Worker Startup Configuration

**File**: `Oms.Worker/Startup/ServiceCollectionExtensions.cs`

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Temporal Client & Worker Registration
        services.AddSingleton(sp =>
        {
            var options = new TemporalClientConnectOptions
            {
                TargetHost = configuration["Temporal:Host"],
                TargetPort = int.Parse(configuration["Temporal:Port"]),
                Namespace = configuration["Temporal:Namespace"]
            };
            return new TemporalClient(options);
        });

        // Task Queue Workers
        services.AddSingleton<TemporalWorkerHostedService>();

        // Activities (registered with Temporal)
        services.AddScoped<ValidateCommerceActivity>();
        services.AddScoped<CollectRiskActivity>();
        services.AddScoped<ValidateRiskActivity>();
        services.AddScoped<ValidatePaymentActivity>();
        services.AddScoped<EnrichOrderActivity>();
        services.AddScoped<UpdateDashboardActivity>();
        services.AddScoped<PublishFulfillmentActivity>();
        services.AddScoped<RefundPaymentActivity>();

        // Infrastructure
        services.AddHttpClients(configuration);
        services.AddKafkaProducer(configuration);
        services.AddExternalServiceClients(configuration);

        // Payload Codec (encryption)
        services.AddSingleton(sp =>
        {
            var masterKey = configuration["Encryption:MasterKey"];
            return new AesGcmPayloadCodec(Convert.FromBase64String(masterKey));
        });

        // Observability
        services.AddOpenTelemetry(configuration);
        services.AddLogging(configuration);

        return services;
    }

    // Similar helper methods as Api...
}
```

### Middleware Pipeline (Oms.Api)

**File**: `Oms.Api/Startup/ApplicationBuilderExtensions.cs`

```csharp
public static class ApplicationBuilderExtensions
{
    public static WebApplication ConfigureMiddleware(this WebApplication app)
    {
        // Exception handling (first)
        app.UseMiddleware<ErrorHandlingMiddleware>();

        // Tracing and logging
        app.UseMiddleware<TracingMiddleware>();
        app.UseMiddleware<LoggingMiddleware>();

        // HTTPS redirection
        app.UseHttpsRedirection();

        // CORS (if needed)
        app.UseCors("AllowAll");

        // Temporal status check
        app.UseTemporalHealthCheck();

        // Routing
        app.UseRouting();

        // Authentication/Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Endpoints
        app.MapControllers();
        app.MapHealthChecks("/health");
        app.MapPrometheusMetrics();

        // Swagger/OpenAPI
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}
```

---

## Build Configuration

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <DebugType>embedded</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

### Directory.Packages.props

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Framework -->
    <PackageVersion Include="Microsoft.AspNetCore.App" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    
    <!-- Temporal -->
    <PackageVersion Include="Temporalio" Version="1.2.0" />
    <PackageVersion Include="Temporalio.Extensions.OpenTelemetry" Version="1.2.0" />
    
    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    
    <!-- [All other packages as listed above] -->
  </ItemGroup>
</Project>
```

---

## Project Dependencies

### Dependency Graph

```
Oms.Api
├── Oms.Application
│   ├── Oms.Domain
│   ├── Oms.Contracts
│   └── Oms.Shared
├── Oms.Infrastructure
│   ├── Oms.Domain
│   ├── Oms.Shared
│   └── [External packages]
├── Oms.Temporal
│   ├── Oms.Contracts
│   ├── Oms.Shared
│   └── [Temporal packages]
└── Oms.Shared

Oms.Worker
├── Oms.Temporal
│   ├── Oms.Contracts
│   ├── Oms.Domain
│   └── Oms.Shared
├── Oms.Infrastructure
│   ├── Oms.Domain
│   └── Oms.Shared
└── Oms.Shared

Oms.Application
├── Oms.Domain
├── Oms.Contracts
└── Oms.Shared

Oms.Temporal
├── Oms.Contracts
├── Oms.Domain
└── Oms.Shared

Oms.Infrastructure
├── Oms.Domain
└── Oms.Shared

Oms.Domain
└── [No internal dependencies]

Oms.Contracts
└── [No internal dependencies]

Oms.Shared
└── [No internal dependencies]

Oms.Tests
├── Oms.Domain
├── Oms.Application
├── Oms.Infrastructure
├── Oms.Temporal
├── Oms.Api
└── [Testing packages]

Oms.ReplayTests
├── Oms.Temporal
└── [Testing packages]
```

### Layering Principles

1. **No circular dependencies**: Each layer depends only on layers below it
2. **One-way dependencies**: Application → Domain, Infrastructure → Domain
3. **Interface segregation**: Layers depend on abstractions (interfaces), not implementations
4. **Independent testability**: Each layer can be tested in isolation

### Dependency Constraints

- **Domain Layer**: Zero external dependencies (except base .NET)
- **Application Layer**: Depends on Domain only; no Infrastructure or Temporal
- **Infrastructure Layer**: Depends on Domain; no Application or Temporal
- **Temporal Layer**: Depends on Domain and Contracts; no Application
- **API Layer**: Orchestrates all layers; top-level entry point
- **Worker Layer**: Orchestrates Activities and Infrastructure; execution layer

---

## Build & Run Commands

### Local Development

```bash
# Restore packages
dotnet restore OmsTemporal.sln

# Build all projects
dotnet build OmsTemporal.sln

# Run API
dotnet run --project Oms.Api/Oms.Api.csproj

# Run Worker
dotnet run --project Oms.Worker/Oms.Worker.csproj

# Run tests
dotnet test Oms.Tests/Oms.Tests.csproj
dotnet test Oms.ReplayTests/Oms.ReplayTests.csproj
```

### Docker Build

```bash
# Build API image
docker build -f Oms.Api/Dockerfile -t oms-api:latest .

# Build Worker image
docker build -f Oms.Worker/Dockerfile -t oms-worker:latest .

# Run with compose
docker-compose up
```

---

## Next Steps

1. **Review** `ProjectSetup.md` for step-by-step scaffolding instructions
2. **Reference** `solution-manifest.json` for machine-readable configuration
3. **Follow** Architecture.md for design principles
4. **Implement** projects in dependency order (Domain → Application → Infrastructure → Temporal → Api/Worker)

