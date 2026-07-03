namespace Oms.Temporal.Workflows;

using Temporalio.Workflows;
using Oms.Contracts.ActivityInputOutputs;
using Oms.Contracts.WorkflowSignals;
using Oms.Contracts.WorkflowQueries;
using Oms.Application.DTOs;

/// <summary>
/// Main order processing workflow interface.
/// Orchestrates all activities and manages signals/queries for order processing.
/// </summary>
[Workflow]
public interface IOrderProcessingWorkflow
{
    /// <summary>Main workflow entry point - orchestrates complete order processing</summary>
    [WorkflowRun]
    Task<OrderProcessingResult> ProcessOrderAsync(ProcessOrderInput input);

    /// <summary>Signal: Cancel order at any point in workflow</summary>
    [WorkflowSignal]
    Task HandleCancelOrderSignalAsync(CancelOrderSignal signal);

    /// <summary>Signal: Request correction and retry validation</summary>
    [WorkflowSignal]
    Task HandleRequestCorrectionSignalAsync(RequestCorrectionSignal signal);

    /// <summary>Signal: Manager approval for high-risk orders</summary>
    [WorkflowSignal]
    Task HandleApproveRiskSignalAsync(ApproveRiskSignal signal);

    /// <summary>Query: Get current order status</summary>
    [WorkflowQuery]
    Task<GetOrderStatusResult> GetOrderStatusAsync(GetOrderStatusQuery query);

    /// <summary>Query: Get full order details and enrichment data</summary>
    [WorkflowQuery]
    Task<GetOrderDetailsResult> GetOrderDetailsAsync(GetOrderDetailsQuery query);

    /// <summary>Query: Get payment-specific information</summary>
    [WorkflowQuery]
    Task<GetPaymentStatusResult> GetPaymentStatusAsync(GetPaymentStatusQuery query);

    /// <summary>Query: Get risk assessment details</summary>
    [WorkflowQuery]
    Task<GetRiskAssessmentResult> GetRiskAssessmentAsync(GetRiskAssessmentQuery query);
}

/// <summary>Workflow input containing order and customer data</summary>
public class ProcessOrderInput
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerSegment { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public List<OrderItemInput> Items { get; set; } = new();
    
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public string PaymentToken { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromHours(24);
    public string TaskQueue { get; set; } = "OMS_QUEUE";
}

/// <summary>Workflow output containing final order state</summary>
public class OrderProcessingResult
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string FinalStatus { get; set; } = string.Empty;
    
    public OrderDto? Order { get; set; }
    public PaymentDto? Payment { get; set; }
    public RiskDataDto? RiskAssessment { get; set; }
    public EnrichedOrderDto? EnrichedData { get; set; }
    
    public DateTime CompletedAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Shipping address for workflow input</summary>
public class ShippingAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>Shared state maintained across workflow execution</summary>
public class WorkflowExecutionContext
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    
    public OrderDto? Order { get; set; }
    public PaymentDto? Payment { get; set; }
    public RiskDataDto? RiskAssessment { get; set; }
    public EnrichedOrderDto? EnrichedData { get; set; }
    
    public bool IsCancelled { get; set; }
    public bool IsCorrectionRequested { get; set; }
    public bool IsRiskApproved { get; set; }
    public int CorrectionAttempts { get; set; }
    
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
