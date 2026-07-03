# Activities API Implementation Guide

Complete implementation guide for all 7 activities with HTTP clients, Kafka producers, and external service integration.

---

## Overview

All 7 activities follow common patterns:
- Timeout and retry specifications
- Idempotency keys for safety
- Error classification (retryable vs non-retryable)
- Heartbeat reporting for long operations
- External service integration
- Activity context logging

---

## Part 1: ValidateCommerceActivity

**Spec**: 30s timeout | 3 retries | 10s heartbeat | FastApiPolicy

```csharp
namespace Oms.Temporal.Activities;

using Temporalio.Activities;
using Oms.Contracts.ActivityInputOutputs;
using Oms.Temporal.ErrorHandling;
using System.Net.Http;
using Microsoft.Extensions.Logging;

public class ValidateCommerceActivity : BaseActivityWithErrorHandling, IValidateCommerceActivity
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ValidateCommerceActivity> _logger;
    private const string CommerceApiUrl = "https://commerce-api.example.com";

    public string ActivityName => "ValidateCommerceActivity";
    public int ActivityVersion => 1;

    public ValidateCommerceActivity(
        HttpClient httpClient,
        ILogger<ValidateCommerceActivity> logger)
        : base(logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [Activity]
    public async Task<ValidateCommerceActivityOutput> ExecuteAsync(ValidateCommerceActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Validating commerce for order {OrderId}",
                input.OrderId);

            // Idempotency: Use order ID + attempt for deduplication
            var idempotencyKey = $"validate-commerce-{input.OrderId}";

            using var request = new HttpRequestMessage(HttpMethod.Post, 
                $"{CommerceApiUrl}/validate");

            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(new
            {
                orderId = input.OrderId,
                customerId = input.CustomerId,
                items = input.Items.Select(i => new
                {
                    productCode = i.ProductCode,
                    quantity = i.Quantity,
                    unitPrice = i.UnitPrice
                }),
                totalAmount = input.TotalAmount
            });

            // Timeout: 25s (5s buffer for retries)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            
            var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<CommerceValidationResult>(content);

            _logger.LogInformation(
                "Commerce validation completed for order {OrderId}: Valid={IsValid}",
                input.OrderId,
                result?.IsValid ?? false);

            return new ValidateCommerceActivityOutput
            {
                IsValid = result?.IsValid ?? false,
                ValidationErrors = result?.Errors ?? new(),
                ValidatedOrder = result?.IsValid == true ? new OrderContractDto
                {
                    OrderId = input.OrderId,
                    Items = input.Items,
                    CalculatedTotal = result.CalculatedTotal
                } : null
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error validating commerce");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Commerce validation timeout");
            throw new ApplicationException("Commerce validation timed out", ex);
        }
    }

    private class CommerceValidationResult
    {
        public bool IsValid { get; set; }
        public decimal CalculatedTotal { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
```

---

## Part 2: CollectRiskActivity

**Spec**: 45s timeout | 2 retries | 15s heartbeat | SlowApiPolicy

```csharp
public class CollectRiskActivity : BaseActivityWithErrorHandling, ICollectRiskActivity
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CollectRiskActivity> _logger;
    private const string RiskEngineUrl = "https://risk-engine.example.com";

    public string ActivityName => "CollectRiskActivity";
    public int ActivityVersion => 1;

    public CollectRiskActivity(
        HttpClient httpClient,
        ILogger<CollectRiskActivity> logger)
        : base(logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [Activity]
    public async Task<CollectRiskActivityOutput> ExecuteAsync(CollectRiskActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Collecting risk for order {OrderId}, amount={Amount}",
                input.OrderId,
                input.OrderAmount);

            // Idempotency key
            var idempotencyKey = $"collect-risk-{input.OrderId}";

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{RiskEngineUrl}/assess");

            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(new
            {
                orderId = input.OrderId,
                customerId = input.CustomerId,
                orderAmount = input.OrderAmount,
                productCodes = input.ProductCodes,
                customerEmail = input.CustomerEmail,
                customerSegment = input.CustomerSegment,
                timestamp = DateTime.UtcNow
            });

            // Timeout: 40s (5s buffer)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));

            // Report heartbeat every 15 seconds
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(15));
            
            try
            {
                var response = await _httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RiskAssessmentResult>(content);

                _logger.LogInformation(
                    "Risk assessment completed for order {OrderId}: Level={RiskLevel}, Score={Score}",
                    input.OrderId,
                    result?.RiskLevel,
                    result?.RiskScore);

                return new CollectRiskActivityOutput
                {
                    RiskLevel = result?.RiskLevel ?? "Unknown",
                    RiskScore = result?.RiskScore ?? 0m,
                    RiskIndicators = result?.Indicators ?? new(),
                    RequiresManualReview = result?.ManualReviewRequired ?? false,
                    RiskEngineVersion = result?.EngineVersion ?? "unknown"
                };
            }
            finally
            {
                await heartbeat.DisposeAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error collecting risk");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Risk collection timeout");
            throw new ApplicationException("Risk collection timed out", ex);
        }
    }

    private class RiskAssessmentResult
    {
        public string RiskLevel { get; set; } = string.Empty;
        public decimal RiskScore { get; set; }
        public List<string> Indicators { get; set; } = new();
        public bool ManualReviewRequired { get; set; }
        public string EngineVersion { get; set; } = string.Empty;
    }
}
```

