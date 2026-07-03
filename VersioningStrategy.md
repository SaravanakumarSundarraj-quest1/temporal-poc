# Workflow Versioning Strategy

Complete guide for versioning OrderProcessingWorkflow, managing activity evolution, and handling backward compatibility.

---

## Overview

Versioning ensures:
- ✅ New workflows start with latest code
- ✅ Existing workflows continue with original logic
- ✅ Safe deployment without workflow interruption
- ✅ Activity additions/removals/reordering
- ✅ Safe schema migrations
- ✅ Historical workflow replays work correctly

---

## Part 1: Core Versioning Pattern

### Workflow.GetVersion()

```csharp
public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
{
    // Get workflow version for backward compatibility
    // Parameters: (id, minVersion, maxVersion)
    int workflowVersion = Workflow.GetVersion("OrderProcessing.V1", 1, 1);

    if (workflowVersion >= 1)
    {
        // New activities in V1
        await ValidateCommercePhaseAsync(input);
    }
}
```

**How GetVersion Works:**
1. **New workflows**: Returns `maxVersion` (uses latest code)
2. **Existing workflows**: Returns original version (reproducible)
3. **Replay**: Returns stored version (historically accurate)

---

## Part 2: Scenario 1 - Adding New Activity

### Current Workflow (V1)

```csharp
[WorkflowRun]
public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
{
    // Current flow:
    // 1. Validate Commerce
    // 2. Collect Risk
    // 3. Validate Payment
    // 4. Enrich
    // 5. Publish Fulfillment
    // 6. Publish Event

    await ValidateCommercePhaseAsync(input);
    await CollectRiskPhaseAsync(input);
    await ValidatePaymentPhaseAsync(input);
    await EnrichOrderPhaseAsync(input);
    await PublishFulfillmentPhaseAsync(input);
    await PublishStatusEventAsync("Fulfilled");
}
```

### Adding FraudDetectionActivity

```csharp
[WorkflowRun]
public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
{
    // Version gate for new fraud detection step
    int fraudDetectionVersion = Workflow.GetVersion("OrderProcessing.FraudDetection", 1, 2);

    // Existing activities
    await ValidateCommercePhaseAsync(input);
    await CollectRiskPhaseAsync(input);

    // NEW: Fraud detection (only in V2+)
    if (fraudDetectionVersion >= 2)
    {
        await PerformFraudDetectionAsync(input);
        if (_context.IsFraudulent)
        {
            _context.CurrentStatus = "FraudDetected";
            return BuildErrorResult("Fraud detected", "");
        }
    }

    // Continue with remaining activities
    await ValidatePaymentPhaseAsync(input);
    await EnrichOrderPhaseAsync(input);
    await PublishFulfillmentPhaseAsync(input);
    await PublishStatusEventAsync("Fulfilled");
}

private async Task PerformFraudDetectionAsync(ProcessOrderInput input)
{
    _logger?.LogInformation("Detecting fraud for order {OrderId}", _context.OrderId);

    var fraudResult = await ExecuteActivityAsync(
        () => _detectFraud.ExecuteAsync(new FraudDetectionActivityInput
        {
            OrderId = _context.OrderId,
            CustomerId = _context.CustomerId,
            Amount = input.TotalAmount,
            Email = input.CustomerEmail
        }),
        "FraudDetectionActivity",
        ActivityPolicies.FastApiPolicy);

    _context.IsFraudulent = fraudResult.IsFraudulent;
    _context.FraudScore = fraudResult.FraudScore;
}
```

**Behavior:**
- **Old workflows (V1)**: Skip fraud detection, continue normally
- **New workflows (V2+)**: Run fraud detection before payment
- **Replays**: Use version from original execution

---

## Part 3: Scenario 2 - Reordering Activities

### Current Order

```csharp
// Step 1: Validate Commerce
await ValidateCommercePhaseAsync(input);

// Step 2: Collect Risk
await CollectRiskPhaseAsync(input);

// Step 3: Validate Payment
await ValidatePaymentPhaseAsync(input);

// Step 4: Enrich
await EnrichOrderPhaseAsync(input);

// Step 5: Publish Fulfillment
await PublishFulfillmentPhaseAsync(input);
```

### New Order - Reorder to Risk Before Payment

```csharp
[WorkflowRun]
public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
{
    int reorderingVersion = Workflow.GetVersion("OrderProcessing.ReorderRisk", 1, 2);

    // Version 1: Validate Commerce → Collect Risk → Validate Payment
    if (reorderingVersion < 2)
    {
        await ValidateCommercePhaseAsync(input);
        await CollectRiskPhaseAsync(input);
        await ValidatePaymentPhaseAsync(input);
    }
    // Version 2+: Validate Commerce → Validate Payment → Collect Risk
    else
    {
        await ValidateCommercePhaseAsync(input);
        await ValidatePaymentPhaseAsync(input);
        await CollectRiskPhaseAsync(input);
    }

    await EnrichOrderPhaseAsync(input);
    await PublishFulfillmentPhaseAsync(input);
}
```

