# Project Setup Guide

Complete step-by-step instructions to scaffold and configure the OmsTemporal solution for production-grade Order Management System.

---

## Prerequisites

### System Requirements

- **Operating System**: Linux, macOS, or Windows
- **.NET SDK**: Version 8.0 or later
- **Runtime**: .NET 8.0 Runtime
- **Git**: Latest version
- **Docker**: 20.10+ (for containerization)
- **Docker Compose**: 1.29+ (for local development)

### Verify Installation

```bash
dotnet --version      # Should output 8.0.xxx
git --version         # Should output 2.x+
docker --version      # Should output 20.10+
docker-compose --version  # Should output 1.29+
```

---

## Step 1: Create Solution Structure

### 1.1 Create Solution File

```bash
# Create solution directory
mkdir -p /path/to/OmsTemporal
cd /path/to/OmsTemporal

# Create solution file
dotnet new sln -n OmsTemporal

# Verify
ls -la OmsTemporal.sln
```

### 1.2 Create Shared Build Configuration Files

**Create `Directory.Build.props`**:

```bash
cat > Directory.Build.props << 'EOF'
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
EOF
```

**Create `Directory.Packages.props`**:

```bash
cat > Directory.Packages.props << 'EOF'
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Framework -->
    <PackageVersion Include="Microsoft.AspNetCore.App" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    
    <!-- Temporal -->
    <PackageVersion Include="Temporalio" Version="1.2.0" />
    <PackageVersion Include="Temporalio.Extensions.OpenTelemetry" Version="1.2.0" />
    <PackageVersion Include="Temporalio.Testing" Version="1.2.0" />
    
    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.SqlClient" Version="1.9.0" />
    <PackageVersion Include="OpenTelemetry.Exporter.Otlp" Version="1.9.0" />
    
    <!-- Logging & Metrics -->
    <PackageVersion Include="Serilog" Version="3.1.0" />
    <PackageVersion Include="Serilog.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Serilog.Sinks.OpenTelemetry" Version="1.0.0" />
    <PackageVersion Include="Prometheus.Client" Version="4.4.0" />
    
    <!-- Data Access -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageVersion Include="StackExchange.Redis" Version="2.7.0" />
    
    <!-- External Services -->
    <PackageVersion Include="Confluent.Kafka" Version="2.3.0" />
    <PackageVersion Include="Stripe.net" Version="42.0.0" />
    <PackageVersion Include="Polly" Version="8.2.0" />
    <PackageVersion Include="Refit" Version="7.0.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.3" />
    <PackageVersion Include="System.Text.Json" Version="8.0.0" />
    
    <!-- Security -->
    <PackageVersion Include="System.Security.Cryptography" Version="4.3.1" />
    <PackageVersion Include="Azure.Identity" Version="1.11.0" />
    <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.5.0" />
    
    <!-- Validation & Mapping -->
    <PackageVersion Include="FluentValidation" Version="11.8.0" />
    <PackageVersion Include="AutoMapper" Version="13.0.0" />
    <PackageVersion Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="12.0.0" />
    
    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.6.0" />
    <PackageVersion Include="Moq" Version="4.20.0" />
    <PackageVersion Include="FluentAssertions" Version="6.12.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
    
    <!-- Utilities -->
    <PackageVersion Include="GuardClauses" Version="4.2.0" />
    <PackageVersion Include="MediatR" Version="12.1.1" />
  </ItemGroup>
</Project>
EOF
```

---

## Step 2: Create Domain Layer Projects

### 2.1 Create Oms.Domain Class Library

```bash
# Create project
dotnet new classlib -n Oms.Domain -f net8.0
dotnet sln OmsTemporal.sln add Oms.Domain/Oms.Domain.csproj

# Create folder structure
mkdir -p Oms.Domain/{Aggregates,ValueObjects,DomainEvents,Exceptions,Enums}
mkdir -p Oms.Domain/Aggregates/{Order,Customer,Payment}

# Remove default class
rm Oms.Domain/Class1.cs
```

