# Temporal Infrastructure Guide

Complete specification for Temporal workflow orchestration, activities, signals, queries, and worker registration.

---

## Overview

This guide covers the Temporal .NET SDK implementation layer:

1. **Workflow Interface** - `IOrderProcessingWorkflow` with signal and query handlers
2. **Activity Interfaces** - 7 activities with timeout, retry, and heartbeat specs
3. **Signals** - Dynamic channel communication to running workflows
4. **Queries** - Real-time state queries from running workflows
5. **Worker Registration** - Setup and task queue configuration
6. **Payload Codec** - AES-256-GCM encryption for event history
7. **Error Handling** - Activity failures, retries, timeouts

---

## Part 1: Workflow Interface

### IOrderProcessingWorkflow

```csharp
namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;
using Oms.Contracts.ActivityInputOutputs;
using Oms.Contracts.WorkflowSignals;
using Oms.Contracts.WorkflowQueries;

/// <summary>
/// Main order processing workflow orchestrating all activities and state transitions.
/// Handles signals for cancellation and corrections, queries for state inspection.
/// </summary>
[Workflow]
public interface IOrderProcessingWorkflow
{
    /// <summary>
    /// Main workflow entry point. Orchestrates order processing through all activities.
    /// </summary>
    /// <param name="input">Order and customer data from API</param>
    /// <returns>Completed order with all enrichments</returns>
    [WorkflowRun]
    Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input);

    /// <summary>
    /// Signal: Cancel order at any point in workflow
    /// </summary>
    [WorkflowSignal]
    Task HandleCancelOrderSignalAsync(CancelOrderSignal signal);

    /// <summary>
    /// Signal: Request correction and retry validation
    /// </summary>
    [WorkflowSignal]
    Task HandleRequestCorrectionSignalAsync(RequestCorrectionSignal signal);

    /// <summary>
    /// Signal: Manager approval for high-risk orders
    /// </summary>
    [WorkflowSignal]
    Task HandleApproveRiskSignalAsync(ApproveRiskSignal signal);

    /// <summary>
    /// Query: Get current order status
    /// </summary>
    [WorkflowQuery]
    Task<GetOrderStatusResult> GetOrderStatusAsync(GetOrderStatusQuery query);

    /// <summary>
    /// Query: Get full order details and enrichment data
    /// </summary>
    [WorkflowQuery]
    Task<GetOrderDetailsResult> GetOrderDetailsAsync(GetOrderDetailsQuery query);

    /// <summary>
    /// Query: Get payment-specific information
    /// </summary>
    [WorkflowQuery]
    Task<GetPaymentStatusResult> GetPaymentStatusAsync(GetPaymentStatusQuery query);

    /// <summary>
    /// Query: Get risk assessment details
    /// </summary>
    [WorkflowQuery]
    Task<GetRiskAssessmentResult> GetRiskAssessmentAsync(GetRiskAssessmentQuery query);
}

/// <summary>Workflow input containing order and customer data</summary>
public class ProcessOrderInput
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerSegment { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public List<OrderItemInput> Items { get; set; } = new();
    
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public string PaymentToken { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    
    // SLA configuration
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromHours(24);
    public string TaskQueue { get; set; } = "OMS_QUEUE";
}

/// <summary>Workflow output containing final order state</summary>
public class OrderProcessingResult
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string FinalStatus { get; set; } = string.Empty;
    
    public OrderDto? Order { get; set; }
    public PaymentDto? Payment { get; set; }
    public RiskDataDto? RiskAssessment { get; set; }
    public EnrichedOrderDto? EnrichedData { get; set; }
    
    public DateTime CompletedAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Addresses used in workflow input</summary>
public class ShippingAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
```

### Workflow Execution Context

```csharp
/// <summary>Shared state maintained across workflow execution</summary>
public class WorkflowExecutionContext
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    
    public OrderDto? Order { get; set; }
    public PaymentDto? Payment { get; set; }
    public RiskDataDto? RiskAssessment { get; set; }
    public EnrichedOrderDto? EnrichedData { get; set; }
    
    // Tracking state
    public bool IsCancelled { get; set; }
    public bool IsCorrectionRequested { get; set; }
    public bool IsRiskApproved { get; set; }
    public int CorrectionAttempts { get; set; }
    
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
```

---

## Part 2: Activity Interfaces

### Base Activity Interface

