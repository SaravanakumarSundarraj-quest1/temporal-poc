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
    public bool IsComplete { get; set; }
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
    public List<OrderItemDetailDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int CorrectionAttempts { get; set; }
}

public class OrderItemDetailDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
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
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>Query risk assessment details</summary>
public class GetRiskAssessmentQuery
{
    public Guid OrderId { get; set; }
}

public class GetRiskAssessmentResult
{
    public string RiskLevel { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public List<string> RiskIndicators { get; set; } = new();
    public bool RequiresManualReview { get; set; }
    public DateTime EvaluatedAt { get; set; }
}
