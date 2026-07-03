# Domain Models, DTOs, Enums & Contracts Guide

Complete reference for all data structures in the Order Management System.

---

## Overview

This guide covers four layers of data representation:

1. **Domain Models** (Oms.Domain): Core business entities with logic
2. **DTOs** (Oms.Application): Application-layer data transfer objects
3. **Enums** (Oms.Domain.Enums): Type-safe state and classification definitions
4. **Contracts** (Oms.Contracts): Serializable contracts for Temporal and messaging

### Design Principles

- **Domain-Driven Design**: Business logic lives in domain entities
- **Immutability**: Value objects are immutable after creation
- **Encapsulation**: Aggregate roots control access to related entities
- **Separation of Concerns**: Domain ≠ DTOs ≠ Contracts
- **Testability**: No external dependencies in domain models

---

## Part 1: Enums

All type-safe enumerations for state management and classification.

### OrderStatus

Represents the complete lifecycle of an order.

```csharp
namespace Oms.Domain.Enums;

public enum OrderStatus
{
    /// <summary>Order created, awaiting initial validation</summary>
    Initializing = 0,
    
    /// <summary>Validating order details against commerce system</summary>
    ValidatingOrder = 1,
    
    /// <summary>Validation failed, awaiting customer correction</summary>
    PendingCorrection = 2,
    
    /// <summary>Collecting risk assessment from external engine</summary>
    CollectingRisk = 3,
    
    /// <summary>Risk assessment rejected (high/critical risk level)</summary>
    RiskRejected = 4,
    
    /// <summary>Awaiting payment processing (pause state)</summary>
    AwaitingPayment = 5,
    
    /// <summary>Payment authorization failed</summary>
    PaymentInvalid = 6,
    
    /// <summary>Validating payment with gateway</summary>
    ValidatingPayment = 7,
    
    /// <summary>Enriching order with PIM data</summary>
    Enriching = 8,
    
    /// <summary>Order fulfilled and published to fulfillment system</summary>
    Fulfilled = 9,
    
    /// <summary>Order expired (SLA timeout exceeded)</summary>
    Expired = 10,
    
    /// <summary>Order cancelled by customer or manager</summary>
    Cancelled = 11,
    
    /// <summary>Unrecoverable error, manual intervention required</summary>
    ProcessingError = 12
}
```

**State Transitions**:
```
INITIALIZING
    ↓
VALIDATING_ORDER ← PENDING_CORRECTION (retry)
    ↓
COLLECTING_RISK ← RISK_REJECTED (manager override via ApproveRisk)
    ↓
AWAITING_PAYMENT
    ↓
VALIDATING_PAYMENT ← PAYMENT_INVALID (retry via RequestCorrection)
    ↓
ENRICHING
    ↓
FULFILLED

Cancel at any state → CANCELLED
Timeout at any state → EXPIRED
Unhandled error → PROCESSING_ERROR
```

### PaymentStatus

Payment state throughout authorization and capture lifecycle.

```csharp
public enum PaymentStatus
{
    /// <summary>Payment created, not yet authorized</summary>
    Pending = 0,
    
    /// <summary>Payment authorization in progress</summary>
    Processing = 1,
    
    /// <summary>Payment authorized (funds held, not captured)</summary>
    Authorized = 2,
    
    /// <summary>Payment captured (funds transferred)</summary>
    Captured = 3,
    
    /// <summary>Payment authorization failed</summary>
    Failed = 4,
    
    /// <summary>Authorization reversed (funds released)</summary>
    Reversed = 5,
    
    /// <summary>Payment refunded (after capture)</summary>
    Refunded = 6
}
```

### RiskLevel

Classification of order risk by external risk engine.

```csharp
public enum RiskLevel
{
    /// <summary>Low risk - proceed normally</summary>
    Low = 0,
    
    /// <summary>Medium risk - acceptable, but monitor</summary>
    Medium = 1,
    
    /// <summary>High risk - requires manual review</summary>
    High = 2,
    
    /// <summary>Critical risk - auto-reject or require manager approval</summary>
    Critical = 3
}
```

### CustomerSegment

Customer classification for business logic routing.