```csharp
namespace Oms.Temporal.Activities;

using Temporalio.Activities;

/// <summary>Base interface for all activities with common properties</summary>
public interface IBaseActivity
{
    /// <summary>Activity name for logging and monitoring</summary>
    string ActivityName { get; }

    /// <summary>Activity version for compatibility</summary>
    int ActivityVersion { get; }
}
```

### ValidateCommerceActivity

```csharp
/// <summary>
/// Activity: Validate order against commerce system
/// Timeout: 30s | Retries: 3 | Heartbeat: 10s
/// </summary>
[Activity]
public interface IValidateCommerceActivity : IBaseActivity
{
    /// <summary>
    /// Validates order details including product availability and pricing.
    /// Fails fast if items not found or pricing mismatch > 5%.
    /// </summary>
    [ActivityMethod(Name = "ValidateCommerceActivity")]
    Task<ValidateCommerceActivityOutput> ExecuteAsync(
        ValidateCommerceActivityInput input);
}

/// <summary>Activity implementation skeleton</summary>
public class ValidateCommerceActivity : IValidateCommerceActivity
{
    public string ActivityName => "ValidateCommerceActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<ValidateCommerceActivityOutput> ExecuteAsync(
        ValidateCommerceActivityInput input)
    {
        // Timeout: 30 seconds
        // Retry Policy: 3 attempts, exponential backoff 2s base
        // Heartbeat: Every 10 seconds (implicit in long operations)
        // Idempotency: Yes - checks cache by (OrderId, attempt)
        
        throw new NotImplementedException();
    }
}
```

### CollectRiskActivity

```csharp
/// <summary>
/// Activity: Collect risk assessment from external risk engine
/// Timeout: 45s | Retries: 2 | Heartbeat: 15s
/// </summary>
[Activity]
public interface ICollectRiskActivity : IBaseActivity
{
    /// <summary>
    /// Calls external risk engine API with order details.
    /// Returns RiskLevel (Low, Medium, High, Critical) and scoring.
    /// If service unavailable, fails workflow (manual retry via signal).
    /// </summary>
    [ActivityMethod(Name = "CollectRiskActivity")]
    Task<CollectRiskActivityOutput> ExecuteAsync(
        CollectRiskActivityInput input);
}

public class CollectRiskActivity : ICollectRiskActivity
{
    public string ActivityName => "CollectRiskActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<CollectRiskActivityOutput> ExecuteAsync(
        CollectRiskActivityInput input)
    {
        // Timeout: 45 seconds
        // Retry Policy: 2 attempts, exponential backoff 3s base
        // Heartbeat: Every 15 seconds
        // Idempotency: Yes - external service provides idempotency key
        
        throw new NotImplementedException();
    }
}
```

### ValidatePaymentActivity

```csharp
/// <summary>
/// Activity: Validate and authorize payment with gateway
/// Timeout: 60s | Retries: 3 | Heartbeat: 20s
/// </summary>
[Activity]
public interface IValidatePaymentActivity : IBaseActivity
{
    /// <summary>
    /// Authorizes payment with configured gateway (Stripe/Square).
    /// Does NOT capture funds, only authorizes hold.
    /// Returns transaction ID for later capture or refund.
    /// </summary>
    [ActivityMethod(Name = "ValidatePaymentActivity")]
    Task<ValidatePaymentActivityOutput> ExecuteAsync(
        ValidatePaymentActivityInput input);
}

public class ValidatePaymentActivity : IValidatePaymentActivity
{
    public string ActivityName => "ValidatePaymentActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<ValidatePaymentActivityOutput> ExecuteAsync(
        ValidatePaymentActivityInput input)
    {
        // Timeout: 60 seconds (gateway calls can be slow)
        // Retry Policy: 3 attempts, exponential backoff 2s base
        // Heartbeat: Every 20 seconds
        // Idempotency: Yes - idempotency key = OrderId + attempt
        
        throw new NotImplementedException();
    }
}
```

### EnrichOrderActivity

