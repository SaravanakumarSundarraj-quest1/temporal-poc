namespace Oms.Domain.Enums;

/// <summary>Payment state throughout authorization and capture lifecycle</summary>
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

/// <summary>Classification of order risk by external risk engine</summary>
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

/// <summary>Customer classification for business logic routing</summary>
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

/// <summary>Individual payment transaction outcome</summary>
public enum TransactionStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Declined = 3,
    Timeout = 4
}
