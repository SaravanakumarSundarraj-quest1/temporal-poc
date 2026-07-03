# Workflow Replay Tests Guide

Complete guide for Temporal workflow replay testing, history verification, and production validation.

---

## Overview

Replay testing verifies that:
- ✅ Code changes don't break historical workflows
- ✅ New workflow logic matches recorded history
- ✅ Versioning gates work correctly
- ✅ Activity mocking behaves like production
- ✅ Workflow logic determinism is maintained

---

## Part 1: Replay Testing Fundamentals

### What is Replay Testing?

```
Production Event History (JSON)
    ↓
Extract all decision points
    ↓
Load into test environment
    ↓
Run workflow logic with history
    ↓
Verify workflow reaches same outcome
    ↓
✅ PASS: Code change is safe
❌ FAIL: Code change breaks workflow
```

### Why Replay Testing Matters

| Scenario | Impact |
|----------|--------|
| Code change breaks conditional | Workflow halts mid-execution |
| Activity retry count changes | Workflow behavior differs |
| New code path not tested | Production errors |
| Versioning gates not used | Historical workflows fail |
| Non-deterministic code | Replay produces different result |

---

## Part 2: Complete Replay Test Setup

```csharp
namespace Oms.Temporal.Tests.ReplayTests;

using Temporalio.Testing;
using Temporalio.Workflows;
using Temporalio.Activities;
using Temporalio.Client;
using Oms.Temporal.Workflows;
using Oms.Temporal.Activities;
using Oms.Contracts.ActivityInputOutputs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Replay tests for OrderProcessingWorkflow using historical event histories.
/// Verifies that code changes don't break historical workflows.
/// </summary>
public class OrderProcessingWorkflowReplayTests
{
    private readonly IServiceProvider _serviceProvider;

    public OrderProcessingWorkflowReplayTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<OrderProcessingWorkflow>();
        _serviceProvider = services.BuildServiceProvider();
    }

    // ===== Test 1: Successful Order Processing Replay =====

    [Fact]
    public async Task ReplaySuccessfulOrder_CompletesWithFulfilledStatus()
    {
        // Arrange: Load event history from production
        var eventHistory = new WorkflowHistory
        {
            Events = new()
            {
                // Event 1: Workflow started
                WorkflowExecutionStartedEvent(
                    input: new ProcessOrderInput
                    {
                        OrderId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                        OrderNumber = "ORD-2024-001",
                        CustomerId = Guid.Parse("660e8400-e29b-41d4-a716-446655440002"),
                        Items = new()
                        {
                            new OrderItemInput
                            {
                                ProductCode = "PROD-001",
                                ProductName = "Widget",
                                UnitPrice = 29.99m,
                                Quantity = 2
                            }
                        },
                        TotalAmount = 59.98m
                    }),

                // Event 2: ValidateCommerceActivity scheduled
                ActivityTaskScheduledEvent(
                    activityId: "1",
                    activityType: "ValidateCommerceActivity"),

                // Event 3: ValidateCommerceActivity completed
                ActivityTaskCompletedEvent(
                    activityId: "1",
                    result: new ValidateCommerceActivityOutput
                    {
                        IsValid = true,
                        ValidationErrors = new(),
                        ValidatedOrder = new OrderContractDto
                        {
                            OrderId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                            Items = new() { /* items */ },
                            CalculatedTotal = 59.98m
                        }
                    }),

                // Event 4: CollectRiskActivity scheduled
                ActivityTaskScheduledEvent(
                    activityId: "2",
                    activityType: "CollectRiskActivity"),

                // Event 5: CollectRiskActivity completed
                ActivityTaskCompletedEvent(
                    activityId: "2",
                    result: new CollectRiskActivityOutput
                    {
                        RiskLevel = "Low",
                        RiskScore = 25m,
                        RiskIndicators = new() { "Good customer history" },
                        RequiresManualReview = false,
                        RiskEngineVersion = "3.2.1"
                    }),

                // Event 6: ValidatePaymentActivity scheduled
                ActivityTaskScheduledEvent(
                    activityId: "3",
                    activityType: "ValidatePaymentActivity"),

                // Event 7: ValidatePaymentActivity completed
                ActivityTaskCompletedEvent(
                    activityId: "3",
                    result: new ValidatePaymentActivityOutput
                    {
                        TransactionId = "TXN-2024-001",
                        Status = "Authorized",
                        AuthCode = "AUTH123456",
                        ProcessedAt = DateTime.UtcNow
                    }),

                // Event 8: EnrichOrderActivity scheduled
                ActivityTaskScheduledEvent(
                    activityId: "4",
                    activityType: "EnrichOrderActivity"),

                // Event 9: EnrichOrderActivity completed
                ActivityTaskCompletedEvent(
                    activityId: "4",
                    result: new EnrichOrderActivityOutput
                    {
                        OrderId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                        EnrichedItems = new() { /* enriched items */ },
                        PimVersion = "5.1.0",
                        EnrichedTotalPrice = 59.98m
                    }),

                // Event 10: PublishFulfillmentActivity scheduled
                ActivityTaskScheduledEvent(
                    activityId: "5",
                    activityType: "PublishFulfillmentActivity"),

                // Event 11: PublishFulfillmentActivity completed
                ActivityTaskCompletedEvent(
                    activityId: "5",
                    result: new PublishFulfillmentActivityOutput
                    {
                        KafkaPartition = 0,
                        KafkaOffset = 12345,
                        PublishedAt = DateTime.UtcNow,
                        TopicName = "fulfillment-orders"
                    }),

                // Event 12: PublishEventActivity scheduled
                ActivityTaskScheduledEvent(
                    activityId: "6",
                    activityType: "PublishEventActivity"),

                // Event 13: PublishEventActivity completed
                ActivityTaskCompletedEvent(
                    activityId: "6",
                    result: new PublishEventActivityOutput
                    {
                        KafkaPartition = 0,
                        KafkaOffset = 12346,
                        PublishedAt = DateTime.UtcNow
                    }),

                // Event 14: Workflow completed
                WorkflowExecutionCompletedEvent(
                    result: new OrderProcessingResult
                    {
                        OrderId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001"),
                        OrderNumber = "ORD-2024-001",
                        FinalStatus = "Fulfilled"
                    })
            }
        };

        // Act: Replay workflow with event history
        using var testServer = new WorkflowServiceWorkflowServerOptions().Build();
        using var client = new WorkflowClient(testServer);

        var replay = await client.ReplayWorkflowAsync(
            eventHistory,
            typeof(OrderProcessingWorkflow),
            _serviceProvider);

        // Assert: Workflow completes with expected status
        var result = replay.GetResult<OrderProcessingResult>();
        Assert.Equal("Fulfilled", result.FinalStatus);
        Assert.Equal("ORD-2024-001", result.OrderNumber);
        Assert.Null(result.ErrorMessage);
    }

    // ===== Test 2: Order Cancellation During Validation =====

    [Fact]
    public async Task ReplayOrderCancelledDuringValidation_CompletesWithCancelledStatus()
    {
        // Arrange: Event history with cancellation signal
        var eventHistory = new WorkflowHistory
        {
            Events = new()
            {
                WorkflowExecutionStartedEvent(
                    input: new ProcessOrderInput { /* input */ }),

                ActivityTaskScheduledEvent(activityId: "1", "ValidateCommerceActivity"),

                ActivityTaskCompletedEvent(
                    activityId: "1",
                    result: new ValidateCommerceActivityOutput { IsValid = true }),

                // SIGNAL: Cancel order received
                WorkflowSignalReceivedEvent(
                    signalName: "CancelOrderSignal",
                    signal: new CancelOrderSignal
                    {
                        CancellationReason = "Customer requested cancellation"
                    }),

                // Workflow handles signal and completes
                WorkflowExecutionCompletedEvent(
                    result: new OrderProcessingResult
                    {
                        FinalStatus = "Cancelled",
                        CancellationReason = "Customer requested cancellation"
                    })
            }
        };

        // Act
        using var testServer = new WorkflowServiceWorkflowServerOptions().Build();
        using var client = new WorkflowClient(testServer);

        var replay = await client.ReplayWorkflowAsync(
            eventHistory,
            typeof(OrderProcessingWorkflow),
            _serviceProvider);

        // Assert
        var result = replay.GetResult<OrderProcessingResult>();
        Assert.Equal("Cancelled", result.FinalStatus);
        Assert.Equal("Customer requested cancellation", result.CancellationReason);
    }

    // ===== Test 3: Order Correction During Validation =====

    [Fact]
    public async Task ReplayOrderCorrectionDuringValidation_RetryWithCorrectedItems()
    {
        // Arrange: Event history with correction signal
        var eventHistory = new WorkflowHistory
        {
            Events = new()
            {
                WorkflowExecutionStartedEvent(new ProcessOrderInput { /* ... */ }),

                ActivityTaskScheduledEvent(activityId: "1", "ValidateCommerceActivity"),

                // First validation fails
                ActivityTaskCompletedEvent(
                    activityId: "1",
                    result: new ValidateCommerceActivityOutput
                    {
                        IsValid = false,
                        ValidationErrors = new() { "Invalid product code" }
                    }),

                // SIGNAL: Correction requested
                WorkflowSignalReceivedEvent(
                    signalName: "RequestCorrectionSignal",
                    signal: new RequestCorrectionSignal
                    {
                        CorrectedItems = new() { /* corrected items */ }
                    }),

                // Retry validation with corrected items
                ActivityTaskScheduledEvent(activityId: "1", "ValidateCommerceActivity"),

                // Second validation succeeds
                ActivityTaskCompletedEvent(
                    activityId: "1",
                    result: new ValidateCommerceActivityOutput
                    {
                        IsValid = true,
                        ValidatedOrder = new OrderContractDto { /* ... */ }
                    }),

                // ... rest of successful flow
                WorkflowExecutionCompletedEvent(
                    result: new OrderProcessingResult
                    {
                        FinalStatus = "Fulfilled"
                    })
            }
        };

        // Act & Assert
        var replay = await ReplayWorkflowAsync(eventHistory);
        var result = replay.GetResult<OrderProcessingResult>();
        Assert.Equal("Fulfilled", result.FinalStatus);
    }

    // ===== Test 4: Risk Approval Signal =====

    [Fact]
    public async Task ReplayHighRiskOrderWithApprovalSignal_ContinuesToPayment()
    {
        // Arrange
        var eventHistory = new WorkflowHistory
        {
            Events = new()
            {
                WorkflowExecutionStartedEvent(new ProcessOrderInput { /* ... */ }),

                ActivityTaskScheduledEvent(activityId: "1", "ValidateCommerceActivity"),
                ActivityTaskCompletedEvent(activityId: "1", new ValidateCommerceActivityOutput
                {
                    IsValid = true
                }),

                ActivityTaskScheduledEvent(activityId: "2", "CollectRiskActivity"),

                // High risk detected
                ActivityTaskCompletedEvent(
                    activityId: "2",
                    result: new CollectRiskActivityOutput
                    {
                        RiskLevel = "Critical",
                        RiskScore = 95m,
                        RequiresManualReview = true
                    }),

                // SIGNAL: Manager approves high risk
                WorkflowSignalReceivedEvent(
                    signalName: "ApproveRiskSignal",
                    signal: new ApproveRiskSignal
                    {
                        ApprovedBy = "manager@example.com",
                        ApprovalReason = "Approved for VIP customer"
                    }),

                // Continue to payment
                ActivityTaskScheduledEvent(activityId: "3", "ValidatePaymentActivity"),
                ActivityTaskCompletedEvent(activityId: "3", new ValidatePaymentActivityOutput
                {
                    Status = "Authorized",
                    TransactionId = "TXN-001"
                }),

                // ... continue to fulfillment
                WorkflowExecutionCompletedEvent(
                    result: new OrderProcessingResult
                    {
                        FinalStatus = "Fulfilled"
                    })
            }
        };

        // Act & Assert
        var replay = await ReplayWorkflowAsync(eventHistory);
        var result = replay.GetResult<OrderProcessingResult>();
        Assert.Equal("Fulfilled", result.FinalStatus);
    }

    // ===== Test 5: Activity Retry =====

    [Fact]
    public async Task ReplayPaymentValidationWithRetry_EventuallySucceeds()
    {
        // Arrange: Event history showing activity retry
        var eventHistory = new WorkflowHistory
        {
            Events = new()
            {
                WorkflowExecutionStartedEvent(new ProcessOrderInput { /* ... */ }),

                ActivityTaskScheduledEvent(activityId: "1", "ValidateCommerceActivity"),
                ActivityTaskCompletedEvent(activityId: "1", new ValidateCommerceActivityOutput { IsValid = true }),

                ActivityTaskScheduledEvent(activityId: "2", "CollectRiskActivity"),
                ActivityTaskCompletedEvent(activityId: "2", new CollectRiskActivityOutput { RiskLevel = "Low" }),

                // Payment validation attempt 1: FAILED
                ActivityTaskScheduledEvent(activityId: "3", "ValidatePaymentActivity"),
                ActivityTaskFailedEvent(
                    activityId: "3",
                    reason: "PaymentGatewayException",
                    details: "Connection timeout"),

                // Retry payment validation attempt 2: FAILED
                ActivityTaskScheduledEvent(activityId: "3", "ValidatePaymentActivity"),
                ActivityTaskFailedEvent(
                    activityId: "3",
                    reason: "PaymentGatewayException",
                    details: "Connection timeout"),

                // Retry payment validation attempt 3: SUCCESS
                ActivityTaskScheduledEvent(activityId: "3", "ValidatePaymentActivity"),
                ActivityTaskCompletedEvent(
                    activityId: "3",
                    result: new ValidatePaymentActivityOutput
                    {
                        Status = "Authorized",
                        TransactionId = "TXN-001"
                    }),

                // Continue normally
                ActivityTaskScheduledEvent(activityId: "4", "EnrichOrderActivity"),
                ActivityTaskCompletedEvent(activityId: "4", new EnrichOrderActivityOutput { /* ... */ }),

                ActivityTaskScheduledEvent(activityId: "5", "PublishFulfillmentActivity"),
                ActivityTaskCompletedEvent(activityId: "5", new PublishFulfillmentActivityOutput { /* ... */ }),

                WorkflowExecutionCompletedEvent(
                    result: new OrderProcessingResult
                    {
                        FinalStatus = "Fulfilled"
                    })
            }
        };

        // Act & Assert
        var replay = await ReplayWorkflowAsync(eventHistory);
        var result = replay.GetResult<OrderProcessingResult>();
        Assert.Equal("Fulfilled", result.FinalStatus);
    }

    // ===== Test 6: Query During Execution =====

    [Fact]
    public async Task ReplayWorkflowWithQueries_ReturnsCurrentState()
    {
        // Arrange
        var eventHistory = new WorkflowHistory
        {
            Events = new()
            {
                WorkflowExecutionStartedEvent(new ProcessOrderInput { /* ... */ }),

                ActivityTaskScheduledEvent(activityId: "1", "ValidateCommerceActivity"),
                ActivityTaskCompletedEvent(activityId: "1", new ValidateCommerceActivityOutput { IsValid = true }),

                // QUERY: Get order status mid-execution
                WorkflowQueryReceivedEvent(
                    queryType: "GetOrderStatus",
                    queryResult: new GetOrderStatusQueryResult
                    {
                        Status = "ValidatingOrder"
                    }),

                ActivityTaskScheduledEvent(activityId: "2", "CollectRiskActivity"),
                ActivityTaskCompletedEvent(activityId: "2", new CollectRiskActivityOutput { RiskLevel = "Low" }),

                // QUERY: Get payment status (not yet processed)
                WorkflowQueryReceivedEvent(
                    queryType: "GetPaymentStatus",
                    queryResult: new GetPaymentStatusQueryResult
                    {
                        Status = "Pending"
                    }),

                // ... workflow completes
                WorkflowExecutionCompletedEvent(
                    result: new OrderProcessingResult
                    {
                        FinalStatus = "Fulfilled"
                    })
            }
        };

        // Act & Assert
        var replay = await ReplayWorkflowAsync(eventHistory);
        var result = replay.GetResult<OrderProcessingResult>();
        Assert.Equal("Fulfilled", result.FinalStatus);
    }

    // ===== Helper Methods =====

    private async Task<WorkflowExecutionResult> ReplayWorkflowAsync(WorkflowHistory history)
    {
        using var testServer = new WorkflowServiceWorkflowServerOptions().Build();
        using var client = new WorkflowClient(testServer);

        return await client.ReplayWorkflowAsync(
            history,
            typeof(OrderProcessingWorkflow),
            _serviceProvider);
    }

    private WorkflowHistory LoadEventHistoryFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<WorkflowHistory>(json) 
            ?? throw new InvalidOperationException("Failed to deserialize history");
    }

    private WorkflowHistory ExportEventHistoryFromServer(string workflowId, string runId)
    {
        // TODO: Connect to Temporal server and export history
        // This would use `tctl workflow describe` or gRPC API
        throw new NotImplementedException();
    }
}
```