```csharp
/// <summary>
/// Activity: Enrich order with PIM (Product Information Management) data
/// Timeout: 90s | Retries: 2 | Heartbeat: 30s
/// </summary>
[Activity]
public interface IEnrichOrderActivity : IBaseActivity
{
    /// <summary>
    /// Calls PIM system to enrich product information:
    /// - Category, manufacturer, tags, pricing adjustments
    /// - May adjust prices based on customer segment
    /// - Incremental operation (fetches only missing enrichment)
    /// </summary>
    [ActivityMethod(Name = "EnrichOrderActivity")]
    Task<EnrichOrderActivityOutput> ExecuteAsync(
        EnrichOrderActivityInput input);
}

public class EnrichOrderActivity : IEnrichOrderActivity
{
    public string ActivityName => "EnrichOrderActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<EnrichOrderActivityOutput> ExecuteAsync(
        EnrichOrderActivityInput input)
    {
        // Timeout: 90 seconds (PIM can be slow with many items)
        // Retry Policy: 2 attempts, exponential backoff 5s base
        // Heartbeat: Every 30 seconds (long-running)
        // Idempotency: Yes - caches by OrderId
        
        throw new NotImplementedException();
    }
}
```

### PublishFulfillmentActivity

```csharp
/// <summary>
/// Activity: Publish order to Kafka for fulfillment system
/// Timeout: 30s | Retries: 5 | Heartbeat: 10s
/// </summary>
[Activity]
public interface IPublishFulfillmentActivity : IBaseActivity
{
    /// <summary>
    /// Publishes completed order to Kafka 'fulfillment-orders' topic.
    /// Event consumed by fulfillment system for warehouse processing.
    /// Supports auto-retry on broker unavailability (5 attempts).
    /// </summary>
    [ActivityMethod(Name = "PublishFulfillmentActivity")]
    Task<PublishFulfillmentActivityOutput> ExecuteAsync(
        PublishFulfillmentActivityInput input);
}

public class PublishFulfillmentActivity : IPublishFulfillmentActivity
{
    public string ActivityName => "PublishFulfillmentActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<PublishFulfillmentActivityOutput> ExecuteAsync(
        PublishFulfillmentActivityInput input)
    {
        // Timeout: 30 seconds
        // Retry Policy: 5 attempts, linear backoff 1s base
        // Heartbeat: Every 10 seconds
        // Idempotency: Yes - Kafka deduplicates by transaction ID
        
        throw new NotImplementedException();
    }
}
```

### RequestApprovalActivity

```csharp
/// <summary>
/// Activity: Send approval request to manager (e.g., risk override)
/// Timeout: No timeout (waits for human approval) | Retries: 1 | Heartbeat: 60s
/// </summary>
[Activity]
public interface IRequestApprovalActivity : IBaseActivity
{
    /// <summary>
    /// Sends approval request and waits for response via webhook.
    /// Does NOT timeout - uses webhook/callback pattern.
    /// Workflow continues via signal from approval service.
    /// </summary>
    [ActivityMethod(Name = "RequestApprovalActivity")]
    Task<RequestApprovalActivityOutput> ExecuteAsync(
        RequestApprovalActivityInput input);
}

public class RequestApprovalActivity : IRequestApprovalActivity
{
    public string ActivityName => "RequestApprovalActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<RequestApprovalActivityOutput> ExecuteAsync(
        RequestApprovalActivityInput input)
    {
        // Timeout: None (heartbeat-only - waits indefinitely)
        // Retry Policy: 1 attempt (manual approval, no auto-retry)
        // Heartbeat: Every 60 seconds (keep alive)
        // Idempotency: Yes - uses ApprovalRequestId for deduplication
        
        throw new NotImplementedException();
    }
}
```

### PublishEventActivity

```csharp
/// <summary>
/// Activity: Publish domain events (status changes, cancellations)
/// Timeout: 20s | Retries: 3 | Heartbeat: 5s
/// </summary>
[Activity]
public interface IPublishEventActivity : IBaseActivity
{
    /// <summary>
    /// Publishes order events to Kafka for event streaming.
    /// Supports OrderStatusChangedEvent, OrderCancelledEvent, OrderFailedEvent.
    /// Used for audit trail and downstream system notifications.
    /// </summary>
    [ActivityMethod(Name = "PublishEventActivity")]
    Task<PublishEventActivityOutput> ExecuteAsync(
        PublishEventActivityInput input);
}

public class PublishEventActivity : IPublishEventActivity
{
    public string ActivityName => "PublishEventActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<PublishEventActivityOutput> ExecuteAsync(
        PublishEventActivityInput input)
    {
        // Timeout: 20 seconds
        // Retry Policy: 3 attempts, linear backoff 1s base
        // Heartbeat: Every 5 seconds
        // Idempotency: Yes - event deduplication by EventId
        
        throw new NotImplementedException();
    }
}

public class PublishEventActivityInput
{
    public string EventType { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? EventData { get; set; }
}

public class PublishEventActivityOutput
{
    public int KafkaPartition { get; set; }
    public long KafkaOffset { get; set; }
    public DateTime PublishedAt { get; set; }
}
```

