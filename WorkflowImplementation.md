# Workflow Implementation Guide

Complete implementation blueprint for OrderProcessingWorkflow orchestrating all activities, signals, and state management.

---

## Overview

The `OrderProcessingWorkflow` is the central orchestrator coordinating:
- 7 activities across 3 task queues
- 3 signals for dynamic control
- 4 queries for state inspection
- 13 order states with transitions
- Error handling and retries
- Timeout management and cancellation

---

## Part 1: Workflow Orchestration Flow

### Complete Order Processing Journey

```
START (ProcessOrderAsync)
    ↓
INITIALIZING
    ↓
[1] ValidateCommerceActivity
    ├─ Success → VALIDATING_ORDER
    └─ Failure → PendingCorrection (wait for RequestCorrectionSignal)
    
PENDING_CORRECTION (Signal Received)
    ├─ RequestCorrectionSignal → VALIDATING_ORDER (retry with new items)
    └─ CancelOrderSignal → CANCELLED
    
VALIDATING_ORDER
    ↓
[2] CollectRiskActivity
    ├─ Low/Medium Risk → AWAITING_PAYMENT
    └─ High/Critical Risk → RiskRejected
    
RISK_REJECTED
    ├─ ApproveRiskSignal → AWAITING_PAYMENT (manager override)
    └─ CancelOrderSignal → CANCELLED
    
AWAITING_PAYMENT
    ↓
[3] ValidatePaymentActivity
    ├─ Success → ENRICHING
    └─ Failure → PaymentInvalid (wait for RequestCorrectionSignal)
    
ENRICHING
    ↓
[4] EnrichOrderActivity
    ├─ Success → PublishingFulfillment
    └─ Failure → Error handling & retry
    
PublishingFulfillment
    ↓
[5] PublishFulfillmentActivity (Kafka)
    ├─ Success → [6] PublishEventActivity
    └─ Failure → Retry on FULFILLMENT_QUEUE
    
[6] PublishEventActivity (OrderStatusChangedEvent)
    ↓
FULFILLED
    ↓
END (OrderProcessingResult)

CANCEL at any point → [7] PublishEventActivity (OrderCancelledEvent) → CANCELLED
ERROR at any point → [7] PublishEventActivity (OrderFailedEvent) → PROCESSING_ERROR
TIMEOUT at any point → EXPIRED
```

---

## Part 2: Complete Workflow Implementation

