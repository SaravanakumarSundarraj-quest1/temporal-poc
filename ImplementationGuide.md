# Implementation Guide: Domain Models, DTOs & Contracts

## Summary

This package contains a comprehensive set of production-ready C# templates for the Order Management System. It includes:

1. **DomainModelsAndContracts.md** - Complete reference guide (~800 lines)
2. **13 C# code templates** in `/code-templates/` directory - Ready to be placed in project folders

### What Was Created

**Documentation** (`DomainModelsAndContracts.md`):
- 5 Enums (OrderStatus, PaymentStatus, RiskLevel, CustomerSegment, TransactionStatus)
- 4 Domain Model aggregates (Order, Customer, Payment, RiskData, EnrichedOrder)
- 10+ DTOs for API communication
- Temporal activity contracts (input/output)
- Workflow signals (CancelOrder, RequestCorrection, ApproveRisk)
- Workflow queries (GetOrderStatus, GetOrderDetails, GetPaymentStatus)
- Kafka event contracts (FulfillmentOrderEvent, OrderStatusChangedEvent)

**C# Code Templates** (Ready to implement):

| Category | Files | Location |
|----------|-------|----------|
| **Enums** | 2 files | `Oms.Domain.Enums` |
| **Domain Models** | 4 files | `Oms.Domain.Aggregates.*` |
| **Value Objects** | 1 file | `Oms.Domain.ValueObjects` |
| **Exceptions** | 1 file | `Oms.Domain.Exceptions` |
| **DTOs** | 1 file | `Oms.Application.DTOs` |
| **Mappings** | 1 file | `Oms.Application.Mappings` |
| **Activity Contracts** | 1 file | `Oms.Contracts.ActivityInputOutputs` |
| **Signals** | 1 file | `Oms.Contracts.WorkflowSignals` |
| **Queries** | 1 file | `Oms.Contracts.WorkflowQueries` |
| **Events** | 1 file | `Oms.Contracts.Events` |

---

## File Structure

### Domain Layer (`/code-templates/Domain_*.cs`)

These files implement Domain-Driven Design patterns:

1. **Enums_OrderStatus.cs**
   - 13 states: INITIALIZING → VALIDATING_ORDER → ... → FULFILLED/CANCELLED/EXPIRED
   - Complete state machine in comments

2. **Enums_Additional.cs**
   - PaymentStatus (6 states)
   - RiskLevel (Low, Medium, High, Critical)
   - CustomerSegment (New, Loyal, VIP, Flagged)
   - TransactionStatus (5 states)

3. **Domain_Order_Aggregate.cs**
   - `Order` class: Aggregate root with ~200 lines of state transition logic
   - `OrderItem` class: Line item entity
   - `IOrderRepository`: Persistence interface
   - Factory method: `Order.Create(...)`
   - 8 State transition methods with validation
   - 5 Business logic methods

4. **Domain_Customer_Aggregate.cs**
   - `Customer` class: Customer aggregate root
   - `Address` class: Value object for addresses
   - `ICustomerRepository`: Persistence interface
   - Segment auto-promotion (New → Loyal after 10 orders)

5. **Domain_Payment_Aggregate.cs**
   - `Payment` class: Payment aggregate root with retry support
   - `PaymentTransaction` class: Transaction history
   - `IPaymentRepository`: Persistence interface
   - 6 State management methods
   - Retry logic (max 5 attempts)

6. **Domain_ValueObjects.cs**
   - `RiskData` class: Immutable risk assessment results
   - `RiskIndicator` class: Individual risk factors
   - `EnrichedOrder` class: Immutable PIM-enriched data
   - `EnrichedOrderItem` class: Enriched line item details

7. **Domain_Exceptions.cs**
   - `DomainException`: Base exception
   - `InvalidOrderStateException`: For state transition violations

### Application Layer (`/code-templates/Application_*.cs`)

Transfer objects and mapping logic:

1. **Application_DTOs.cs** (~200 lines)
   - `CreateOrderDto`: Request for creating orders
   - `OrderDto`: Full order response
   - `PaymentDto`, `RiskDataDto`, `EnrichedOrderDto`: Supporting responses
   - `PaginatedDto<T>`, `ErrorDto`: Standard response wrappers
   - All string-based enums for JSON serialization

2. **Application_Mappings.cs** (~100 lines)
   - `OrderMappingProfile`: Domain ↔ DTO mappings
   - `CreateOrderMappingProfile`: DTO → Domain for creation
   - `MappingExtensions`: Helper methods for manual mapping

### Contracts Layer (`/code-templates/Contracts_*.cs`)

Serializable contracts for external communication:

1. **Contracts_ActivityInputOutputs.cs** (~200 lines)
   - 7 Activity pairs (Input/Output):
     - ValidateCommerceActivity
     - CollectRiskActivity
     - ValidatePaymentActivity
     - EnrichOrderActivity
     - PublishFulfillmentActivity
     - RequestApprovalActivity

