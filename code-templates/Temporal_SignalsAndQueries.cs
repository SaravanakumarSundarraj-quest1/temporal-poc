namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;
using Oms.Contracts.ActivityInputOutputs;
using Oms.Contracts.WorkflowSignals;
using Oms.Contracts.WorkflowQueries;
using Microsoft.Extensions.Logging;

/// <summary>
/// Signal handlers for workflow control
/// Handle cancellation, corrections, and manager approvals
/// </summary>
public partial class OrderProcessingWorkflow : IOrderProcessingWorkflow
{
    private readonly WorkflowExecutionContext _context = new();
    private readonly ILogger _logger = null!; // Injected

    // Signal channels for async communication
    private Channel<CancelOrderSignal>? _cancelChannel;
    private Channel<RequestCorrectionSignal>? _correctionChannel;
    private Channel<ApproveRiskSignal>? _approvalChannel;

    /// <summary>Handle cancellation signal - terminate workflow gracefully</summary>
    [WorkflowSignal(Name = "CancelOrderSignal")]
    public async Task HandleCancelOrderSignalAsync(CancelOrderSignal signal)
    {
        _context.IsCancelled = true;
        _context.CancellationReason = signal.CancellationReason;
        
        await _cancelChannel!.Writer.WriteAsync(signal);
    }

    /// <summary>Handle correction signal - retry validation with corrected data</summary>
    [WorkflowSignal(Name = "RequestCorrectionSignal")]
    public async Task HandleRequestCorrectionSignalAsync(RequestCorrectionSignal signal)
    {
        _context.IsCorrectionRequested = true;
        _context.CorrectionAttempts++;
        
        await _correctionChannel!.Writer.WriteAsync(signal);
    }

    /// <summary>Handle manager approval for high-risk orders</summary>
    [WorkflowSignal(Name = "ApproveRiskSignal")]
    public async Task HandleApproveRiskSignalAsync(ApproveRiskSignal signal)
    {
        _context.IsRiskApproved = true;
        
        await _approvalChannel!.Writer.WriteAsync(signal);
    }

    // ===== Query Handlers =====

    /// <summary>Query: Get current order status</summary>
    [WorkflowQuery(Name = "GetOrderStatus")]
    public async Task<GetOrderStatusResult> GetOrderStatusAsync(GetOrderStatusQuery query)
    {
        return await Task.FromResult(new GetOrderStatusResult
        {
            Status = _context.CurrentStatus,
            UpdatedAt = DateTime.UtcNow,
            IsComplete = _context.CompletedAt.HasValue
        });
    }

    /// <summary>Query: Get full order details</summary>
    [WorkflowQuery(Name = "GetOrderDetails")]
    public async Task<GetOrderDetailsResult> GetOrderDetailsAsync(GetOrderDetailsQuery query)
    {
        return await Task.FromResult(new GetOrderDetailsResult
        {
            OrderId = _context.OrderId,
            OrderNumber = _context.OrderNumber,
            Status = _context.CurrentStatus,
            TotalAmount = _context.Order?.TotalAmount ?? 0,
            Items = _context.Order?.Items.Select(i => new OrderItemDetailDto
            {
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList() ?? new(),
            CreatedAt = _context.CreatedAt,
            ExpiresAt = _context.CreatedAt.AddHours(24),
            CorrectionAttempts = _context.CorrectionAttempts
        });
    }

    /// <summary>Query: Get payment status</summary>
    [WorkflowQuery(Name = "GetPaymentStatus")]
    public async Task<GetPaymentStatusResult> GetPaymentStatusAsync(GetPaymentStatusQuery query)
    {
        return await Task.FromResult(new GetPaymentStatusResult
        {
            PaymentStatus = _context.Payment?.Status ?? "NotInitiated",
            Amount = _context.Payment?.Amount ?? 0,
            RetryCount = _context.Payment?.RetryCount ?? 0,
            TransactionIds = _context.Payment?.Transactions
                .Select(t => t.TransactionId.ToString())
                .ToList() ?? new(),
            ProcessedAt = _context.Payment?.ProcessedAt
        });
    }

    /// <summary>Query: Get risk assessment</summary>
    [WorkflowQuery(Name = "GetRiskAssessment")]
    public async Task<GetRiskAssessmentResult> GetRiskAssessmentAsync(GetRiskAssessmentQuery query)
    {
        return await Task.FromResult(new GetRiskAssessmentResult
        {
            RiskLevel = _context.RiskAssessment?.Level ?? "NotAssessed",
            RiskScore = _context.RiskAssessment?.RiskScore ?? 0,
            RiskIndicators = _context.RiskAssessment?.Indicators
                .Select(i => i.RiskFactor)
                .ToList() ?? new(),
            RequiresManualReview = _context.RiskAssessment?.RequiresManualReview ?? false,
            EvaluatedAt = _context.RiskAssessment?.EvaluatedAt ?? DateTime.UtcNow
        });
    }
}

/// <summary>Helper class for order item details in queries</summary>
public class OrderItemDetailDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