### 2.2 Create Domain Entities

**Create `Oms.Domain/Enums/OrderStatus.cs`**:

```csharp
namespace Oms.Domain.Enums;

public enum OrderStatus
{
    Initializing = 0,
    ValidatingOrder = 1,
    PendingCorrection = 2,
    CollectingRisk = 3,
    RiskRejected = 4,
    AwaitingPayment = 5,
    PaymentInvalid = 6,
    ValidatingPayment = 7,
    Enriching = 8,
    Fulfilled = 9,
    Expired = 10,
    Cancelled = 11,
    ProcessingError = 12
}
```

**Create `Oms.Domain/Aggregates/Order/Order.cs`**:

```csharp
namespace Oms.Domain.Aggregates.Order;

public class Order
{
    public Guid OrderId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public OrderStatus CurrentStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public decimal TotalAmount { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();

    private Order() { }

    public static Order Create(Guid customerId, string orderNumber, decimal totalAmount, DateTime expiresAt)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = customerId,
            CurrentStatus = OrderStatus.Initializing,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            UpdatedAt = DateTime.UtcNow,
            TotalAmount = totalAmount
        };
        return order;
    }

    public void UpdateStatus(OrderStatus newStatus)
    {
        CurrentStatus = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddItem(OrderItem item)
    {
        Items.Add(item);
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
}

public class OrderItem
{
    public Guid ItemId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    private OrderItem() { }

    public static OrderItem Create(string productCode, string productName, decimal unitPrice, int quantity)
    {
        return new OrderItem
        {
            ItemId = Guid.NewGuid(),
            ProductCode = productCode,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }

    public decimal GetTotalPrice() => UnitPrice * Quantity;
}
```

**Create `Oms.Domain/Enums/PaymentStatus.cs`**:

```csharp
namespace Oms.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Authorized = 2,
    Captured = 3,
    Failed = 4,
    Reversed = 5,
    Refunded = 6
}
```

**Create `Oms.Domain/Aggregates/Payment/Payment.cs`**:

```csharp
namespace Oms.Domain.Aggregates.Payment;

public class Payment
{
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string Gateway { get; private set; } = string.Empty;
    public string TransactionId { get; private set; } = string.Empty;
    public DateTime? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }

    private Payment() { }

    public static Payment Create(decimal amount, string gateway)
    {
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            Amount = amount,
            Gateway = gateway,
            Status = PaymentStatus.Pending,
            RetryCount = 0
        };
    }

    public void SetAuthorized(string transactionId)
    {
        Status = PaymentStatus.Authorized;
        TransactionId = transactionId;
        ProcessedAt = DateTime.UtcNow;
    }

    public void IncrementRetry()
    {
        RetryCount++;
    }
}
```

---

## Step 3: Create Application Layer

### 3.1 Create Oms.Application Class Library

```bash
dotnet new classlib -n Oms.Application -f net8.0
dotnet sln OmsTemporal.sln add Oms.Application/Oms.Application.csproj
dotnet add Oms.Application reference Oms.Domain

mkdir -p Oms.Application/{Services,DTOs,Mappers,Validators,CompensationStrategies}

rm Oms.Application/Class1.cs
```

### 3.2 Create DTOs and Mappers

**Create `Oms.Application/DTOs/OrderDto.cs`**:

