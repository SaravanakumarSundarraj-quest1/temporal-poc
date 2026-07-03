namespace Oms.Contracts.Events;

/// <summary>Event published to Kafka for fulfillment system</summary>
public class FulfillmentOrderEvent
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    
    public List<FulfillmentItemEvent> Items { get; set; } = new();
    public ShippingAddressEvent ShippingAddress { get; set; } = new();
    
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    
    // Event metadata
    public string EventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int EventVersion { get; set; } = 1;
}

public class FulfillmentItemEvent
{
    public Guid ItemId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ShippingAddressEvent
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>Event published when order status changes</summary>
public class OrderStatusChangedEvent
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    // Event metadata
    public string EventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int EventVersion { get; set; } = 1;
}

/// <summary>Event published when payment status changes</summary>
public class PaymentStatusChangedEvent
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    // Event metadata
    public string EventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int EventVersion { get; set; } = 1;
}

/// <summary>Event published when order is cancelled</summary>
public class OrderCancelledEvent
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
    
    // Event metadata
    public string EventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int EventVersion { get; set; } = 1;
}

/// <summary>Event published when order processing fails</summary>
public class OrderFailedEvent
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    
    // Event metadata
    public string EventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public int EventVersion { get; set; } = 1;
}