```csharp
public enum CustomerSegment
{
    /// <summary>New customer (< 1st order)</summary>
    New = 0,
    
    /// <summary>Loyal customer (> 10 orders)</summary>
    Loyal = 1,
    
    /// <summary>VIP customer (high lifetime value)</summary>
    Vip = 2,
    
    /// <summary>Flagged customer (chargeback/fraud history)</summary>
    Flagged = 3
}
```

### TransactionStatus

Individual payment transaction outcome.

```csharp
public enum TransactionStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Declined = 3,
    Timeout = 4
}
```

---

## Part 2: Domain Models

Core business entities with encapsulated logic.

### Aggregate Root: Order

```csharp
namespace Oms.Domain.Aggregates.Order;

/// <summary>
/// Order aggregate root - coordinates all order-related data and state.
/// Maintains invariants about order state transitions and financial calculations.
/// </summary>
public class Order
{
    // ===== Properties =====
    
    public Guid OrderId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    
    public OrderStatus CurrentStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    
    public decimal TotalAmount { get; private set; }
    
    // Related aggregates
    public Customer OrderCustomer { get; private set; } = new();
    public List<OrderItem> Items { get; private set; } = new();
    public Payment? OrderPayment { get; private set; }
    public RiskData? RiskAssessment { get; private set; }
    public EnrichedOrder? EnrichedData { get; private set; }
    
    // Tracking
    public int CorrectionAttempts { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    // ===== Factory Method =====
    
    public static Order Create(
        Guid customerId,
        string orderNumber,
        decimal totalAmount,
        Customer customer,
        DateTime expiresAt)
    {
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = customerId,
            OrderCustomer = customer,
            CurrentStatus = OrderStatus.Initializing,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            UpdatedAt = DateTime.UtcNow,
            TotalAmount = totalAmount,
            CorrectionAttempts = 0
        };
        return order;
    }

    // ===== State Transitions =====
    
    public void TransitionToValidatingOrder()
    {
        if (CurrentStatus != OrderStatus.Initializing)
            throw new InvalidOrderStateException("Can only transition to ValidatingOrder from Initializing");
        
        CurrentStatus = OrderStatus.ValidatingOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToPendingCorrection()
    {
        if (CurrentStatus != OrderStatus.ValidatingOrder)
            throw new InvalidOrderStateException("Can only transition to PendingCorrection from ValidatingOrder");
        
        if (CorrectionAttempts >= 3)
            throw new InvalidOrderStateException("Maximum 3 correction attempts exceeded");
        
        CurrentStatus = OrderStatus.PendingCorrection;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RetryCorrection()
    {
        if (CurrentStatus != OrderStatus.PendingCorrection)
            throw new InvalidOrderStateException("Can only retry from PendingCorrection state");
        
        CorrectionAttempts++;
        CurrentStatus = OrderStatus.ValidatingOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToCollectingRisk()
    {
        if (CurrentStatus != OrderStatus.ValidatingOrder)
            throw new InvalidOrderStateException("Can only transition to CollectingRisk from ValidatingOrder");
        
        CurrentStatus = OrderStatus.CollectingRisk;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignRiskAssessment(RiskData riskData)
    {
        RiskAssessment = riskData;
        
        if (riskData.Level == RiskLevel.Critical)
        {
            CurrentStatus = OrderStatus.RiskRejected;
        }
        else
        {
            CurrentStatus = OrderStatus.AwaitingPayment;
        }
        
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApproveRiskOverride()
    {
        if (CurrentStatus != OrderStatus.RiskRejected)
            throw new InvalidOrderStateException("Can only approve risk from RiskRejected state");
        
        CurrentStatus = OrderStatus.AwaitingPayment;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToValidatingPayment()
    {
        if (CurrentStatus != OrderStatus.AwaitingPayment)
            throw new InvalidOrderStateException("Can only transition to ValidatingPayment from AwaitingPayment");
        
        CurrentStatus = OrderStatus.ValidatingPayment;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToPaymentInvalid()
    {
        if (CurrentStatus != OrderStatus.ValidatingPayment)
            throw new InvalidOrderStateException("Can only transition to PaymentInvalid from ValidatingPayment");
        
        CurrentStatus = OrderStatus.PaymentInvalid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToEnriching()
    {
        if (CurrentStatus != OrderStatus.ValidatingPayment)
            throw new InvalidOrderStateException("Can only transition to Enriching from ValidatingPayment");
        
        CurrentStatus = OrderStatus.Enriching;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignEnrichedData(EnrichedOrder enrichedOrder)
    {
        EnrichedData = enrichedOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionToFulfilled()
    {
        if (CurrentStatus != OrderStatus.Enriching)
            throw new InvalidOrderStateException("Can only transition to Fulfilled from Enriching");
        
        CurrentStatus = OrderStatus.Fulfilled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (CurrentStatus == OrderStatus.Fulfilled || CurrentStatus == OrderStatus.Cancelled)
            throw new InvalidOrderStateException("Cannot cancel completed orders");
        
        CurrentStatus = OrderStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (CurrentStatus == OrderStatus.Fulfilled || CurrentStatus == OrderStatus.Cancelled)
            throw new InvalidOrderStateException("Cannot expire completed orders");
        
        CurrentStatus = OrderStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    // ===== Business Logic =====
    
    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
    
    public bool CanBeCancelled() =>
        CurrentStatus != OrderStatus.Fulfilled &&
        CurrentStatus != OrderStatus.Cancelled &&
        CurrentStatus != OrderStatus.Expired;
    
    public bool HasPaymentCharged() =>
        OrderPayment != null &&
        (OrderPayment.Status == PaymentStatus.Authorized ||
         OrderPayment.Status == PaymentStatus.Captured);
    
    public void AddItem(OrderItem item)
    {
        if (CurrentStatus != OrderStatus.Initializing)
            throw new InvalidOrderStateException("Can only add items during initialization");
        
        Items.Add(item);
    }

    public void AssignPayment(Payment payment)
    {
        OrderPayment = payment;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

### Entity: OrderItem

```csharp
/// <summary>Line item in an order with product and pricing information</summary>
public class OrderItem
{
    public Guid ItemId { get; private set; }
    public Guid OrderId { get; private set; }
    