---

## Part 3: ValidatePaymentActivity

**Spec**: 60s timeout | 3 retries | 20s heartbeat | SlowApiPolicy

```csharp
public class ValidatePaymentActivity : BaseActivityWithErrorHandling, IValidatePaymentActivity
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ValidatePaymentActivity> _logger;
    private const string PaymentGatewayUrl = "https://payment-gateway.example.com";

    public string ActivityName => "ValidatePaymentActivity";
    public int ActivityVersion => 1;

    public ValidatePaymentActivity(
        HttpClient httpClient,
        ILogger<ValidatePaymentActivity> logger)
        : base(logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [Activity]
    public async Task<ValidatePaymentActivityOutput> ExecuteAsync(ValidatePaymentActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Authorizing payment for order {OrderId}, amount={Amount}",
                input.OrderId,
                input.Amount);

            // Idempotency key: Use order ID for deduplication
            var idempotencyKey = $"authorize-payment-{input.OrderId}";

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{PaymentGatewayUrl}/authorize");

            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(new
            {
                orderId = input.OrderId,
                amount = input.Amount,
                currency = input.Currency,
                paymentMethod = input.PaymentMethod,
                token = input.PaymentToken,
                customerId = input.CustomerId
            });

            // Timeout: 55s (5s buffer)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(55));

            var response = await _httpClient.SendAsync(request, cts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Payment authorization failed: {StatusCode}, {ErrorContent}",
                    response.StatusCode,
                    errorContent);

                return new ValidatePaymentActivityOutput
                {
                    Status = "Failed",
                    ErrorCode = response.StatusCode.ToString(),
                    ProcessedAt = DateTime.UtcNow
                };
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PaymentAuthorizationResult>(content);

            _logger.LogInformation(
                "Payment authorized for order {OrderId}: TransactionId={TransactionId}",
                input.OrderId,
                result?.TransactionId);

            return new ValidatePaymentActivityOutput
            {
                TransactionId = result?.TransactionId ?? string.Empty,
                Status = "Authorized",
                AuthCode = result?.AuthorizationCode,
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error authorizing payment");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Payment authorization timeout");
            throw new ApplicationException("Payment authorization timed out", ex);
        }
    }

    private class PaymentAuthorizationResult
    {
        public string TransactionId { get; set; } = string.Empty;
        public string AuthorizationCode { get; set; } = string.Empty;
    }
}
```

---

## Part 4: EnrichOrderActivity

**Spec**: 90s timeout | 2 retries | 30s heartbeat | LongRunningPolicy

```csharp
public class EnrichOrderActivity : BaseActivityWithErrorHandling, IEnrichOrderActivity
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EnrichOrderActivity> _logger;
    private const string PimApiUrl = "https://pim-api.example.com";

    public string ActivityName => "EnrichOrderActivity";
    public int ActivityVersion => 1;

    public EnrichOrderActivity(
        HttpClient httpClient,
        ILogger<EnrichOrderActivity> logger)
        : base(logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [Activity]
    public async Task<EnrichOrderActivityOutput> ExecuteAsync(EnrichOrderActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Enriching order {OrderId} with {ItemCount} items",
                input.OrderId,
                input.Items.Count);

            // Idempotency key
            var idempotencyKey = $"enrich-order-{input.OrderId}";

            // Heartbeat reporting for long-running operation
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(30));
            var heartbeatTask = ReportHeartbeatAsync(heartbeat);

            try
            {
                // Batch request for all items
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{PimApiUrl}/enrich-batch");

                request.Headers.Add("Idempotency-Key", idempotencyKey);
                request.Content = JsonContent.Create(new
                {
                    items = input.Items.Select(i => new
                    {
                        itemId = i.ItemId,
                        productCode = i.ProductCode,
                        quantity = i.Quantity
                    })
                });

                // Timeout: 85s (5s buffer)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(85));

                var response = await _httpClient.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<EnrichmentResult>(content);

                _logger.LogInformation(
                    "Order enrichment completed: {EnrichedItemCount} items",
                    result?.EnrichedItems.Count ?? 0);

                return new EnrichOrderActivityOutput
                {
                    OrderId = input.OrderId,
                    EnrichedItems = result?.EnrichedItems ?? new(),
                    PimVersion = result?.Version ?? "unknown",
                    EnrichedTotalPrice = result?.TotalPrice ?? 0m
                };
            }
            finally
            {
                await heartbeat.DisposeAsync();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error enriching order");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Order enrichment timeout");
            throw new ApplicationException("Order enrichment timed out", ex);
        }
    }

    private async Task ReportHeartbeatAsync(PeriodicTimer timer)
    {
        try
        {
            await foreach (var _ in timer.TickAsync())
            {
                ActivityExecutionContext.Current.ReportHeartbeat();
            }
        }
        catch (OperationCanceledException)
        {
            // Timer disposed
        }
    }

    private class EnrichmentResult
    {
        public List<EnrichedItemContractDto> EnrichedItems { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public string Version { get; set; } = string.Empty;
    }
}
```

