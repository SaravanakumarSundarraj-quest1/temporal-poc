namespace Oms.Contracts.ActivityInputOutputs;

/// <summary>Input for ValidateCommerceActivity</summary>
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

/// <summary>Output from ValidateCommerceActivity</summary>
public class ValidateCommerceActivityOutput
{
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public OrderContractDto? ValidatedOrder { get; set; }
}

public class OrderContractDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public List<OrderItemInput> Items { get; set; } = new();
    public decimal CalculatedTotal { get; set; }
}

// ===== CollectRiskActivity =====

/// <summary>Input for CollectRiskActivity</summary>
public class CollectRiskActivityInput
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal OrderAmount { get; set; }
    public List<string> ProductCodes { get; set; } = new();
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerSegment { get; set; } = string.Empty;
}

/// <summary>Output from CollectRiskActivity</summary>
public class CollectRiskActivityOutput
{
    public string RiskLevel { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public List<string> RiskIndicators { get; set; } = new();
    public bool RequiresManualReview { get; set; }
    public string RiskEngineVersion { get; set; } = string.Empty;
}

// ===== ValidatePaymentActivity =====

/// <summary>Input for ValidatePaymentActivity</summary>
public class ValidatePaymentActivityInput
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentToken { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
}

/// <summary>Output from ValidatePaymentActivity</summary>
public class ValidatePaymentActivityOutput
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? AuthCode { get; set; }
    public DateTime ProcessedAt { get; set; }
}

// ===== EnrichOrderActivity =====

/// <summary>Input for EnrichOrderActivity</summary>
public class EnrichOrderActivityInput
{
    public Guid OrderId { get; set; }
    public List<OrderItemContractDto> Items { get; set; } = new();
}

public class OrderItemContractDto
{
    public Guid ItemId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Output from EnrichOrderActivity</summary>
public class EnrichOrderActivityOutput
{
    public Guid OrderId { get; set; }
    public List<EnrichedItemContractDto> EnrichedItems { get; set; } = new();
    public string PimVersion { get; set; } = string.Empty;
    public decimal EnrichedTotalPrice { get; set; }
}

public class EnrichedItemContractDto
{
    public Guid ItemId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public decimal EnrichedPrice { get; set; }
}

// ===== PublishFulfillmentActivity =====

/// <summary>Input for PublishFulfillmentActivity</summary>
public class PublishFulfillmentActivityInput
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public List<FulfillmentItemDto> Items { get; set; } = new();
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public decimal TotalAmount { get; set; }
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

/// <summary>Output from PublishFulfillmentActivity</summary>
public class PublishFulfillmentActivityOutput
{
    public int KafkaPartition { get; set; }
    public long KafkaOffset { get; set; }
    public DateTime PublishedAt { get; set; }
    public string TopicName { get; set; } = string.Empty;
}

// ===== RequestApprovalActivity =====

/// <summary>Input for RequestApprovalActivity</summary>
public class RequestApprovalActivityInput
{
    public Guid OrderId { get; set; }
    public string ApprovalType { get; set; } = string.Empty; // "RiskOverride", "PaymentRetry"
    public string Reason { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>Output from RequestApprovalActivity</summary>
public class RequestApprovalActivityOutput
{
    public string ApprovalRequestId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public int NotificationsCreated { get; set; }
}