---

## Part 3: Signals & Queries

### Signal Handlers

```csharp
namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;

/// <summary>Workflow signal handlers for dynamic workflow control</summary>
public partial class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    private readonly WorkflowExecutionContext _context = new();
    
    // Signal channels
    private Channel<CancelOrderSignal>? _cancelChannel;
    private Channel<RequestCorrectionSignal>? _correctionChannel;
    private Channel<ApproveRiskSignal>? _approvalChannel;

    /// <summary>Handle cancellation signal - can be sent at any point</summary>
    [WorkflowSignal(Name = "CancelOrderSignal")]
    public async Task HandleCancelOrderSignalAsync(CancelOrderSignal signal)
    {
        _context.IsCancelled = true;
        _context.CancellationReason = signal.CancellationReason;
        
        // Signal is buffered in channel for workflow to handle
        // Workflow can gracefully cancel activities and cleanup
        await _cancelChannel!.Writer.WriteAsync(signal);
    }

    /// <summary>Handle correction signal - send corrected order data</summary>
    [WorkflowSignal(Name = "RequestCorrectionSignal")]
    public async Task HandleRequestCorrectionSignalAsync(RequestCorrectionSignal signal)
    {
        _context.IsCorrectionRequested = true;
        _context.CorrectionAttempts++;
        
        // Signal contains corrected items for retry
        await _correctionChannel!.Writer.WriteAsync(signal);
    }

    /// <summary>Handle manager approval for high-risk orders</summary>
    [WorkflowSignal(Name = "ApproveRiskSignal")]
    public async Task HandleApproveRiskSignalAsync(ApproveRiskSignal signal)
    {
        _context.IsRiskApproved = true;
        
        // Approval signal unblocks waiting activity
        await _approvalChannel!.Writer.WriteAsync(signal);
    }
}
```

### Query Handlers

```csharp
/// <summary>Query handlers - inspect workflow state without blocking</summary>
public partial class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    /// <summary>Query: Get current order status</summary>
    [WorkflowQuery(Name = "GetOrderStatus")]
    public async Task<GetOrderStatusResult> GetOrderStatusAsync(GetOrderStatusQuery query)
    {
        return await Task.FromResult(new GetOrderStatusResult
        {
            Status = _context.CurrentStatus,
            UpdatedAt = DateTime.UtcNow,
            IsComplete = _context.CompletedAt.HasValue
        });
    }

    /// <summary>Query: Get full order details and enrichments</summary>
    [WorkflowQuery(Name = "GetOrderDetails")]
    public async Task<GetOrderDetailsResult> GetOrderDetailsAsync(GetOrderDetailsQuery query)
    {
        return await Task.FromResult(new GetOrderDetailsResult
        {
            OrderId = _context.OrderId,
            OrderNumber = _context.OrderNumber,
            Status = _context.CurrentStatus,
            TotalAmount = _context.Order?.TotalAmount ?? 0,
            Items = _context.Order?.Items.Select(i => new OrderItemDetailDto
            {
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList() ?? new(),
            CreatedAt = _context.CreatedAt,
            ExpiresAt = _context.CreatedAt.AddHours(24),
            CorrectionAttempts = _context.CorrectionAttempts
        });
    }

    /// <summary>Query: Get payment status and transaction history</summary>
    [WorkflowQuery(Name = "GetPaymentStatus")]
    public async Task<GetPaymentStatusResult> GetPaymentStatusAsync(GetPaymentStatusQuery query)
    {
        return await Task.FromResult(new GetPaymentStatusResult
        {
            PaymentStatus = _context.Payment?.Status ?? "NotInitiated",
            Amount = _context.Payment?.Amount ?? 0,
            RetryCount = _context.Payment?.RetryCount ?? 0,
            TransactionIds = _context.Payment?.Transactions
                .Select(t => t.TransactionId.ToString())
                .ToList() ?? new(),
            ProcessedAt = _context.Payment?.ProcessedAt
        });
    }

    /// <summary>Query: Get risk assessment results</summary>
    [WorkflowQuery(Name = "GetRiskAssessment")]
    public async Task<GetRiskAssessmentResult> GetRiskAssessmentAsync(GetRiskAssessmentQuery query)
    {
        return await Task.FromResult(new GetRiskAssessmentResult
        {
            RiskLevel = _context.RiskAssessment?.Level ?? "NotAssessed",
            RiskScore = _context.RiskAssessment?.RiskScore ?? 0,
            RiskIndicators = _context.RiskAssessment?.Indicators
                .Select(i => i.RiskFactor)
                .ToList() ?? new(),
            RequiresManualReview = _context.RiskAssessment?.RequiresManualReview ?? false,
            EvaluatedAt = _context.RiskAssessment?.EvaluatedAt ?? DateTime.UtcNow
        });
    }
}
```