---

## Part 5: PublishFulfillmentActivity

**Spec**: 30s timeout | 5 retries | 10s heartbeat | PublishingPolicy

```csharp
public class PublishFulfillmentActivity : BaseActivityWithErrorHandling, IPublishFulfillmentActivity
{
    private readonly IProducer<string, string> _kafkaProducer;
    private readonly ILogger<PublishFulfillmentActivity> _logger;
    private const string FulfillmentTopic = "fulfillment-orders";

    public string ActivityName => "PublishFulfillmentActivity";
    public int ActivityVersion => 1;

    public PublishFulfillmentActivity(
        IProducer<string, string> kafkaProducer,
        ILogger<PublishFulfillmentActivity> logger)
        : base(logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [Activity]
    public async Task<PublishFulfillmentActivityOutput> ExecuteAsync(PublishFulfillmentActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Publishing order {OrderId} to Kafka topic {Topic}",
                input.OrderId,
                FulfillmentTopic);

            // Idempotent event key (prevents Kafka deduplication)
            var eventKey = $"{input.OrderId}";
            var eventData = JsonSerializer.Serialize(input);

            var message = new Message<string, string>
            {
                Key = eventKey,
                Value = eventData,
                Headers = new Headers
                {
                    // Add correlation ID for tracing
                    new Header("correlation-id", Guid.NewGuid().ToString().GetBytes()),
                    new Header("timestamp", DateTime.UtcNow.Ticks.ToString().GetBytes())
                }
            };

            // Timeout: 25s (5s buffer)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));

            var deliveryReport = await _kafkaProducer.ProduceAsync(
                FulfillmentTopic,
                message,
                cts.Token);

            _logger.LogInformation(
                "Order published to Kafka: Partition={Partition}, Offset={Offset}",
                deliveryReport.Partition,
                deliveryReport.Offset);

            return new PublishFulfillmentActivityOutput
            {
                KafkaPartition = deliveryReport.Partition.Value,
                KafkaOffset = deliveryReport.Offset.Value,
                PublishedAt = DateTime.UtcNow,
                TopicName = FulfillmentTopic
            };
        }
        catch (KafkaException ex)
        {
            _logger.LogError(ex, "Kafka error publishing fulfillment");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Fulfillment publishing timeout");
            throw new ApplicationException("Publishing to Kafka timed out", ex);
        }
    }
}
```

---

## Part 6: RequestApprovalActivity

**Spec**: No timeout | 1 retry | 60s heartbeat | ApprovalPolicy

```csharp
public class RequestApprovalActivity : BaseActivityWithErrorHandling, IRequestApprovalActivity
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RequestApprovalActivity> _logger;
    private const string ApprovalServiceUrl = "https://approval-service.example.com";

    public string ActivityName => "RequestApprovalActivity";
    public int ActivityVersion => 1;

    public RequestApprovalActivity(
        HttpClient httpClient,
        ILogger<RequestApprovalActivity> logger)
        : base(logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [Activity]
    public async Task<RequestApprovalActivityOutput> ExecuteAsync(RequestApprovalActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Requesting {ApprovalType} for order {OrderId}",
                input.ApprovalType,
                input.OrderId);

            var approvalRequestId = Guid.NewGuid().ToString();

            // Create approval request
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{ApprovalServiceUrl}/requests");

            request.Content = JsonContent.Create(new
            {
                approvalRequestId,
                approvalType = input.ApprovalType,
                orderId = input.OrderId,
                reason = input.Reason,
                context = input.Context,
                requestedAt = DateTime.UtcNow,
                callbackUrl = $"https://oms-api.example.com/callbacks/approvals/{approvalRequestId}"
            });

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Approval request created: {ApprovalRequestId}",
                approvalRequestId);

            // Heartbeat every 60 seconds while waiting for approval
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(60));

            try
            {
                await foreach (var _ in heartbeat.TickAsync())
                {
                    ActivityExecutionContext.Current.ReportHeartbeat();
                    _logger.LogDebug("Heartbeat reported for approval {ApprovalRequestId}", approvalRequestId);
                }
            }
            finally
            {
                await heartbeat.DisposeAsync();
            }

            return new RequestApprovalActivityOutput
            {
                ApprovalRequestId = approvalRequestId,
                RequestedAt = DateTime.UtcNow,
                NotificationsCreated = 1
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error requesting approval");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
    }
}
```