```csharp
namespace Oms.Application.DTOs;

public class OrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class OrderItemDto
{
    public Guid ItemId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

### 3.3 Create Application Service

**Create `Oms.Application/Services/OrderService.cs`**:

```csharp
namespace Oms.Application.Services;

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMapper _mapper;

    public OrderService(IOrderRepository orderRepository, IMapper mapper)
    {
        _orderRepository = orderRepository;
        _mapper = mapper;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto createRequest)
    {
        var order = Order.Create(
            createRequest.CustomerId,
            createRequest.OrderNumber,
            createRequest.TotalAmount,
            DateTime.UtcNow.AddHours(24)
        );

        foreach (var item in createRequest.Items)
        {
            var orderItem = OrderItem.Create(
                item.ProductCode,
                item.ProductName,
                item.UnitPrice,
                item.Quantity
            );
            order.AddItem(orderItem);
        }

        await _orderRepository.AddAsync(order);
        return _mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto?> GetOrderAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        return order == null ? null : _mapper.Map<OrderDto>(order);
    }
}
```

---

## Step 4: Create Infrastructure Layer

### 4.1 Create Oms.Infrastructure Class Library

```bash
dotnet new classlib -n Oms.Infrastructure -f net8.0
dotnet sln OmsTemporal.sln add Oms.Infrastructure/Oms.Infrastructure.csproj
dotnet add Oms.Infrastructure reference Oms.Domain

mkdir -p Oms.Infrastructure/{ExternalServices,Persistence,HttpClients,Security}
mkdir -p Oms.Infrastructure/ExternalServices/{PaymentGateway,RiskEngine,PIM}

rm Oms.Infrastructure/Class1.cs
```

### 4.2 Update Infrastructure Project File

**Edit `Oms.Infrastructure/Oms.Infrastructure.csproj`**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="StackExchange.Redis" />
    <PackageReference Include="Confluent.Kafka" />
    <PackageReference Include="Refit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Oms.Domain/Oms.Domain.csproj" />
  </ItemGroup>
</Project>
```

---

## Step 5: Create Contracts Layer

### 5.1 Create Oms.Contracts Class Library

```bash
dotnet new classlib -n Oms.Contracts -f net8.0
dotnet sln OmsTemporal.sln add Oms.Contracts/Oms.Contracts.csproj

mkdir -p Oms.Contracts/{ActivityInputOutputs,WorkflowSignals,WorkflowQueries,ExternalServices}

rm Oms.Contracts/Class1.cs
```

### 5.2 Create Activity Contracts

**Create `Oms.Contracts/ActivityInputOutputs/ValidateCommerceActivity.cs`**:

```csharp
namespace Oms.Contracts.ActivityInputOutputs;

public class ValidateCommerceActivityInput
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItemInput> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class OrderItemInput
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class ValidateCommerceActivityOutput
{
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public OrderDto? ValidatedOrder { get; set; }
}

public class OrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public string ProductCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

---

## Step 6: Create Temporal Layer

### 6.1 Create Oms.Temporal Class Library

```bash
dotnet new classlib -n Oms.Temporal -f net8.0
dotnet sln OmsTemporal.sln add Oms.Temporal/Oms.Temporal.csproj
dotnet add Oms.Temporal reference Oms.Domain
dotnet add Oms.Temporal reference Oms.Contracts

mkdir -p Oms.Temporal/{Workflows,Activities,Codecs,SearchAttributes,Clients,Options}

rm Oms.Temporal/Class1.cs
```

### 6.2 Update Temporal Project File

**Edit `Oms.Temporal/Oms.Temporal.csproj`**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Temporalio" />
    <PackageReference Include="Temporalio.Extensions.OpenTelemetry" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../Oms.Domain/Oms.Domain.csproj" />
    <ProjectReference Include="../Oms.Contracts/Oms.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### 6.3 Create Workflow Definition

**Create `Oms.Temporal/Workflows/OrderProcessingWorkflow.cs`**:

```csharp
namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;

[Workflow]
public class OrderProcessingWorkflow
{
    private OrderStatus currentStatus = OrderStatus.Initializing;
    private OrderDto? orderData;