2. **Contracts_WorkflowSignals.cs** (~50 lines)
   - `CancelOrderSignal`: Terminate workflow
   - `RequestCorrectionSignal`: Retry validation with corrections
   - `ApproveRiskSignal`: Manager override for high-risk orders

3. **Contracts_WorkflowQueries.cs** (~80 lines)
   - `GetOrderStatusQuery`: Current state
   - `GetOrderDetailsQuery`: Full order snapshot
   - `GetPaymentStatusQuery`: Payment details
   - `GetRiskAssessmentQuery`: Risk assessment details

4. **Contracts_Events.cs** (~150 lines)
   - `FulfillmentOrderEvent`: Kafka event to fulfillment system
   - `OrderStatusChangedEvent`: Status transition event
   - `PaymentStatusChangedEvent`: Payment lifecycle event
   - `OrderCancelledEvent`: Cancellation event
   - `OrderFailedEvent`: Failure/error event

---

## How to Use These Templates

### Step 1: Create Project Structure

Use the **ProjectSetup.md** guide to create the 10 projects:

```bash
# Create solution
dotnet new globaljson --sdk-version 8.0.0 --roll-forward latestFeature
dotnet new sln -n Oms

# Create projects (from ProjectSetup.md)
dotnet new classlib -n Oms.Domain -f net8.0
dotnet new classlib -n Oms.Application -f net8.0
# ... (10 projects total)
```

### Step 2: Add Code Templates to Projects

Copy each template to the appropriate project and namespace:

```bash
# Domain Layer
cp code-templates/Enums_*.cs Oms.Domain/Enums/
cp code-templates/Domain_*.cs Oms.Domain/Aggregates/  # Split into subfolders
cp code-templates/Domain_ValueObjects.cs Oms.Domain/ValueObjects/
cp code-templates/Domain_Exceptions.cs Oms.Domain/Exceptions/

# Application Layer
cp code-templates/Application_DTOs.cs Oms.Application/DTOs/
cp code-templates/Application_Mappings.cs Oms.Application/Mappings/

# Contracts Layer
cp code-templates/Contracts_*.cs Oms.Contracts/
```

### Step 3: Organize Domain Aggregates

The `/Domain_Aggregates/` templates should be split into folders:

```
Oms.Domain/
├── Aggregates/
│   ├── Order/
│   │   ├── Order.cs
│   │   ├── OrderItem.cs
│   │   └── IOrderRepository.cs
│   ├── Customer/
│   │   ├── Customer.cs
│   │   ├── Address.cs
│   │   └── ICustomerRepository.cs
│   └── Payment/
│       ├── Payment.cs
│       ├── PaymentTransaction.cs
│       └── IPaymentRepository.cs
├── ValueObjects/
│   ├── RiskData.cs
│   ├── RiskIndicator.cs
│   ├── EnrichedOrder.cs
│   └── EnrichedOrderItem.cs
├── Enums/
│   ├── OrderStatus.cs
│   ├── PaymentStatus.cs
│   ├── RiskLevel.cs
│   ├── CustomerSegment.cs
│   └── TransactionStatus.cs
└── Exceptions/
    └── DomainException.cs
```

### Step 4: Configure AutoMapper

In `Oms.Api/Program.cs`:

```csharp
builder.Services.AddAutoMapper(typeof(OrderMappingProfile));
```

### Step 5: Implement Repositories

Create implementations of:
- `IOrderRepository`
- `ICustomerRepository`
- `IPaymentRepository`

### Step 6: Create Temporal Activities

Reference the activity contracts from `Contracts_ActivityInputOutputs.cs`:

```csharp
public class ValidateCommerceActivity : IValidateCommerceActivity
{
    [Activity]
    public async Task<ValidateCommerceActivityOutput> Execute(
        ValidateCommerceActivityInput input)
    {
        // Implementation
    }
}
```

---

## Design Patterns Implemented

### 1. Domain-Driven Design (DDD)
- **Aggregate Roots**: Order, Customer, Payment
- **Value Objects**: RiskData, EnrichedOrder, Address
- **Repositories**: Interfaces in domain, implementations in infrastructure
- **Factories**: `Order.Create()`, `Customer.Create()`, `Payment.Create()`

### 2. State Machine
- 13 ordered states from INITIALIZING → FULFILLED
- Encapsulated transitions in domain model
- Explicit state change validation

### 3. Immutability
- Value objects (RiskData, EnrichedOrder) are immutable
- Created via factory methods
- Prevents business logic drift

### 4. Encapsulation
- Private setters on all properties
- State changes through public methods only
- Related entities accessed through aggregate root

### 5. Separation of Concerns
- **Domain**: Business logic (no dependencies)
- **Application**: DTOs and mappings (light dependencies)
- **Contracts**: Serialization only (for Temporal/Kafka)

---

## Key Classes & Methods

### Order Aggregate Root

**Creation**:
```csharp
var order = Order.Create(customerId, orderNumber, totalAmount, customer, expiresAt);
```

