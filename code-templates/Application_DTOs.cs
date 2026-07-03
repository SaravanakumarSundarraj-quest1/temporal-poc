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
    public int CorrectionAttempts { get; set; }
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

/// <summary>DTO for customer information</summary>
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

/// <summary>DTO for address information</summary>
public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>DTO for payment information</summary>
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

/// <summary>DTO for risk assessment data</summary>
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

/// <summary>DTO for enriched order data from PIM</summary>
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

/// <summary>DTO for API responses with pagination</summary>
public class PaginatedDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

/// <summary>DTO for API error responses</summary>
public class ErrorDto
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