    public string ProductCode { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    // EF Core constructor
    private OrderItem() { }

    public static OrderItem Create(
        string productCode,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        if (unitPrice < 0 || quantity <= 0)
            throw new ArgumentException("Invalid price or quantity");
        
        return new OrderItem
        {
            ItemId = Guid.NewGuid(),
            ProductCode = productCode,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }

    public decimal GetLineTotal() => UnitPrice * Quantity;
}
```

### Entity: Customer

```csharp
namespace Oms.Domain.Aggregates.Customer;

/// <summary>Customer entity with contact and segment information</summary>
public class Customer
{
    public Guid CustomerId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    
    public Address ShippingAddress { get; private set; } = new();
    public Address BillingAddress { get; private set; } = new();
    
    public CustomerSegment Segment { get; private set; }
    public int PreviousOrderCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Customer() { }

    public static Customer Create(
        string email,
        string name,
        string phone,
        Address shippingAddress,
        Address billingAddress)
    {
        return new Customer
        {
            CustomerId = Guid.NewGuid(),
            Email = email,
            Name = name,
            Phone = phone,
            ShippingAddress = shippingAddress,
            BillingAddress = billingAddress,
            Segment = CustomerSegment.New,
            PreviousOrderCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>Value object representing physical address</summary>
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    
    public override bool Equals(object? obj) =>
        obj is Address other &&
        Street == other.Street &&
        City == other.City &&
        State == other.State &&
        ZipCode == other.ZipCode &&
        Country == other.Country;
    
    public override int GetHashCode() =>
        HashCode.Combine(Street, City, State, ZipCode, Country);
}
```

### Aggregate Root: Payment

```csharp
namespace Oms.Domain.Aggregates.Payment;

/// <summary>
/// Payment aggregate - manages payment lifecycle from authorization to capture/refund.
/// Tracks transaction history and supports retries and partial payments.
/// </summary>
public class Payment
{
    public Guid PaymentId { get; private set; }
    public Guid OrderId { get; private set; }
    
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    
    public PaymentStatus Status { get; private set; }
    public string Gateway { get; private set; } = string.Empty;
    public string TransactionId { get; private set; } = string.Empty;
    
    public DateTime? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    
    public List<PaymentTransaction> Transactions { get; private set; } = new();

    private Payment() { }

    public static Payment Create(Guid orderId, decimal amount, string gateway)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
        
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Gateway = gateway,
            Status = PaymentStatus.Pending,
            RetryCount = 0
        };
    }

    public void SetAuthorized(string transactionId)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Processing)
            throw new InvalidOperationException("Cannot authorize from current state");
        
        Status = PaymentStatus.Authorized;
        TransactionId = transactionId;
        ProcessedAt = DateTime.UtcNow;
        
        Transactions.Add(PaymentTransaction.Create(
            transactionId,
            Amount,
            TransactionStatus.Success
        ));
    }