**Why This Works:**
- **Old workflows (V1)**: Use original order (risk → payment)
- **New workflows (V2+)**: Use new order (payment → risk)
- **Replays**: Use version-gated logic for each execution

---

## Part 4: Scenario 3 - Removing Activity

### Current Activities

```csharp
await ValidateCommercePhaseAsync(input);
await CollectRiskPhaseAsync(input);
await ValidatePaymentPhaseAsync(input);
await EnrichOrderPhaseAsync(input);
await PublishFulfillmentPhaseAsync(input);
```

### Removing ValidatePaymentActivity (Deprecated)

```csharp
[WorkflowRun]
public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
{
    int removePaymentValidationVersion = Workflow.GetVersion(
        "OrderProcessing.RemovePaymentValidation", 1, 2);

    await ValidateCommercePhaseAsync(input);
    await CollectRiskPhaseAsync(input);

    // Version 1: Keep old payment validation
    if (removePaymentValidationVersion < 2)
    {
        await ValidatePaymentPhaseAsync(input);
    }
    // Version 2+: Skip payment validation (now done by payment service)

    await EnrichOrderPhaseAsync(input);
    await PublishFulfillmentPhaseAsync(input);
}
```

**Why This Works:**
- **Old workflows (V1)**: Payment validation still runs
- **New workflows (V2+)**: Payment validation skipped (handled elsewhere)
- **Replays**: Maintains historical accuracy

---

## Part 5: Scenario 4 - Schema Changes in Activity Input/Output

### Original Activity

```csharp
public class CollectRiskActivityInput
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal OrderAmount { get; set; }
    public List<string> ProductCodes { get; set; } = new();
    // No CustomerSegment in V1
}

public async Task<CollectRiskActivityOutput> ExecuteAsync(CollectRiskActivityInput input)
{
    // V1 logic
    var result = await _riskEngine.AssessAsync(new
    {
        orderId = input.OrderId,
        customerId = input.CustomerId,
        amount = input.OrderAmount,
        products = input.ProductCodes
    });
    // ...
}
```

### Enhanced Schema (V2)

```csharp
// New version with additional fields
public class CollectRiskActivityInputV2
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal OrderAmount { get; set; }
    public List<string> ProductCodes { get; set; } = new();
    public string CustomerSegment { get; set; } = "Standard"; // NEW
    public DateTime? LastPurchaseDate { get; set; } // NEW
}

[Workflow]
public class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    [WorkflowRun]
    public async Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input)
    {
        int schemaVersion = Workflow.GetVersion(
            "OrderProcessing.CollectRiskSchema", 1, 2);

        // Version 1: Use old input schema
        if (schemaVersion < 2)
        {
            var riskInput = new CollectRiskActivityInput
            {
                OrderId = _context.OrderId,
                CustomerId = _context.CustomerId,
                OrderAmount = input.TotalAmount,
                ProductCodes = input.Items.Select(i => i.ProductCode).ToList()
                // No CustomerSegment
            };

            var result = await ExecuteActivityAsync(
                () => _collectRisk.ExecuteAsync(riskInput),
                "CollectRiskActivity",
                ActivityPolicies.SlowApiPolicy);

            _context.RiskAssessment = MapToRiskDataDto(result);
        }
        // Version 2+: Use enhanced input schema
        else
        {
            var riskInputV2 = new CollectRiskActivityInputV2
            {
                OrderId = _context.OrderId,
                CustomerId = _context.CustomerId,
                OrderAmount = input.TotalAmount,
                ProductCodes = input.Items.Select(i => i.ProductCode).ToList(),
                CustomerSegment = input.CustomerSegment ?? "Standard",
                LastPurchaseDate = input.LastPurchaseDate
            };

            var result = await ExecuteActivityAsync(
                () => _collectRisk.ExecuteAsync(riskInputV2),
                "CollectRiskActivity",
                ActivityPolicies.SlowApiPolicy);

            _context.RiskAssessment = MapToRiskDataDto(result);
        }

        // Rest of workflow...
    }
}
```

**Why This Works:**
- **Old workflows (V1)**: Call activity with original input
- **New workflows (V2+)**: Call activity with enhanced input
- **Activity handles both**: Can check input type/version

---

## Part 6: Activity Implementation with Versioning

```csharp
public class CollectRiskActivity : BaseActivityWithErrorHandling, ICollectRiskActivity
{
    [Activity]
    public async Task<CollectRiskActivityOutput> ExecuteAsync(object input)
    {
        // Handle both V1 and V2 inputs
        if (input is CollectRiskActivityInputV2 inputV2)
        {
            return await ExecuteV2(inputV2);
        }
        else if (input is CollectRiskActivityInput inputV1)
        {
            return await ExecuteV1(inputV1);
        }

        throw new ArgumentException($"Unknown input type: {input?.GetType()}");
    }

    private async Task<CollectRiskActivityOutput> ExecuteV1(CollectRiskActivityInput input)
    {
        _logger.LogInformation("Executing CollectRiskActivity V1");

        var result = await _riskEngine.AssessAsync(new
        {
            orderId = input.OrderId,
            customerId = input.CustomerId,
            amount = input.OrderAmount,
            products = input.ProductCodes
        });

        // V1 logic
        return MapResult(result);
    }

    private async Task<CollectRiskActivityOutput> ExecuteV2(CollectRiskActivityInputV2 input)
    {
        _logger.LogInformation("Executing CollectRiskActivity V2");

        var result = await _riskEngine.AssessAsync(new
        {
            orderId = input.OrderId,
            customerId = input.CustomerId,
            amount = input.OrderAmount,
            products = input.ProductCodes,
            segment = input.CustomerSegment,
            lastPurchase = input.LastPurchaseDate
        });

        // V2 logic - more sophisticated with segment and history
        return MapResultV2(result, input.CustomerSegment);
    }
}
```