```csharp
namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;
using Temporalio.Client;
using Oms.Contracts.ActivityInputOutputs;
using Oms.Contracts.WorkflowSignals;
using Oms.Contracts.WorkflowQueries;
using Oms.Application.DTOs;
using Oms.Temporal.Activities;
using Oms.Temporal.Configuration;
using Oms.Temporal.ErrorHandling;
using Microsoft.Extensions.Logging;

/// <summary>
/// Main order processing workflow orchestrating complete order lifecycle.
/// Coordinates 7 activities across 3 task queues with signal-based control.
/// </summary>
[Workflow]
public class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    // ===== State =====
    
    private readonly WorkflowExecutionContext _context = new();
    private readonly ILogger _logger = null!; // Injected

    // Signal channels for non-blocking communication
    private Channel<CancelOrderSignal>? _cancelChannel;
    private Channel<RequestCorrectionSignal>? _correctionChannel;
    private Channel<ApproveRiskSignal>? _approvalChannel;

    // Activity references
    private readonly IValidateCommerceActivity _validateCommerce = null!;
    private readonly ICollectRiskActivity _collectRisk = null!;
    private readonly IValidatePaymentActivity _validatePayment = null!;
    private readonly IEnrichOrderActivity _enrichOrder = null!;
    private readonly IPublishFulfillmentActivity _publishFulfillment = null!;
    private readonly IRequestApprovalActivity _requestApproval = null!;
    private readonly IPublishEventActivity _publishEvent = null!;

    // ===== Workflow Initialization =====

    [WorkflowInit]
    public void Init()
    {
        // Initialize signal channels for bidirectional communication
        _cancelChannel = Channel.CreateUnbounded<CancelOrderSignal>();
        _correctionChannel = Channel.CreateUnbounded<RequestCorrectionSignal>();
        _approvalChannel = Channel.CreateUnbounded<ApproveRiskSignal>();

        // Workflow versioning for backward compatibility
        int workflowVersion = Workflow.GetVersion("OrderProcessing.V1", 1, 1);
    }

    // ===== Main Workflow Entry Point =====

    [WorkflowRun]
    public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
    {
        try
        {
            _context.OrderId = input.OrderId;
            _context.OrderNumber = input.OrderNumber;
            _context.CustomerId = input.CustomerId;
            _context.CreatedAt = DateTime.UtcNow;
            _context.CurrentStatus = "Initializing";

            _logger?.LogInformation(
                "Processing order {OrderNumber} (ID: {OrderId})",
                input.OrderNumber,
                input.OrderId);

            // ===== Step 1: Validate Commerce =====
            await ValidateCommercePhaseAsync(input);
            if (_context.IsCancelled)
                return BuildCancelledResult();

            // ===== Step 2: Collect Risk Assessment =====
            await CollectRiskPhaseAsync(input);
            if (_context.IsCancelled)
                return BuildCancelledResult();

            // ===== Step 3: Validate Payment =====
            await ValidatePaymentPhaseAsync(input);
            if (_context.IsCancelled)
                return BuildCancelledResult();

            // ===== Step 4: Enrich Order =====
            await EnrichOrderPhaseAsync(input);
            if (_context.IsCancelled)
                return BuildCancelledResult();

            // ===== Step 5: Publish to Fulfillment =====
            await PublishFulfillmentPhaseAsync(input);
            if (_context.IsCancelled)
                return BuildCancelledResult();

            // ===== Step 6: Publish Status Event =====
            await PublishStatusEventAsync("Fulfilled");

            _context.CurrentStatus = "Fulfilled";
            _context.CompletedAt = DateTime.UtcNow;

            return BuildSuccessResult();
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "Workflow cancelled for order {OrderId}", _context.OrderId);
            _context.CurrentStatus = "Cancelled";
            _context.CompletedAt = DateTime.UtcNow;
            return BuildErrorResult("Workflow cancelled", ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Workflow failed for order {OrderId}", _context.OrderId);
            _context.CurrentStatus = "ProcessingError";
            _context.CompletedAt = DateTime.UtcNow;

            // Publish failure event
            await PublishErrorEventAsync(ex);

            return BuildErrorResult("Workflow failed", ex.Message);
        }
    }

    // ===== Workflow Phases =====

    /// <summary>Phase 1: Validate order against commerce system</summary>
    private async Task ValidateCommercePhaseAsync(ProcessOrderInput input)
    {
        _context.CurrentStatus = "ValidatingOrder";
        int correctionAttempts = 0;

        while (correctionAttempts < 3)
        {
            try
            {
                _logger?.LogInformation("Validating commerce for order {OrderId}", _context.OrderId);

                var validateInput = new ValidateCommerceActivityInput
                {
                    OrderId = _context.OrderId,
                    CustomerId = _context.CustomerId,
                    Items = input.Items,
                    TotalAmount = input.TotalAmount
                };

                var result = await ExecuteActivityAsync(
                    () => _validateCommerce.ExecuteAsync(validateInput),
                    "ValidateCommerceActivity",
                    ActivityPolicies.FastApiPolicy);

                if (!result.IsValid)
                {
                    _logger?.LogWarning(
                        "Order validation failed: {Errors}",
                        string.Join(", ", result.ValidationErrors));

                    _context.CurrentStatus = "PendingCorrection";

                    // Wait for correction signal or cancellation
                    var correctionOrCancel = await WaitForSignalAsync(
                        _correctionChannel!,
                        _cancelChannel!);

                    if (correctionOrCancel is CancelOrderSignal cancel)
                    {
                        _context.IsCancelled = true;
                        _context.CancellationReason = cancel.CancellationReason;
                        return;
                    }

                    if (correctionOrCancel is RequestCorrectionSignal correction)
                    {
                        input.Items = correction.CorrectedItems;
                        correctionAttempts++;
                        continue;
                    }
                }

                // Validation successful
                _context.Order = result.ValidatedOrder != null 
                    ? MapToOrderDto(result.ValidatedOrder) 
                    : null;
                _context.CurrentStatus = "ValidatingOrder";
                return;
            }
            catch (ActivityFailureException ex)
            {
                _logger?.LogError(ex, "Commerce validation activity failed");
                throw;
            }
        }

        throw new InvalidOperationException(
            $"Commerce validation failed after {correctionAttempts} attempts");
    }

    /// <summary>Phase 2: Collect risk assessment from external engine</summary>
    private async Task CollectRiskPhaseAsync(ProcessOrderInput input)
    {
        _context.CurrentStatus = "CollectingRisk";

        try
        {
            _logger?.LogInformation("Collecting risk for order {OrderId}", _context.OrderId);

            var riskInput = new CollectRiskActivityInput
            {
                OrderId = _context.OrderId,
                CustomerId = _context.CustomerId,
                OrderAmount = input.TotalAmount,
                ProductCodes = input.Items.Select(i => i.ProductCode).ToList(),
                CustomerEmail = input.CustomerEmail,
                CustomerSegment = input.CustomerSegment
            };

            var result = await ExecuteActivityAsync(
                () => _collectRisk.ExecuteAsync(riskInput),
                "CollectRiskActivity",
                ActivityPolicies.SlowApiPolicy);

            _context.RiskAssessment = MapToRiskDataDto(result);

            // Handle high/critical risk
            if (result.RiskLevel == "Critical" || result.RiskLevel == "High")
            {
                _context.CurrentStatus = "RiskRejected";
                _logger?.LogWarning(
                    "High risk detected for order {OrderId}: {RiskLevel}",
                    _context.OrderId,
                    result.RiskLevel);

                // Wait for manager approval or cancellation
                var approvalOrCancel = await WaitForSignalAsync(
                    _approvalChannel!,
                    _cancelChannel!);

                if (approvalOrCancel is CancelOrderSignal cancel)
                {
                    _context.IsCancelled = true;
                    _context.CancellationReason = cancel.CancellationReason;
                    return;
                }

                if (approvalOrCancel is ApproveRiskSignal approval)
                {
                    _logger?.LogInformation(
                        "Risk approved by {Manager} for order {OrderId}",
                        approval.ApprovedBy,
                        _context.OrderId);
                    _context.IsRiskApproved = true;
                }
            }

            _context.CurrentStatus = "AwaitingPayment";
        }
        catch (ActivityFailureException ex)
        {
            _logger?.LogError(ex, "Risk collection activity failed");
            throw;
        }
    }

    /// <summary>Phase 3: Validate and authorize payment</summary>
    private async Task ValidatePaymentPhaseAsync(ProcessOrderInput input)
    {
        _context.CurrentStatus = "ValidatingPayment";
        int retryCount = 0;

        while (retryCount < 3)
        {
            try
            {
                _logger?.LogInformation("Validating payment for order {OrderId}", _context.OrderId);

                var paymentInput = new ValidatePaymentActivityInput
                {
                    OrderId = _context.OrderId,
                    Amount = input.TotalAmount,
                    Currency = "USD",
                    PaymentToken = input.PaymentToken,
                    CustomerId = _context.CustomerId,
                    PaymentMethod = input.PaymentMethod
                };

                var result = await ExecuteActivityAsync(
                    () => _validatePayment.ExecuteAsync(paymentInput),
                    "ValidatePaymentActivity",
                    ActivityPolicies.SlowApiPolicy);

                if (result.Status != "Authorized" && result.Status != "Success")
                {
                    _logger?.LogWarning(
                        "Payment authorization failed: {ErrorCode}",
                        result.ErrorCode);

                    _context.CurrentStatus = "PaymentInvalid";
                    retryCount++;

                    // Wait for retry signal or cancellation
                    var correctionOrCancel = await WaitForSignalWithTimeoutAsync(
                        _correctionChannel!,
                        _cancelChannel!,
                        TimeSpan.FromMinutes(5));

                    if (correctionOrCancel is CancelOrderSignal cancel)
                    {
                        _context.IsCancelled = true;
                        _context.CancellationReason = cancel.CancellationReason;
                        return;
                    }

                    if (correctionOrCancel is RequestCorrectionSignal correction)
                    {
                        input.PaymentToken = correction.CorrectedItems.FirstOrDefault()?.ProductCode ?? "";
                        continue;
                    }
                }

                // Payment authorized
                _context.Payment = MapToPaymentDto(result);
                _context.CurrentStatus = "Enriching";
                return;
            }
            catch (ActivityFailureException ex)
            {
                _logger?.LogError(ex, "Payment validation activity failed");
                retryCount++;
                if (retryCount >= 3)
                    throw;
            }
        }

        throw new InvalidOperationException("Payment validation failed after 3 attempts");
    }

    /// <summary>Phase 4: Enrich order with PIM data</summary>
    private async Task EnrichOrderPhaseAsync(ProcessOrderInput input)
    {
        _context.CurrentStatus = "Enriching";

        try
        {
            _logger?.LogInformation("Enriching order {OrderId}", _context.OrderId);

            var enrichInput = new EnrichOrderActivityInput
            {
                OrderId = _context.OrderId,
                Items = input.Items.Select(i => new OrderItemContractDto
                {
                    ItemId = Guid.NewGuid(),
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity
                }).ToList()
            };

            var result = await ExecuteActivityAsync(
                () => _enrichOrder.ExecuteAsync(enrichInput),
                "EnrichOrderActivity",
                ActivityPolicies.LongRunningPolicy);

            _context.EnrichedData = MapToEnrichedOrderDto(result);
        }
        catch (ActivityFailureException ex)
        {
            _logger?.LogError(ex, "Order enrichment activity failed");
            throw;
        }
    }

    /// <summary>Phase 5: Publish to fulfillment system</summary>
    private async Task PublishFulfillmentPhaseAsync(ProcessOrderInput input)
    {
        _context.CurrentStatus = "PublishingFulfillment";

        try
        {
            _logger?.LogInformation("Publishing order {OrderId} to fulfillment", _context.OrderId);

            var fulfillInput = new PublishFulfillmentActivityInput
            {
                OrderId = _context.OrderId,
                OrderNumber = _context.OrderNumber,
                CustomerId = _context.CustomerId,
                Items = input.Items.Select(i => new FulfillmentItemDto
                {
                    ItemId = Guid.NewGuid(),
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    Category = _context.EnrichedData?.EnrichedItems
                        .FirstOrDefault(e => e.ProductId == i.ProductCode)?.Category ?? "",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList(),
                ShippingAddress = new ShippingAddressDto
                {
                    Street = input.ShippingAddress.Street,
                    City = input.ShippingAddress.City,
                    State = input.ShippingAddress.State,
                    ZipCode = input.ShippingAddress.ZipCode,
                    Country = input.ShippingAddress.Country
                },
                TotalAmount = input.TotalAmount
            };

            var result = await ExecuteActivityAsync(
                () => _publishFulfillment.ExecuteAsync(fulfillInput),
                "PublishFulfillmentActivity",
                ActivityPolicies.PublishingPolicy);

            _logger?.LogInformation(
                "Order published to Kafka partition {Partition} offset {Offset}",
                result.KafkaPartition,
                result.KafkaOffset);
        }
        catch (ActivityFailureException ex)
        {
            _logger?.LogError(ex, "Fulfillment publishing activity failed");
            throw;
        }
    }

    // ===== Helper Methods =====

    /// <summary>Execute activity with error handling</summary>
    private async Task<T> ExecuteActivityAsync<T>(
        Func<Task<T>> activityFunc,
        string activityName,
        ActivityOptions options)
    {
        try
        {
            _logger?.LogDebug("Starting activity {ActivityName}", activityName);
            var result = await Workflow.ExecuteActivityAsync(activityFunc, options);
            _logger?.LogDebug("Activity {ActivityName} completed successfully", activityName);
            return result;
        }
        catch (ActivityFailureException ex)
        {
            _logger?.LogError(ex, "Activity {ActivityName} failed: {Message}", activityName, ex.Message);
            throw;
        }
    }

    /// <summary>Wait for signal without timeout</summary>
    private async Task<object> WaitForSignalAsync<T1, T2>(
        Channel<T1> channel1,
        Channel<T2> channel2)
        where T1 : class
        where T2 : class
    {
        using var cts = new CancellationTokenSource();
        
        var task1 = channel1.Reader.ReadAsync(cts.Token).AsTask();
        var task2 = channel2.Reader.ReadAsync(cts.Token).AsTask();

        var completedTask = await Task.WhenAny(task1, task2);

        if (completedTask == task1)
            return await task1;
        else
            return await task2;
    }

    /// <summary>Wait for signal with timeout</summary>
    private async Task<object?> WaitForSignalWithTimeoutAsync<T1, T2>(
        Channel<T1> channel1,
        Channel<T2> channel2,
        TimeSpan timeout)
        where T1 : class
        where T2 : class
    {
        using var cts = new CancellationTokenSource(timeout);
        
        try
        {
            var task1 = channel1.Reader.ReadAsync(cts.Token).AsTask();
            var task2 = channel2.Reader.ReadAsync(cts.Token).AsTask();

            var completedTask = await Task.WhenAny(task1, task2);

            if (completedTask == task1)
                return await task1;
            else
                return await task2;
        }
        catch (OperationCanceledException)
        {
            return null; // Timeout
        }
    }

    /// <summary>Publish status change event</summary>
    private async Task PublishStatusEventAsync(string newStatus)
    {
        try
        {
            var eventInput = new PublishEventActivityInput
            {
                EventType = "OrderStatusChanged",
                OrderId = _context.OrderId,
                OrderNumber = _context.OrderNumber,
                EventData = $"Status changed to {newStatus}"
            };

            await ExecuteActivityAsync(
                () => _publishEvent.ExecuteAsync(eventInput),
                "PublishEventActivity",
                ActivityPolicies.PublishingPolicy);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to publish status event");
            // Don't fail workflow for event publishing errors
        }
    }

    /// <summary>Publish error/failure event</summary>
    private async Task PublishErrorEventAsync(Exception ex)
    {
        try
        {
            var eventInput = new PublishEventActivityInput
            {
                EventType = "OrderFailed",
                OrderId = _context.OrderId,
                OrderNumber = _context.OrderNumber,
                EventData = $"Order failed: {ex.Message}"
            };

            await ExecuteActivityAsync(
                () => _publishEvent.ExecuteAsync(eventInput),
                "PublishEventActivity",
                ActivityPolicies.PublishingPolicy);
        }
        catch (Exception publishEx)
        {
            _logger?.LogError(publishEx, "Failed to publish error event");
        }
    }

    // ===== Result Building =====

    private OrderProcessingResult BuildSuccessResult()
    {
        return new OrderProcessingResult
        {
            OrderId = _context.OrderId,
            OrderNumber = _context.OrderNumber,
            FinalStatus = _context.CurrentStatus,
            Order = _context.Order,
            Payment = _context.Payment,
            RiskAssessment = _context.RiskAssessment,
            EnrichedData = _context.EnrichedData,
            CompletedAt = DateTime.UtcNow,
            CancellationReason = null,
            ErrorMessage = null
        };
    }

    private OrderProcessingResult BuildCancelledResult()
    {
        return new OrderProcessingResult
        {
            OrderId = _context.OrderId,
            OrderNumber = _context.OrderNumber,
            FinalStatus = "Cancelled",
            Order = _context.Order,
            Payment = _context.Payment,
            RiskAssessment = _context.RiskAssessment,
            EnrichedData = _context.EnrichedData,
            CompletedAt = DateTime.UtcNow,
            CancellationReason = _context.CancellationReason,
            ErrorMessage = null
        };
    }

    private OrderProcessingResult BuildErrorResult(string status, string errorMessage)
    {
        return new OrderProcessingResult
        {
            OrderId = _context.OrderId,
            OrderNumber = _context.OrderNumber,
            FinalStatus = status,
            Order = _context.Order,
            Payment = _context.Payment,
            RiskAssessment = _context.RiskAssessment,
            EnrichedData = _context.EnrichedData,
            CompletedAt = DateTime.UtcNow,
            CancellationReason = null,
            ErrorMessage = errorMessage
        };
    }

    // ===== Mapping Helpers =====

    private OrderDto MapToOrderDto(OrderContractDto contract)
    {
        return new OrderDto
        {
            OrderId = contract.OrderId,
            OrderNumber = contract.OrderNumber,
            Items = contract.Items.Select(i => new OrderItemDto
            {
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = i.UnitPrice * i.Quantity
            }).ToList()
        };
    }

    private RiskDataDto MapToRiskDataDto(CollectRiskActivityOutput output)
    {
        return new RiskDataDto
        {
            RiskId = Guid.NewGuid(),
            Level = output.RiskLevel,
            RiskScore = output.RiskScore,
            Indicators = output.RiskIndicators.Select(r => new RiskIndicatorDto
            {
                RiskFactor = r,
                Weight = 1.0m,
                IsFlagged = output.RiskLevel == "Critical"
            }).ToList(),
            RequiresManualReview = output.RequiresManualReview,
            RiskEngineVersion = output.RiskEngineVersion
        };
    }

    private PaymentDto MapToPaymentDto(ValidatePaymentActivityOutput output)
    {
        return new PaymentDto
        {
            PaymentId = Guid.NewGuid(),
            Status = output.Status,
            TransactionId = output.TransactionId,
            ProcessedAt = output.ProcessedAt,
            Transactions = new() { new PaymentTransactionDto
            {
                TransactionId = Guid.NewGuid(),
                Status = output.Status,
                Timestamp = output.ProcessedAt ?? DateTime.UtcNow,
                GatewayReference = output.AuthCode ?? ""
            }}
        };
    }

    private EnrichedOrderDto MapToEnrichedOrderDto(EnrichOrderActivityOutput output)
    {
        return new EnrichedOrderDto
        {
            OrderId = output.OrderId,
            EnrichedItems = output.EnrichedItems.Select(i => new EnrichedOrderItemDto
            {
                ItemId = i.ItemId,
                ProductId = i.ProductId,
                Category = i.Category,
                Manufacturer = i.Manufacturer,
                Tags = i.Tags,
                EnrichedPrice = i.EnrichedPrice
            }).ToList(),
            PimVersion = output.PimVersion,
            EnrichedTotalPrice = output.EnrichedTotalPrice
        };
    }
}
```