    public void IncrementRetry()
    {
        RetryCount++;
        Status = PaymentStatus.Processing;
    }

    public void SetFailed(string failureReason)
    {
        Status = PaymentStatus.Failed;
        Transactions.Add(PaymentTransaction.Create(
            TransactionId,
            Amount,
            TransactionStatus.Failed,
            failureReason
        ));
    }

    public void SetRefunded()
    {
        if (Status != PaymentStatus.Authorized && Status != PaymentStatus.Captured)
            throw new InvalidOperationException("Can only refund authorized or captured payments");
        
        Status = PaymentStatus.Refunded;
    }

    public bool RequiresRetry() => Status == PaymentStatus.Processing && RetryCount < 5;
}

/// <summary>Value object tracking individual payment transaction</summary>
public class PaymentTransaction
{
    public Guid TransactionId { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string GatewayReference { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }

    private PaymentTransaction() { }

    public static PaymentTransaction Create(
        string gatewayReference,
        decimal amount,
        TransactionStatus status,
        string? failureReason = null)
    {
        return new PaymentTransaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = amount,
            Status = status,
            Timestamp = DateTime.UtcNow,
            GatewayReference = gatewayReference,
            FailureReason = failureReason
        };
    }
}
```

### Value Object: RiskData

```csharp
namespace Oms.Domain.ValueObjects;

/// <summary>
/// Immutable value object capturing risk assessment results from external risk engine.
/// Once created, risk data cannot be modified; prevents business logic drift.
/// </summary>
public class RiskData
{
    public Guid RiskId { get; private set; }
    public RiskLevel Level { get; private set; }
    public decimal RiskScore { get; private set; } // 0-100
    public List<RiskIndicator> Indicators { get; private set; } = new();
    public DateTime EvaluatedAt { get; private set; }
    public string RiskEngineVersion { get; private set; } = string.Empty;
    public bool RequiresManualReview { get; private set; }

    private RiskData() { }

    public static RiskData Create(
        RiskLevel level,
        decimal riskScore,
        List<RiskIndicator> indicators,
        string engineVersion,
        bool requiresReview)
    {
        if (riskScore < 0 || riskScore > 100)
            throw new ArgumentException("Risk score must be between 0 and 100");
        
        return new RiskData
        {
            RiskId = Guid.NewGuid(),
            Level = level,
            RiskScore = riskScore,
            Indicators = indicators,
            EvaluatedAt = DateTime.UtcNow,
            RiskEngineVersion = engineVersion,
            RequiresManualReview = requiresReview
        };
    }

    public override bool Equals(object? obj) =>
        obj is RiskData other && RiskId == other.RiskId;
    
    public override int GetHashCode() => RiskId.GetHashCode();
}

public class RiskIndicator
{
    public string IndicatorType { get; set; } = string.Empty;
    public string RiskFactor { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool IsFlagged { get; set; }
}
```

### Value Object: EnrichedOrder

```csharp
namespace Oms.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing order data enriched from PIM.
/// Contains detailed product information and pricing after enrichment.
/// </summary>
public class EnrichedOrder
{
    public Guid OrderId { get; private set; }
    public List<EnrichedOrderItem> EnrichedItems { get; private set; } = new();
    public DateTime EnrichedAt { get; private set; }
    public string PimVersion { get; private set; } = string.Empty;
    public decimal EnrichedTotalPrice { get; private set; }