    [WorkflowRun]
    public async Task RunAsync(Guid orderId, OrderDto initialOrder)
    {
        orderData = initialOrder;
        currentStatus = OrderStatus.Initializing;

        // Validate order
        currentStatus = OrderStatus.ValidatingOrder;
        try
        {
            // Schedule activity
            // await Workflow.ExecuteActivityAsync(
            //     (ValidateCommerceActivity act) => act.ExecuteAsync(
            //         new ValidateCommerceActivityInput { OrderId = orderId }
            //     )
            // );
        }
        catch (Exception ex)
        {
            currentStatus = OrderStatus.ProcessingError;
            throw;
        }

        currentStatus = OrderStatus.Fulfilled;
    }

    [WorkflowSignal]
    public async Task CancelOrderAsync(string reason)
    {
        currentStatus = OrderStatus.Cancelled;
    }

    [WorkflowQuery]
    public OrderStatus GetStatus() => currentStatus;

    [WorkflowQuery]
    public OrderDto? GetOrderDetails() => orderData;
}

public enum OrderStatus
{
    Initializing,
    ValidatingOrder,
    CollectingRisk,
    AwaitingPayment,
    ValidatingPayment,
    Enriching,
    Fulfilled,
    Cancelled
}
```

### 6.4 Create Activity Implementation

**Create `Oms.Temporal/Activities/ValidateCommerceActivity.cs`**:

```csharp
namespace Oms.Temporal.Activities;

using Temporalio.Activities;

public class ValidateCommerceActivity
{
    [Activity]
    public async Task<ValidateCommerceActivityOutput> ExecuteAsync(
        ValidateCommerceActivityInput input)
    {
        // Implement validation logic
        // Call external PIM service
        // Return validation results

        return new ValidateCommerceActivityOutput
        {
            IsValid = true,
            ValidationErrors = new(),
            ValidatedOrder = new()
            {
                OrderId = input.OrderId,
                TotalAmount = input.TotalAmount,
                Items = new()
            }
        };
    }
}
```

---

## Step 7: Create Shared Layer

### 7.1 Create Oms.Shared Class Library

```bash
dotnet new classlib -n Oms.Shared -f net8.0
dotnet sln OmsTemporal.sln add Oms.Shared/Oms.Shared.csproj

mkdir -p Oms.Shared/{Logging,Tracing,Metrics,Exceptions,Constants,Extensions}

rm Oms.Shared/Class1.cs
```

### 7.2 Create Shared Interfaces

**Create `Oms.Shared/Logging/Logger.cs`**:

```csharp
namespace Oms.Shared.Logging;

using Microsoft.Extensions.Logging;

public interface ILogger
{
    void LogInfo(string message, Dictionary<string, object>? attributes = null);
    void LogError(string message, Exception? ex = null, Dictionary<string, object>? attributes = null);
    void LogWarning(string message, Dictionary<string, object>? attributes = null);
}

public class LoggerImpl : ILogger
{
    private readonly ILogger<LoggerImpl> _logger;

    public LoggerImpl(ILogger<LoggerImpl> logger)
    {
        _logger = logger;
    }

    public void LogInfo(string message, Dictionary<string, object>? attributes = null)
    {
        _logger.LogInformation(message);
    }

    public void LogError(string message, Exception? ex = null, Dictionary<string, object>? attributes = null)
    {
        _logger.LogError(ex, message);
    }

    public void LogWarning(string message, Dictionary<string, object>? attributes = null)
    {
        _logger.LogWarning(message);
    }
}
```

---

## Step 8: Create API Layer

### 8.1 Create Oms.Api ASP.NET Core Project

```bash
dotnet new webapi -n Oms.Api -f net8.0
dotnet sln OmsTemporal.sln add Oms.Api/Oms.Api.csproj
dotnet add Oms.Api reference Oms.Application
dotnet add Oms.Api reference Oms.Infrastructure
dotnet add Oms.Api reference Oms.Temporal
dotnet add Oms.Api reference Oms.Shared