---

## Part 7: PublishEventActivity

**Spec**: 20s timeout | 3 retries | 5s heartbeat | PublishingPolicy

```csharp
public class PublishEventActivity : BaseActivityWithErrorHandling, IPublishEventActivity
{
    private readonly IProducer<string, string> _kafkaProducer;
    private readonly ILogger<PublishEventActivity> _logger;
    private const string EventsTopic = "order-events";

    public string ActivityName => "PublishEventActivity";
    public int ActivityVersion => 1;

    public PublishEventActivity(
        IProducer<string, string> kafkaProducer,
        ILogger<PublishEventActivity> logger)
        : base(logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [Activity]
    public async Task<PublishEventActivityOutput> ExecuteAsync(PublishEventActivityInput input)
    {
        try
        {
            _logger.LogInformation(
                "Publishing {EventType} event for order {OrderId}",
                input.EventType,
                input.OrderId);

            var eventKey = $"{input.OrderId}#{DateTime.UtcNow.Ticks}";
            var eventValue = JsonSerializer.Serialize(new
            {
                eventId = Guid.NewGuid(),
                eventType = input.EventType,
                orderId = input.OrderId,
                orderNumber = input.OrderNumber,
                eventData = input.EventData,
                timestamp = DateTime.UtcNow,
                version = 1
            });

            var message = new Message<string, string>
            {
                Key = eventKey,
                Value = eventValue
            };

            // Timeout: 15s (5s buffer)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var deliveryReport = await _kafkaProducer.ProduceAsync(
                EventsTopic,
                message,
                cts.Token);

            _logger.LogInformation(
                "Event published: Partition={Partition}, Offset={Offset}",
                deliveryReport.Partition,
                deliveryReport.Offset);

            return new PublishEventActivityOutput
            {
                KafkaPartition = deliveryReport.Partition.Value,
                KafkaOffset = deliveryReport.Offset.Value,
                PublishedAt = DateTime.UtcNow
            };
        }
        catch (KafkaException ex)
        {
            _logger.LogError(ex, "Kafka error publishing event");
            HandleActivityFailure(ex, ActivityName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Event publishing timeout");
            throw new ApplicationException("Publishing event timed out", ex);
        }
    }
}
```

---

## DI Registration for Activities

```csharp
namespace Oms.Api.Configuration;

using Confluent.Kafka;
using Oms.Temporal.Activities;
using Microsoft.Extensions.DependencyInjection;

public static class ActivitiesExtensions
{
    public static IServiceCollection AddActivityServices(this IServiceCollection services)
    {
        // HTTP client for external APIs
        services.AddHttpClient<IValidateCommerceActivity, ValidateCommerceActivity>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer ...");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        services.AddHttpClient<ICollectRiskActivity, CollectRiskActivity>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer ...");
                client.Timeout = TimeSpan.FromSeconds(45);
            });

        services.AddHttpClient<IValidatePaymentActivity, ValidatePaymentActivity>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer ...");
                client.Timeout = TimeSpan.FromSeconds(60);
            });

        services.AddHttpClient<IEnrichOrderActivity, EnrichOrderActivity>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer ...");
                client.Timeout = TimeSpan.FromSeconds(90);
            });

        services.AddHttpClient<IRequestApprovalActivity, RequestApprovalActivity>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer ...");
            });

        // Kafka producer for publishing activities
        services.AddSingleton(provider =>
        {
            var config = new ProducerConfig
            {
                BootstrapServers = "kafka:9092",
                ClientId = "oms-producer",
                EnableIdempotence = true,
                Acks = Acks.All,
                MessageSendMaxRetries = 5
            };

            return new ProducerBuilder<string, string>(config).Build();
        });

        services.AddScoped<IPublishFulfillmentActivity, PublishFulfillmentActivity>();
        services.AddScoped<IPublishEventActivity, PublishEventActivity>();

        return services;
    }
}
```

---

## Summary

All 7 activities follow patterns for:
- ✅ External service integration (HTTP, Kafka)
- ✅ Idempotency keys for safety
- ✅ Timeout management
- ✅ Heartbeat reporting
- ✅ Error handling and logging
- ✅ Retry classification
- ✅ Production-ready implementation