---

## Part 4: Worker Registration

### Worker Setup

```csharp
namespace Oms.Worker;

using Temporalio.Client;
using Temporalio.Worker;
using Oms.Temporal.Workflows;
using Oms.Temporal.Activities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Hosted service for Temporal worker lifecycle management</summary>
public class TemporalWorkerHostedService : BackgroundService
{
    private readonly ITemporalClient _temporalClient;
    private readonly ILogger<TemporalWorkerHostedService> _logger;
    private TemporalWorker? _worker;

    public TemporalWorkerHostedService(
        ITemporalClient temporalClient,
        ILogger<TemporalWorkerHostedService> logger)
    {
        _temporalClient = temporalClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Temporal worker...");

        try
        {
            // Create worker with configuration
            var options = new TemporalWorkerOptions()
                .AddWorkflow<OrderProcessingWorkflow>()
                .AddActivity<ValidateCommerceActivity>()
                .AddActivity<CollectRiskActivity>()
                .AddActivity<ValidatePaymentActivity>()
                .AddActivity<EnrichOrderActivity>()
                .AddActivity<PublishFulfillmentActivity>()
                .AddActivity<RequestApprovalActivity>()
                .AddActivity<PublishEventActivity>();

            _worker = new TemporalWorker(
                _temporalClient,
                taskQueue: "OMS_QUEUE",
                options);

            // Run worker until stopped
            await _worker.ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Temporal worker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Temporal worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Temporal worker...");
        
        if (_worker != null)
        {
            await _worker.ShutdownAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}
```

### Task Queue Configuration

```csharp
namespace Oms.Temporal.Configuration;

/// <summary>Task queue definitions and configuration</summary>
public static class TaskQueues
{
    /// <summary>Main workflow task queue - processes order workflows</summary>
    public const string OmsQueue = "OMS_QUEUE";

    /// <summary>
    /// External services queue - calls to external APIs (payments, risk, PIM).
    /// Separated to prevent head-of-line blocking.
    /// </summary>
    public const string CommerceQueue = "COMMERCE_QUEUE";

    /// <summary>
    /// Event publishing queue - publishes to Kafka.
    /// Separated for performance isolation.
    /// </summary>
    public const string FulfillmentQueue = "FULFILLMENT_QUEUE";

    /// <summary>
    /// Approval workflows - human approval requests.
    /// Lower concurrency, longer timeouts.
    /// </summary>
    public const string ApprovalQueue = "APPROVAL_QUEUE";
}

/// <summary>Worker configuration for task queue routing</summary>
public class WorkerConfiguration
{
    /// <summary>Main workflow worker - 100 concurrent workflows</summary>
    public static TemporalWorkerOptions ConfigureMainWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 10;
        options.MaxConcurrentWorkflowTaskExecutors = 100;
        options.MaxConcurrentLocalActivityExecutors = 50;

        return options;
    }

    /// <summary>Commerce queue worker - 20 concurrent external calls</summary>
    public static TemporalWorkerOptions ConfigureCommerceWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 20;
        options.MaxConcurrentWorkflowTaskExecutors = 5;
        options.MaxConcurrentLocalActivityExecutors = 10;

        return options;
    }

    /// <summary>Fulfillment queue worker - 50 concurrent publications</summary>
    public static TemporalWorkerOptions ConfigureFulfillmentWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 50;
        options.MaxConcurrentWorkflowTaskExecutors = 5;
        options.MaxConcurrentLocalActivityExecutors = 20;

        return options;
    }

    /// <summary>Approval queue worker - 5 concurrent approvals</summary>
    public static TemporalWorkerOptions ConfigureApprovalWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 5;
        options.MaxConcurrentWorkflowTaskExecutors = 5;
        options.MaxConcurrentLocalActivityExecutors = 2;
        options.HeartbeatTimeout = TimeSpan.FromMinutes(5);

        return options;
    }
}
```

### DI Registration