**State Transitions** (example):
```csharp
order.TransitionToValidatingOrder();
order.TransitionToCollectingRisk();
order.AssignRiskAssessment(riskData);
order.TransitionToFulfilled();
```

**Queries**:
```csharp
bool isExpired = order.IsExpired();
bool canCancel = order.CanBeCancelled();
bool isPaid = order.HasPaymentCharged();
```

### Payment Aggregate Root

**Creation & State**:
```csharp
var payment = Payment.Create(orderId, amount, "Stripe");
payment.SetProcessing();
payment.SetAuthorized("txn_12345");
payment.SetRefunded();
```

### RiskData Value Object

**Creation** (immutable after):
```csharp
var risk = RiskData.Create(
    RiskLevel.Critical,
    riskScore: 85.5m,
    indicators: new List<RiskIndicator> { ... },
    engineVersion: "v2.1",
    requiresReview: true
);
```

---

## Next Implementation Steps

### Phase 1: Infrastructure (2-3 days)
1. Create EF Core DbContext and configurations
2. Implement repositories
3. Setup DI in Oms.Api and Oms.Worker

### Phase 2: Temporal Workflow (2-3 days)
1. Implement `OrderProcessingWorkflow`
2. Implement 7 activities
3. Implement payload codec for encryption

### Phase 3: API Layer (1-2 days)
1. Create `OrderController` with REST endpoints
2. Implement request validation
3. Add error handling middleware

### Phase 4: Worker Layer (1 day)
1. Create `TemporalWorkerHostedService`
2. Configure task queues
3. Add health checks

### Phase 5: Testing (2-3 days)
1. Unit tests for domain models
2. Integration tests for activities
3. Workflow replay tests

### Phase 6: Deployment (1 day)
1. Docker build and test
2. Local docker-compose deployment
3. Kubernetes manifests

---

## Validation Checklist

Before proceeding to implementation:

- [ ] All 13 enum values match Architecture.md state machine
- [ ] Order aggregate has all 8 state transition methods
- [ ] All DTOs have validation attributes
- [ ] AutoMapper profiles compile successfully
- [ ] Activity contracts match Architecture.md I/O specifications
- [ ] All signal and query contracts are present
- [ ] Kafka event contracts include metadata fields
- [ ] Repository interfaces are defined for all aggregates
- [ ] Value objects are truly immutable
- [ ] No circular dependencies between layers

---

## File Statistics

| File | Lines | Purpose |
|------|-------|---------|
| `Enums_OrderStatus.cs` | 30 | 13-state order lifecycle |
| `Enums_Additional.cs` | 50 | 5 supporting enums |
| `Domain_Order_Aggregate.cs` | 200 | Order aggregate + items + repository |
| `Domain_Customer_Aggregate.cs` | 60 | Customer aggregate + repository |
| `Domain_Payment_Aggregate.cs` | 90 | Payment aggregate + repository |
| `Domain_ValueObjects.cs` | 100 | RiskData + EnrichedOrder immutables |
| `Domain_Exceptions.cs` | 10 | Domain exception base |
| `Application_DTOs.cs` | 150 | 15+ DTO classes |
| `Application_Mappings.cs` | 100 | AutoMapper profiles + extensions |
| `Contracts_ActivityInputOutputs.cs` | 200 | 7 activity pairs (14 classes) |
| `Contracts_WorkflowSignals.cs` | 40 | 3 signal contracts |
| `Contracts_WorkflowQueries.cs` | 80 | 4 query pairs (8 classes) |
| `Contracts_Events.cs` | 140 | 5 Kafka event contracts |
| **Total** | **~1,250** | **All layers + documentation** |

---

## Quick Reference: State Transitions

```
INITIALIZING
    ↓ (TransitionToValidatingOrder)
VALIDATING_ORDER
    ↓ (TransitionToCollectingRisk)
    ↕ (RetryCorrection) ← PENDING_CORRECTION
    ↓
COLLECTING_RISK
    ↓ (AssignRiskAssessment)
    ├→ RiskRejected (if Critical)
    │    ↓ (ApproveRiskOverride)
    └→ AWAITING_PAYMENT
         ↓ (TransitionToValidatingPayment)
         VALIDATING_PAYMENT
         ↕ (PaymentInvalid)
         ↓
         ENRICHING
         ↓ (TransitionToFulfilled)
         FULFILLED

Anytime → CANCELLED (Cancel)
Anytime → EXPIRED (timeout)
Anytime → PROCESSING_ERROR (unhandled error)
```

---

## Commit Information

- **Commit**: `1261ede`
- **Message**: "Add comprehensive domain models, DTOs, enums, and contracts specification with C# implementation templates"
- **Files**: 14 (1 Markdown + 13 C# templates)
- **Lines Added**: 2,625

---

## Next Document

**Create**: `ImplementationStarterGuide.md`
- Step-by-step for implementing first activity
- Sample EF Core DbContext
- Sample DI setup
- Local testing with Temporal CLI