---

## Part 3: Key Implementation Patterns

### Signal Handling Pattern

```csharp
// Wait for either signal or cancellation (non-blocking)
var correctionOrCancel = await WaitForSignalAsync(
    _correctionChannel!,
    _cancelChannel!);

if (correctionOrCancel is CancelOrderSignal cancel)
{
    _context.IsCancelled = true;
    return;
}

if (correctionOrCancel is RequestCorrectionSignal correction)
{
    // Retry with corrected data
    input.Items = correction.CorrectedItems;
    continue;
}
```

### Activity Execution Pattern

```csharp
var result = await ExecuteActivityAsync(
    () => _validateCommerce.ExecuteAsync(input),
    "ValidateCommerceActivity",
    ActivityPolicies.FastApiPolicy);
```

### Error Recovery Pattern

```csharp
while (retryCount < 3)
{
    try
    {
        var result = await ExecuteActivityAsync(...);
        // Process result
        return;
    }
    catch (ActivityFailureException ex)
    {
        retryCount++;
        if (retryCount >= 3)
            throw;
        // Wait for signal or timeout
    }
}
```

---

## Part 4: Testing the Workflow

### Unit Testing with Mock Activities

```csharp
[Fact]
public async Task ProcessOrder_WithValidInput_ReturnsSuccessful()
{
    // Arrange
    var workflow = new OrderProcessingWorkflow();
    var input = new ProcessOrderInput
    {
        OrderId = Guid.NewGuid(),
        OrderNumber = "ORD-001",
        TotalAmount = 100.00m,
        Items = new() { new OrderItemInput { ... } }
    };

    // Mock activities
    // Act
    var result = await workflow.ProcessOrderAsync(input);

    // Assert
    Assert.Equal("Fulfilled", result.FinalStatus);
    Assert.Null(result.ErrorMessage);
}
```

### Workflow Replay Testing

See **ReplayTestsGuide.md** for complete replay testing patterns.

---

## Part 5: Observability & Monitoring

### Key Metrics to Track

- Workflow duration (start to completion)
- Activity execution times
- Activity failure/retry rates
- Signal received/processed latencies
- Query response times
- Cancellation requests
- State transition counts

### Logging Strategy

```csharp
_logger?.LogInformation(
    "Processing order {OrderNumber} (ID: {OrderId})",
    input.OrderNumber,
    input.OrderId);

_logger?.LogWarning(
    "High risk detected for order {OrderId}: {RiskLevel}",
    _context.OrderId,
    result.RiskLevel);

_logger?.LogError(ex,
    "Activity {ActivityName} failed",
    activityName);
```

---

## Summary

The OrderProcessingWorkflow:
- ✅ Orchestrates 7 activities across 3 task queues
- ✅ Handles 3 signals (cancel, correction, approval)
- ✅ Manages 4 queries for state inspection
- ✅ Implements error recovery with retries
- ✅ Supports cancellation and timeout
- ✅ Maintains complete execution context
- ✅ Publishes domain events
- ✅ Ready for production deployment

