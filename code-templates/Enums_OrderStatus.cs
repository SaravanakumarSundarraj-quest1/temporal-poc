namespace Oms.Domain.Enums;

/// <summary>
/// Complete lifecycle state of an order through the fulfillment system.
/// Represents each step from creation through final completion or cancellation.
/// </summary>
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
