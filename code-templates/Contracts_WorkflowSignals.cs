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
    public List<OrderItemInputSignal> CorrectedItems { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public class OrderItemInputSignal
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Signal for manager to approve high-risk orders</summary>
public class ApproveRiskSignal
{
    public Guid OrderId { get; set; }
    public string ManagerApprovalReason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
}