```csharp
namespace Oms.Api.Configuration;

using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Oms.Worker;
using Oms.Temporal.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Temporal SDK dependency injection setup</summary>
public static class TemporalServiceExtensions
{
    public static IServiceCollection AddTemporalServices(
        this IServiceCollection services,
        string serverAddress = "localhost:7233")
    {
        // Register Temporal client
        services.AddTemporalClient(
            clientOptions => new TemporalClientOptions { TargetHost = serverAddress });

        // Register worker hosted service
        services.AddHostedService<TemporalWorkerHostedService>();

        // Register activity implementations
        services.AddScoped<IValidateCommerceActivity, ValidateCommerceActivity>();
        services.AddScoped<ICollectRiskActivity, CollectRiskActivity>();
        services.AddScoped<IValidatePaymentActivity, ValidatePaymentActivity>();
        services.AddScoped<IEnrichOrderActivity, EnrichOrderActivity>();
        services.AddScoped<IPublishFulfillmentActivity, PublishFulfillmentActivity>();
        services.AddScoped<IRequestApprovalActivity, RequestApprovalActivity>();
        services.AddScoped<IPublishEventActivity, PublishEventActivity>();

        // Register workflow
        services.AddScoped<IOrderProcessingWorkflow, OrderProcessingWorkflow>();

        return services;
    }

    public static IServiceCollection AddTemporalClient(
        this IServiceCollection services,
        Func<IServiceProvider, TemporalClientOptions> optionsFactory)
    {
        services.AddSingleton(provider =>
        {
            var options = optionsFactory(provider);
            return new TemporalClient(options);
        });

        return services;
    }
}
```

---

## Part 5: Payload Codec

### AES-256-GCM Encryption

```csharp
namespace Oms.Temporal.Codec;

using Temporalio.Sdk.Interop;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Workflowservice.V1;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

/// <summary>
/// Payload codec for AES-256-GCM encryption of event history.
/// Encrypts all workflow event data at rest in Temporal history.
/// </summary>
public class AesGcmPayloadCodec : IPayloadCodec
{
    private readonly byte[] _encryptionKey;
    private readonly ILogger<AesGcmPayloadCodec> _logger;
    private const string EncodingType = "AES-256-GCM";

    public AesGcmPayloadCodec(string encryptionKey, ILogger<AesGcmPayloadCodec> logger)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("Encryption key is required");

        // Key must be 32 bytes for AES-256
        _encryptionKey = Encoding.UTF8.GetBytes(encryptionKey.PadRight(32).Substring(0, 32));
        _logger = logger;
    }

    public async IAsyncEnumerable<Payload> EncodeAsync(IAsyncEnumerable<Payload> payloads)
    {
        await foreach (var payload in payloads)
        {
            if (payload.Data.IsEmpty)
            {
                yield return payload;
                continue;
            }

            try
            {
                var encrypted = EncryptPayload(payload.Data.ToByteArray());
                var newPayload = new Payload
                {
                    Metadata = { { "encoding", new BytesValue { Value = Google.Protobuf.ByteString.CopyFromUtf8(EncodingType) } } }
                };
                newPayload.Data = Google.Protobuf.ByteString.CopyFrom(encrypted);

                yield return newPayload;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt payload");
                throw;
            }
        }
    }

    public async IAsyncEnumerable<Payload> DecodeAsync(IAsyncEnumerable<Payload> payloads)
    {
        await foreach (var payload in payloads)
        {
            if (!payload.Metadata.TryGetValue("encoding", out var encodingValue) ||
                encodingValue.Value.ToStringUtf8() != EncodingType)
            {
                yield return payload;
                continue;
            }

            try
            {
                var decrypted = DecryptPayload(payload.Data.ToByteArray());
                var newPayload = new Payload();
                newPayload.Data = Google.Protobuf.ByteString.CopyFrom(decrypted);

                yield return newPayload;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt payload");
                throw;
            }
        }
    }

    private byte[] EncryptPayload(byte[] data)
    {
        using (var aes = new AesGcm(_encryptionKey))
        {
            byte[] nonce = new byte[12]; // 96 bits
            byte[] tag = new byte[16];   // 128 bits

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce);
            }

            byte[] ciphertext = new byte[data.Length];
            aes.Encrypt(nonce, data, null, ciphertext, tag);

            // Combine: nonce + tag + ciphertext
            var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

            return result;
        }
    }

    private byte[] DecryptPayload(byte[] encrypted)
    {
        using (var aes = new AesGcm(_encryptionKey))
        {
            const int nonceLength = 12;
            const int tagLength = 16;

            byte[] nonce = new byte[nonceLength];
            byte[] tag = new byte[tagLength];
            byte[] ciphertext = new byte[encrypted.Length - nonceLength - tagLength];

            Buffer.BlockCopy(encrypted, 0, nonce, 0, nonceLength);
            Buffer.BlockCopy(encrypted, nonceLength, tag, 0, tagLength);
            Buffer.BlockCopy(encrypted, nonceLength + tagLength, ciphertext, 0, ciphertext.Length);

            byte[] plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }
    }
}
```