    private EnrichedOrder() { }

    public static EnrichedOrder Create(
        Guid orderId,
        List<EnrichedOrderItem> items,
        string pimVersion)
    {
        return new EnrichedOrder
        {
            OrderId = orderId,
            EnrichedItems = items,
            EnrichedAt = DateTime.UtcNow,
            PimVersion = pimVersion,
            EnrichedTotalPrice = items.Sum(i => i.EnrichedPrice)
        };
    }
}

public class EnrichedOrderItem
{
    public Guid ItemId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public decimal EnrichedPrice { get; set; }
}
```

---

## Part 3: DTOs (Data Transfer Objects)

Application-layer data contracts for API and internal service communication.

### Order DTOs

```csharp
namespace Oms.Application.DTOs;

/// <summary>DTO for order creation request from API</summary>
public class CreateOrderDto
{
    public Guid CustomerId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<CreateOrderItemDto> Items { get; set; } = new();
    
    // Customer data
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public AddressDto ShippingAddress { get; set; } = new();
    public AddressDto BillingAddress { get; set; } = new();
}

public class CreateOrderItemDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>DTO for full order details (response)</summary>
public class OrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    
    public List<OrderItemDto> Items { get; set; } = new();
    public CustomerDto? Customer { get; set; }
    public PaymentDto? Payment { get; set; }
    public RiskDataDto? RiskAssessment { get; set; }
    public EnrichedOrderDto? EnrichedData { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrderItemDto
{
    public Guid ItemId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>DTO for order correction request</summary>
public class RequestCorrectionDto
{
    public Guid OrderId { get; set; }
    public List<CreateOrderItemDto> CorrectedItems { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

/// <summary>DTO for order cancellation request</summary>
public class CancelOrderDto
{
    public Guid OrderId { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
}
```

### Customer & Address DTOs

```csharp
public class CustomerDto
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
    public int PreviousOrderCount { get; set; }
    
    public AddressDto ShippingAddress { get; set; } = new();
    public AddressDto BillingAddress { get; set; } = new();
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
```

### Payment DTOs

```csharp
public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public List<PaymentTransactionDto> Transactions { get; set; } = new();
}

public class PaymentTransactionDto
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string GatewayReference { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}
```

### Risk & Enrichment DTOs

```csharp
public class RiskDataDto
{
    public Guid RiskId { get; set; }
    public string Level { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public List<RiskIndicatorDto> Indicators { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
    public string RiskEngineVersion { get; set; } = string.Empty;
    public bool RequiresManualReview { get; set; }
}

public class RiskIndicatorDto
{
    public string IndicatorType { get; set; } = string.Empty;
    public string RiskFactor { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool IsFlagged { get; set; }
}

public class EnrichedOrderDto
{
    public Guid OrderId { get; set; }
    public List<EnrichedOrderItemDto> EnrichedItems { get; set; } = new();
    public DateTime EnrichedAt { get; set; }
    public string PimVersion { get; set; } = string.Empty;
    public decimal EnrichedTotalPrice { get; set; }
}

public class EnrichedOrderItemDto
{
    public Guid ItemId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public decimal EnrichedPrice { get; set; }
}
```

---

## Part 4: Contracts (for Temporal & Messaging)

Serializable data contracts for workflow activities, signals, queries, and events.

### Activity Input/Output Contracts

```csharp
namespace Oms.Contracts.ActivityInputOutputs;

// ValidateCommerceActivity
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
    public OrderContractDto? ValidatedOrder { get; set; }
}

// CollectRiskActivity
public class CollectRiskActivityInput
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal OrderAmount { get; set; }
    public List<string> ProductCodes { get; set; } = new();
    public string CustomerEmail { get; set; } = string.Empty;
}

public class CollectRiskActivityOutput
{
    public string RiskLevel { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public List<string> RiskIndicators { get; set; } = new();
    public bool RequiresManualReview { get; set; }
}

// ValidatePaymentActivity
public class ValidatePaymentActivityInput
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentToken { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
}

public class ValidatePaymentActivityOutput
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? AuthCode { get; set; }
}

// EnrichOrderActivity
public class EnrichOrderActivityInput
{
    public Guid OrderId { get; set; }
    public List<OrderItemContractDto> Items { get; set; } = new();
}

public class EnrichOrderActivityOutput
{
    public Guid OrderId { get; set; }
    public List<EnrichedItemContractDto> EnrichedItems { get; set; } = new();
    public string PimVersion { get; set; } = string.Empty;
}

// PublishFulfillmentActivity
public class PublishFulfillmentActivityInput
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public List<FulfillmentItemDto> Items { get; set; } = new();
    public string ShippingAddress { get; set; } = string.Empty;
}

public class PublishFulfillmentActivityOutput
{
    public int KafkaPartition { get; set; }
    public long KafkaOffset { get; set; }
    public DateTime PublishedAt { get; set; }
}
```

### Workflow Signals

```csharp
namespace Oms.Contracts.WorkflowSignals;

/// <summary>Signal to cancel an order at any point in workflow</summary>
public class CancelOrderSignal
{
    public Guid OrderId { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Signal to retry order processing with corrections</summary>
public class RequestCorrectionSignal
{
    public Guid OrderId { get; set; }
    public List<OrderItemInput> CorrectedItems { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Signal for manager to approve high-risk orders</summary>
public class ApproveRiskSignal
{
    public Guid OrderId { get; set; }
    public string ManagerApprovalReason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
}
```

### Workflow Queries

```csharp
namespace Oms.Contracts.WorkflowQueries;

/// <summary>Query current order status</summary>
public class GetOrderStatusQuery
{
    public Guid OrderId { get; set; }
}

public class GetOrderStatusResult
{
    public string Status { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Query full order details</summary>
public class GetOrderDetailsQuery
{
    public Guid OrderId { get; set; }
}

public class GetOrderDetailsResult
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<OrderItemContractDto> Items { get; set; } = new();
}

/// <summary>Query payment-specific details</summary>
public class GetPaymentStatusQuery
{
    public Guid OrderId { get; set; }
}

public class GetPaymentStatusResult
{
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int RetryCount { get; set; }
    public List<string> TransactionIds { get; set; } = new();
}
```

### Kafka Event Contracts

```csharp
namespace Oms.Contracts.Events;

/// <summary>Event published to Kafka for fulfillment system</summary>
public class FulfillmentOrderEvent
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    
    public List<FulfillmentItemDto> Items { get; set; } = new();
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
}

public class FulfillmentItemDto
{
    public Guid ItemId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ShippingAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>Event published for order status changes</summary>
public class OrderStatusChangedEvent
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Mapping Between Layers

### Domain → DTO Mapping

```csharp
// AutoMapper Profile
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        // Domain to DTO
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.OrderStatus, opt => opt.MapFrom(s => s.CurrentStatus.ToString()))
            .ForMember(d => d.Items, opt => opt.MapFrom(s => s.Items));
        
        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(d => d.LineTotal, opt => opt.MapFrom(s => s.GetLineTotal()));
        
        CreateMap<Payment, PaymentDto>()
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));
        
        CreateMap<RiskData, RiskDataDto>()
            .ForMember(d => d.Level, opt => opt.MapFrom(s => s.Level.ToString()));
        
        CreateMap<EnrichedOrder, EnrichedOrderDto>();
    }
}
```

### DTO → Contract Mapping

For Temporal activities and Kafka events, use similar mapping patterns to convert between application DTOs and serializable contracts.

---

## Summary Table

| Layer | Purpose | Serializable | External Dependencies |
|-------|---------|-------------|----------------------|
| **Domain Models** | Business logic | No | Zero (encapsulated) |
| **DTOs** | Application transfer | Yes | Mapping libraries |
| **Enums** | Type-safe states | Yes | None |
| **Contracts** | Temporal/Kafka | Yes | Serialization only |

---

## Next Steps

1. Create C# files for all models in respective projects
2. Implement AutoMapper profiles for domain-to-DTO conversion
3. Implement DTO-to-Contract converters
4. Add database entity configurations (EF Core)
5. Create repository interfaces in domain layer
6. Implement repositories in infrastructure layer