mkdir -p Oms.Api/{Controllers,Middleware,Extensions,Startup,Requests,Responses}
```

### 8.2 Create API Controller

**Create `Oms.Api/Controllers/OrderController.cs`**:

```csharp
namespace Oms.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Oms.Application.Services;
using Oms.Api.Requests;
using Oms.Api.Responses;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ILogger<OrderController> _logger;

    public OrderController(OrderService orderService, ILogger<OrderController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> CreateOrderAsync(
        [FromBody] CreateOrderRequest request)
    {
        try
        {
            var orderDto = await _orderService.CreateOrderAsync(
                new CreateOrderDto
                {
                    CustomerId = request.CustomerId,
                    OrderNumber = request.OrderNumber,
                    TotalAmount = request.TotalAmount,
                    Items = request.Items.Select(i => new CreateOrderItemDto
                    {
                        ProductCode = i.ProductCode,
                        ProductName = i.ProductName,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity
                    }).ToList()
                }
            );

            return CreatedAtAction(nameof(GetOrderAsync), 
                new { id = orderDto.OrderId }, 
                new CreateOrderResponse
                {
                    OrderId = orderDto.OrderId,
                    OrderNumber = orderDto.OrderNumber,
                    Status = "Created"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrderAsync(Guid id)
    {
        var order = await _orderService.GetOrderAsync(id);
        if (order == null)
            return NotFound();

        return Ok(order);
    }
}
```

### 8.3 Create Program.cs with DI

**Edit `Oms.Api/Program.cs`**:

```csharp
using Oms.Application.Services;
using Oms.Infrastructure;
using Oms.Temporal;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application services
builder.Services.AddScoped<OrderService>();

// Infrastructure services
builder.Services.AddInfrastructureServices(builder.Configuration);

// Temporal services
builder.Services.AddTemporalServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## Step 9: Create Worker Service

### 9.1 Create Oms.Worker Console App

```bash
dotnet new console -n Oms.Worker -f net8.0
dotnet sln OmsTemporal.sln add Oms.Worker/Oms.Worker.csproj
dotnet add Oms.Worker reference Oms.Temporal
dotnet add Oms.Worker reference Oms.Infrastructure
dotnet add Oms.Worker reference Oms.Shared

mkdir -p Oms.Worker/{HostedServices,Configuration,TaskQueues}
```

### 9.2 Create Worker Program

**Edit `Oms.Worker/Program.cs`**:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oms.Worker.HostedServices;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Add Temporal services
        services.AddScoped<TemporalWorkerHostedService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .Build();

var workerService = host.Services.GetRequiredService<TemporalWorkerHostedService>();
await workerService.ExecuteAsync(CancellationToken.None);
```

---

## Step 10: Create Test Projects

### 10.1 Create Oms.Tests

```bash
dotnet new xunit -n Oms.Tests -f net8.0
dotnet sln OmsTemporal.sln add Oms.Tests/Oms.Tests.csproj
dotnet add Oms.Tests reference Oms.Domain
dotnet add Oms.Tests reference Oms.Application
dotnet add Oms.Tests reference Oms.Temporal

mkdir -p Oms.Tests/{Unit,Integration,Fixtures,Mocks}

# Remove default test
rm Oms.Tests/UnitTest1.cs
```

### 10.2 Create Oms.ReplayTests

```bash
dotnet new xunit -n Oms.ReplayTests -f net8.0
dotnet sln OmsTemporal.sln add Oms.ReplayTests/Oms.ReplayTests.csproj
dotnet add Oms.ReplayTests reference Oms.Temporal

mkdir -p Oms.ReplayTests/{EventHistories,ReplayTestRunner}

rm Oms.ReplayTests/UnitTest1.cs
```

---

## Step 11: Build and Verify Solution

### 11.1 Restore All Dependencies

```bash
dotnet restore OmsTemporal.sln
```

### 11.2 Build Solution

```bash
dotnet build OmsTemporal.sln
```

### 11.3 Run Tests

```bash
dotnet test Oms.Tests/Oms.Tests.csproj
dotnet test Oms.ReplayTests/Oms.ReplayTests.csproj
```

### 11.4 Verify Solution Structure

```bash
dotnet sln OmsTemporal.sln list
```

Should output:
```
Project(s) in solution:
  Oms.Domain/Oms.Domain.csproj
  Oms.Application/Oms.Application.csproj
  Oms.Infrastructure/Oms.Infrastructure.csproj
  Oms.Contracts/Oms.Contracts.csproj
  Oms.Temporal/Oms.Temporal.csproj
  Oms.Shared/Oms.Shared.csproj
  Oms.Api/Oms.Api.csproj
  Oms.Worker/Oms.Worker.csproj
  Oms.Tests/Oms.Tests.csproj
  Oms.ReplayTests/Oms.ReplayTests.csproj
```

---

## Step 12: Docker Setup

### 12.1 Create Docker Configuration Files

**Create `Dockerfile` for API**:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Oms.Api/Oms.Api.csproj", "Oms.Api/"]
COPY ["Oms.Application/Oms.Application.csproj", "Oms.Application/"]
COPY ["Oms.Domain/Oms.Domain.csproj", "Oms.Domain/"]
COPY ["Oms.Infrastructure/Oms.Infrastructure.csproj", "Oms.Infrastructure/"]
COPY ["Oms.Temporal/Oms.Temporal.csproj", "Oms.Temporal/"]
COPY ["Oms.Shared/Oms.Shared.csproj", "Oms.Shared/"]
COPY ["Oms.Contracts/Oms.Contracts.csproj", "Oms.Contracts/"]

RUN dotnet restore "Oms.Api/Oms.Api.csproj"
COPY . .
WORKDIR "/src/Oms.Api"
RUN dotnet build "Oms.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Oms.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "Oms.Api.dll"]
```

### 12.2 Create docker-compose.yml

```yaml
version: '3.8'

services:
  temporal:
    image: temporalio/auto-setup:latest
    environment:
      DB: postgresql
      DB_PORT: 5432
      POSTGRES_PWD: password
      POSTGRES_SEEDS: temporal-db
    ports:
      - "7233:7233"
    depends_on:
      - temporal-db

  temporal-db:
    image: postgres:15-alpine
    environment:
      POSTGRES_PASSWORD: password
    volumes:
      - postgres_data:/var/lib/postgresql/data

  oms-api:
    build:
      context: .
      dockerfile: Oms.Api/Dockerfile
    ports:
      - "5000:80"
    environment:
      - Temporal__Host=temporal
      - Temporal__Port=7233

  oms-worker:
    build:
      context: .
      dockerfile: Oms.Worker/Dockerfile
    depends_on:
      - temporal

volumes:
  postgres_data:
```

---

## Step 13: Local Development Setup

### 13.1 Start Temporal Locally

```bash
docker-compose up temporal temporal-db
```

### 13.2 Run API Locally

```bash
dotnet run --project Oms.Api/Oms.Api.csproj
```

### 13.3 Run Worker Locally (in another terminal)

```bash
dotnet run --project Oms.Worker/Oms.Worker.csproj
```

### 13.4 Test API Endpoint

```bash
curl -X POST http://localhost:5000/api/order \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "550e8400-e29b-41d4-a716-446655440000",
    "orderNumber": "ORD-001",
    "totalAmount": 99.99,
    "items": []
  }'
```

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| **Dotnet SDK not found** | Install .NET 8 from https://dotnet.microsoft.com/download |
| **Port 7233 already in use** | Change Temporal port in docker-compose.yml |
| **Package restore fails** | Run `dotnet nuget locals all --clear` and retry |
| **Build fails** | Delete `bin` and `obj` folders: `dotnet clean` |
| **Temporal connection error** | Ensure Temporal container is running: `docker ps` |

---

## Next Steps

1. Review [SolutionStructure.md](SolutionStructure.md) for project organization
2. Follow [Architecture.md](Architecture.md) for design patterns
3. Implement domain models based on Architecture.md requirements
4. Implement activities and workflows
5. Create comprehensive tests