---

## Part 6: Error Handling & Retries

### Activity Error Handling

```csharp
namespace Oms.Temporal.Activities;

using Temporalio.Exceptions;
using Microsoft.Extensions.Logging;

/// <summary>Error handling patterns for activities</summary>
public abstract class BaseActivityWithErrorHandling : IBaseActivity
{
    protected readonly ILogger Logger;

    protected BaseActivityWithErrorHandling(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>Handle activity failure with appropriate exception type</summary>
    protected void HandleActivityFailure(Exception ex, string activityName)
    {
        Logger.LogError(ex, "Activity {ActivityName} failed", activityName);

        // Determine if error is retryable
        bool isRetryable = IsRetryableError(ex);

        if (isRetryable)
        {
            // Temporal will retry based on retry policy
            throw new ApplicationException($"{activityName} failed temporarily", ex);
        }
        else
        {
            // Non-retryable error - fail immediately
            throw new InvalidOperationException($"{activityName} failed permanently", ex);
        }
    }

    private bool IsRetryableError(Exception ex)
    {
        // Retryable: network, timeout, transient service errors
        if (ex is HttpRequestException || ex is TimeoutException)
            return true;

        // Non-retryable: validation errors, not found, access denied
        if (ex is ArgumentException || ex is InvalidOperationException)
            return false;

        return true; // Default to retryable for safety
    }
}
```

### Workflow Error Handling

```csharp
namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;
using Temporalio.Exceptions;

public partial class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    /// <summary>Execute activity with standardized error handling</summary>
    private async Task<T> ExecuteActivityAsync<T>(
        Func<Task<T>> activityFunc,
        string activityName,
        int maxRetries = 3)
    {
        try
        {
            return await activityFunc();
        }
        catch (ActivityFailureException ex)
        {
            _logger.LogError(ex, "Activity {ActivityName} failed after retries", activityName);

            // Set error status and fail workflow
            _context.CurrentStatus = "ProcessingError";
            throw;
        }
        catch (ApplicationException ex)
        {
            // Retryable error - could implement custom retry logic
            _logger.LogWarning(ex, "Retryable error in {ActivityName}", activityName);
            throw;
        }
    }

    /// <summary>Graceful cancellation handling</summary>
    private async Task<T> ExecuteWithCancellationCheckAsync<T>(
        Func<Task<T>> activityFunc,
        Channel<CancelOrderSignal> cancelChannel)
    {
        var resultTask = activityFunc();
        var cancelTask = cancelChannel.Reader.ReadAsync().AsTask();

        var completedTask = await Task.WhenAny(resultTask, cancelTask);

        if (completedTask == cancelTask)
        {
            // Cancellation requested
            _context.IsCancelled = true;
            _context.CurrentStatus = "Cancelled";
            throw new OperationCanceledException();
        }

        return await resultTask;
    }
}
```

---

## Part 7: Activity Retry & Timeout Policies

### Standardized Retry Policies

