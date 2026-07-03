namespace Oms.Domain.Aggregates.Order;

using Oms.Domain.Enums;
using Oms.Domain.Exceptions;
using Oms.Domain.Aggregates.Customer;
using Oms.Domain.Aggregates.Payment;
using Oms.Domain.ValueObjects;

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
    public Customer? OrderCustomer { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();
    public Payment? OrderPayment { get; private set; }
    public RiskData? RiskAssessment { get; private set; }
    public EnrichedOrder? EnrichedData { get; private set; }
    
    // Tracking
    public int CorrectionAttempts { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    // EF Core constructor
    private Order() { }

    // ===== Factory Method =====
    
    public static Order Create(
        Guid customerId,
        string orderNumber,
        decimal totalAmount,
        Customer customer,
        DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("Order number is required");
        if (totalAmount <= 0)
            throw new ArgumentException("Total amount must be positive");
        
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
        if (string.IsNullOrWhiteSpace(productCode))
            throw new ArgumentException("Product code is required");
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

/// <summary>Repository interface for Order persistence</summary>
public interface IOrderRepository
{
    Task AddAsync(Order order);
    Task<Order?> GetByIdAsync(Guid orderId);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Guid orderId);
    Task<List<Order>> GetByCustomerIdAsync(Guid customerId);
}