---

## Part 3: Exporting Production Event Histories

### Using Temporal CLI

```bash
# Export event history from production workflow
tctl workflow describe \
    --workflow-id "order-550e8400-e29b-41d4-a716-446655440001" \
    --run-id "550e8400-e29b-41d4-a716-446655440001" \
    --output json > order-history.json
```

### Using Temporal .NET SDK

```csharp
public class EventHistoryExporter
{
    private readonly ITemporalClient _client;

    public EventHistoryExporter(ITemporalClient client)
    {
        _client = client;
    }

    public async Task ExportHistoryAsync(string workflowId, string runId, string outputPath)
    {
        // Get workflow execution description
        var execution = await _client.GetWorkflowHandle(workflowId, runId).DescribeAsync();

        // Convert execution to replay history
        var history = ConvertToReplayHistory(execution);

        // Save to file
        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(outputPath, json);
    }

    private WorkflowHistory ConvertToReplayHistory(WorkflowExecutionDescription execution)
    {
        // TODO: Implement conversion from gRPC HistoryEvent to test history
        throw new NotImplementedException();
    }
}
```

---

## Part 4: Continuous Replay Testing in CI/CD

### GitHub Actions Workflow

```yaml
name: Replay Tests

on: [pull_request]

jobs:
  replay-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Download Production Histories
        run: |
          mkdir -p test-histories
          # Download from S3/artifact storage
          aws s3 cp s3://our-bucket/workflow-histories/ test-histories/ --recursive

      - name: Run Replay Tests
        run: |
          dotnet test Oms.Temporal.Tests.ReplayTests \
            --configuration Release \
            --logger "console;verbosity=detailed" \
            --collect:"XPlat Code Coverage"

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: replay-test-results
          path: test-results/

      - name: Comment PR with Results
        uses: actions/github-script@v6
        if: always()
        with:
          script: |
            const fs = require('fs');
            const results = fs.readFileSync('test-results/summary.json');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: `Replay Tests: ${results.passed}/${results.total} passed`
            });
```