```csharp
namespace Oms.Temporal.Configuration;

using Temporalio.Client;

/// <summary>Activity retry and timeout policies by category</summary>
public static class ActivityPolicies
{
    /// <summary>Fast external API calls - 30s timeout, 3 retries</summary>
    public static ActivityOptions FastApiPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(30),
        StartToCloseTimeout = TimeSpan.FromSeconds(25),
        HeartbeatTimeout = TimeSpan.FromSeconds(10),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffCoefficient = 2.0,
            MaximumInterval = TimeSpan.FromSeconds(30),
            MaximumAttempts = 3
        }
    };

    /// <summary>Slow API calls - 60s timeout, 3 retries</summary>
    public static ActivityOptions SlowApiPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(60),
        StartToCloseTimeout = TimeSpan.FromSeconds(55),
        HeartbeatTimeout = TimeSpan.FromSeconds(20),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffCoefficient = 2.0,
            MaximumInterval = TimeSpan.FromSeconds(60),
            MaximumAttempts = 3
        }
    };

    /// <summary>Message publishing - 30s timeout, 5 retries (idempotent)</summary>
    public static ActivityOptions PublishingPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(30),
        StartToCloseTimeout = TimeSpan.FromSeconds(25),
        HeartbeatTimeout = TimeSpan.FromSeconds(5),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 1.0,
            MaximumInterval = TimeSpan.FromSeconds(5),
            MaximumAttempts = 5
        }
    };

    /// <summary>Long-running operations - 90s timeout, 2 retries</summary>
    public static ActivityOptions LongRunningPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(90),
        StartToCloseTimeout = TimeSpan.FromSeconds(85),
        HeartbeatTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(5),
            BackoffCoefficient = 2.0,
            MaximumInterval = TimeSpan.FromSeconds(60),
            MaximumAttempts = 2
        }
    };

    /// <summary>Approval workflows - no timeout (heartbeat only)</summary>
    public static ActivityOptions ApprovalPolicy => new()
    {
        ScheduleToCloseTimeout = null, // No timeout - waits for human
        StartToCloseTimeout = null,    // No timeout
        HeartbeatTimeout = TimeSpan.FromMinutes(5), // Keep alive
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumAttempts = 1 // No auto-retry for approvals
        }
    };
}
```

---

## Part 8: Workflow Versioning

### Workflow Versioning Pattern

```csharp
namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;

public partial class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    private const int WorkflowVersion = 1;

    /// <summary>
    /// Workflow version gate for backward compatibility.
    /// Allows deploying new code while old workflows finish with old behavior.
    /// </summary>
    [WorkflowInit]
    public void Init()
    {
        // Use GetVersion for breaking changes
        // If version >= 2, use new logic; otherwise use v1 behavior
        int codeVersion = Workflow.GetVersion(
            changeId: "OrderProcessing.V2",
            minSupported: 1,
            maxSupported: 1);

        // This allows graceful migration to v2 in future
    }

    /// <summary>
    /// Example: Adding new activity between existing ones
    /// Existing workflows will skip with no issues.
    /// </summary>
    private async Task ExecuteNewActivityIfSupported(ProcessOrderInput input)
    {
        int versionSupportingNewActivity = Workflow.GetVersion(
            changeId: "AddEnhancedValidationActivity",
            minSupported: 1,
            maxSupported: 1);

        if (versionSupportingNewActivity >= 1)
        {
            // Execute new activity (will be skipped in replay)
            // This is safe pattern for backward compatibility
        }
    }
}
```

---

## Summary Table

| Component | Location | Purpose |
|-----------|----------|---------|
| **Workflow Interface** | `Oms.Temporal.Workflows.IOrderProcessingWorkflow` | Main orchestration with signals & queries |
| **Activity Interfaces** | `Oms.Temporal.Activities.I*Activity` | 7 activities with specs |
| **Signal Handlers** | `OrderProcessingWorkflow.HandleXxxSignalAsync` | Dynamic control (3 signals) |
| **Query Handlers** | `OrderProcessingWorkflow.GetXxxAsync` | State inspection (4 queries) |
| **Worker Setup** | `Oms.Worker.TemporalWorkerHostedService` | Lifecycle management |
| **Task Queues** | `Oms.Temporal.Configuration.TaskQueues` | 4 queues for isolation |
| **Payload Codec** | `Oms.Temporal.Codec.AesGcmPayloadCodec` | AES-256-GCM encryption |
| **Error Handling** | Activity/Workflow error handlers | Retry policies & failures |
| **Retry Policies** | `Oms.Temporal.Configuration.ActivityPolicies` | 5 policy templates |

---

## Next Implementation Steps

### Phase 1: Workflow Implementation
1. Implement `OrderProcessingWorkflow` with activity calls
2. Implement signal handlers (cancel, correction, approval)
3. Implement query handlers
4. Test workflow replay

### Phase 2: Activity Implementation
1. Implement each activity with external service calls
2. Add proper error handling and retries
3. Implement idempotency patterns
4. Add activity testing

### Phase 3: Worker Deployment
1. Configure Temporal cluster connection
2. Deploy worker with docker-compose
3. Setup health checks
4. Monitor worker performance

### Phase 4: Integration Testing
1. Test full workflow end-to-end
2. Test signal handling
3. Test query responses
4. Test failure scenarios