---

## Part 7: Versioning Deployment Strategy

### Phase 1: Code Deployment

```bash
# 1. Deploy new workflow code with version gates
dotnet publish --configuration Release

# 2. Start worker with new code
worker --task-queue OMS_QUEUE

# All GetVersion() calls return minVersion initially
```

### Phase 2: Monitor Old Workflows

```bash
# 2a. Old workflows in-flight continue with old version
# 2b. New workflows start with new version
# 2c. No disruption to existing executions

# Monitor metrics:
- New workflows: Count
- Old workflows: Count
- Version distribution
```

### Phase 3: Gradual Cutover

```bash
# Once old workflows complete:
1. Remove version gates in code (V1 branch)
2. Deploy simplified code
3. New workflows use simplified logic
4. Old workflows already using old code (immutable)
```

### Example: Gradual Removal

**Before (with gate):**
```csharp
int version = Workflow.GetVersion("Feature.X", 1, 2);
if (version >= 2)
{
    await NewActivityAsync(); // V2+
}
else
{
    await OldActivityAsync(); // V1
}
```

**After (gates removed, once V1 workflows complete):**
```csharp
// All workflows now use NewActivityAsync
await NewActivityAsync();
```

---

## Part 8: Deployment Checklist

### Pre-Deployment

- [ ] Version gate implemented with clear IDs
- [ ] minVersion ≤ current maxVersion
- [ ] Activity signatures backward compatible
- [ ] All existing workflows can reach end naturally
- [ ] Rollback plan documented

### Post-Deployment

- [ ] Monitor in-flight workflows
- [ ] Verify new workflows use latest version
- [ ] Check version metrics dashboard
- [ ] Gradual rollout percentage tracked
- [ ] No errors in activity logs

### After Old Workflows Complete

- [ ] Remove version gates from code
- [ ] Simplify workflow logic
- [ ] Document when gates were removed
- [ ] Update deployment runbooks

---

## Part 9: Testing Versioned Workflows

```csharp
namespace Oms.Temporal.Tests;

using Xunit;

public class WorkflowVersioningTests
{
    [Fact]
    public async Task OldWorkflow_V1_SkipsFraudDetection()
    {
        // Arrange
        var env = new TestWorkflowEnvironment();
        var input = new ProcessOrderInput
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-001",
            TotalAmount = 100m
        };

        // Act: Replay with V1 history
        var result = await env.ExecuteWorkflowWithVersionAsync(
            input,
            versionHistory: new Dictionary<string, int>
            {
                ["OrderProcessing.FraudDetection"] = 1 // Old version
            });

        // Assert: Fraud detection should NOT have run
        Assert.False(env.FraudDetectionActivityCalled);
        Assert.Equal("Fulfilled", result.FinalStatus);
    }

    [Fact]
    public async Task NewWorkflow_V2_RunsFraudDetection()
    {
        // Arrange
        var env = new TestWorkflowEnvironment();
        var input = new ProcessOrderInput
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = "ORD-002",
            TotalAmount = 100m
        };

        // Act: Run with V2 (new) version
        var result = await env.ExecuteWorkflowWithVersionAsync(
            input,
            versionHistory: new Dictionary<string, int>
            {
                ["OrderProcessing.FraudDetection"] = 2 // New version
            });

        // Assert: Fraud detection should have run
        Assert.True(env.FraudDetectionActivityCalled);
    }

    [Fact]
    public async Task ReplayWorkflow_UsesHistoricalVersion()
    {
        // Arrange
        var env = new TestWorkflowEnvironment();
        var historicalVersion = 1; // Original version
        var input = new ProcessOrderInput { OrderId = Guid.NewGuid() };

        // Act: Replay execution with historical version
        var result = await env.ReplayWorkflowAsync(input, historicalVersion);

        // Assert: Historical version should be used (not current)
        Assert.Equal(historicalVersion, env.ActualVersionUsed);
    }
}
```

---

## Summary

Versioning Strategy:
- ✅ `Workflow.GetVersion()` for backward compatibility
- ✅ Version gates for activity addition/removal/reordering
- ✅ Schema versioning for activity inputs/outputs
- ✅ Seamless deployments without workflow interruption
- ✅ Historical workflow replays maintain accuracy
- ✅ Gradual cutover phase after old workflows complete
- ✅ Comprehensive testing before deployment