---

## Part 5: Production History Collection Strategy

### Sampling Strategy

```csharp
public class ProductionHistorySampler
{
    private readonly ITemporalClient _client;
    private readonly IHistoryRepository _repository;
    private readonly ILogger _logger;

    public async Task SampleHistoriesAsync(int sampleSize = 50)
    {
        // Query workflows that completed in last 24 hours
        var query = new WorkflowExecutionFilter
        {
            StatusFilter = WorkflowExecutionStatus.Completed,
            ExecutionTimeFilter = new()
            {
                EarliestTime = DateTime.UtcNow.AddDays(-1),
                LatestTime = DateTime.UtcNow
            }
        };

        var executions = await _client.ListWorkflowExecutionsAsync(query).ToListAsync();

        // Sample randomly
        var sampled = executions
            .OrderBy(_ => Guid.NewGuid())
            .Take(sampleSize)
            .ToList();

        // Export and store
        foreach (var execution in sampled)
        {
            var history = await ExportHistoryAsync(execution);
            await _repository.StoreHistoryAsync(
                execution.Execution.WorkflowId,
                execution.Execution.RunId,
                history);

            _logger.LogInformation(
                "Sampled workflow {WorkflowId}",
                execution.Execution.WorkflowId);
        }
    }
}
```

---

