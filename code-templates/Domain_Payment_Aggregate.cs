namespace Oms.Domain.Aggregates.Payment;

using Oms.Domain.Enums;

/// <summary>
/// Payment aggregate - manages payment lifecycle from authorization to capture/refund.
/// Tracks transaction history and supports retries and partial payments.
/// </summary>
public class Payment
{
    public Guid PaymentId { get; private set; }
    public Guid OrderId { get; private set; }
    
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    
    public PaymentStatus Status { get; private set; }
    public string Gateway { get; private set; } = string.Empty;
    public string TransactionId { get; private set; } = string.Empty;
    
    public DateTime? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    
    public List<PaymentTransaction> Transactions { get; private set; } = new();

    // EF Core constructor
    private Payment() { }

    public static Payment Create(Guid orderId, decimal amount, string gateway)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");
        if (string.IsNullOrWhiteSpace(gateway))
            throw new ArgumentException("Gateway is required");
        
        return new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Gateway = gateway,
            Status = PaymentStatus.Pending,
            RetryCount = 0
        };
    }

    public void SetProcessing()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Can only process pending payments");
        Status = PaymentStatus.Processing;
    }

    public void SetAuthorized(string transactionId)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Processing)
            throw new InvalidOperationException("Cannot authorize from current state");
        
        Status = PaymentStatus.Authorized;
        TransactionId = transactionId;
        ProcessedAt = DateTime.UtcNow;
        
        Transactions.Add(PaymentTransaction.Create(
            transactionId,
            Amount,
            TransactionStatus.Success
        ));
    }

    public void IncrementRetry()
    {
        RetryCount++;
        Status = PaymentStatus.Processing;
    }

    public void SetFailed(string failureReason)
    {
        Status = PaymentStatus.Failed;
        Transactions.Add(PaymentTransaction.Create(
            TransactionId,
            Amount,
            TransactionStatus.Failed,
            failureReason
        ));
    }

    public void SetRefunded()
    {
        if (Status != PaymentStatus.Authorized && Status != PaymentStatus.Captured)
            throw new InvalidOperationException("Can only refund authorized or captured payments");
        
        Status = PaymentStatus.Refunded;
    }

    public bool RequiresRetry() => Status == PaymentStatus.Processing && RetryCount < 5;
}

/// <summary>Value object tracking individual payment transaction</summary>
public class PaymentTransaction
{
    public Guid TransactionId { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionStatus Status { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string GatewayReference { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }

    // EF Core constructor
    private PaymentTransaction() { }

    public static PaymentTransaction Create(
        string gatewayReference,
        decimal amount,
        TransactionStatus status,
        string? failureReason = null)
    {
        return new PaymentTransaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = amount,
            Status = status,
            Timestamp = DateTime.UtcNow,
            GatewayReference = gatewayReference,
            FailureReason = failureReason
        };
    }
}

/// <summary>Repository interface for Payment persistence</summary>
public interface IPaymentRepository
{
    Task AddAsync(Payment payment);
    Task<Payment?> GetByIdAsync(Guid paymentId);
    Task UpdateAsync(Payment payment);
    Task<Payment?> GetByOrderIdAsync(Guid orderId);
}