## Part 6: Replay Test Assertions

```csharp
public class ReplayTestAssertions
{
    public static void AssertReplaySuccessful(
        WorkflowExecutionResult original,
        WorkflowExecutionResult replayed)
    {
        // Compare execution outcomes
        Assert.Equal(original.Status, replayed.Status);
        Assert.Equal(original.GetResult<OrderProcessingResult>().FinalStatus,
                     replayed.GetResult<OrderProcessingResult>().FinalStatus);

        // Compare execution history length
        Assert.Equal(original.Events.Count, replayed.Events.Count);

        // Compare decision points
        CompareDecisionSequence(original.Events, replayed.Events);
    }

    private static void CompareDecisionSequence(
        List<Event> original,
        List<Event> replayed)
    {
        var originalDecisions = original
            .Where(e => e is ActivityScheduledEvent or SignalReceivedEvent)
            .ToList();

        var replayedDecisions = replayed
            .Where(e => e is ActivityScheduledEvent or SignalReceivedEvent)
            .ToList();

        Assert.Equal(originalDecisions.Count, replayedDecisions.Count);

        for (int i = 0; i < originalDecisions.Count; i++)
        {
            var o = originalDecisions[i];
            var r = replayedDecisions[i];

            // Activities should be scheduled in same order
            if (o is ActivityScheduledEvent oActivity && r is ActivityScheduledEvent rActivity)
            {
                Assert.Equal(oActivity.ActivityType, rActivity.ActivityType);
            }
        }
    }
}
```

---

## Summary

Replay testing ensures:
- ✅ Code changes don't break historical workflows
- ✅ New versions backward compatible with old executions
- ✅ Activity logic deterministic and reproducible
- ✅ Signal/query handling correct
- ✅ Retry logic matches expectations
- ✅ Safe production deployments
- ✅ Confidence in versioning strategy

**Best Practices:**
1. Collect representative production histories
2. Run replay tests on every code change
3. Maintain history database for regression testing
4. Automate replay testing in CI/CD
5. Alert on replay failures

