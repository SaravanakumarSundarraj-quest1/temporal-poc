namespace Oms.Temporal.Activities;

using Temporalio.Activities;
using Oms.Contracts.ActivityInputOutputs;

/// <summary>Base interface for all activities</summary>
public interface IBaseActivity
{
    string ActivityName { get; }
    int ActivityVersion { get; }
}

/// <summary>
/// Validate order against commerce system
/// Timeout: 30s | Retries: 3 | Heartbeat: 10s
/// </summary>
[Activity]
public interface IValidateCommerceActivity : IBaseActivity
{
    [ActivityMethod(Name = "ValidateCommerceActivity")]
    Task<ValidateCommerceActivityOutput> ExecuteAsync(ValidateCommerceActivityInput input);
}

/// <summary>
/// Collect risk assessment from external risk engine
/// Timeout: 45s | Retries: 2 | Heartbeat: 15s
/// </summary>
[Activity]
public interface ICollectRiskActivity : IBaseActivity
{
    [ActivityMethod(Name = "CollectRiskActivity")]
    Task<CollectRiskActivityOutput> ExecuteAsync(CollectRiskActivityInput input);
}

/// <summary>
/// Validate and authorize payment with gateway
/// Timeout: 60s | Retries: 3 | Heartbeat: 20s
/// </summary>
[Activity]
public interface IValidatePaymentActivity : IBaseActivity
{
    [ActivityMethod(Name = "ValidatePaymentActivity")]
    Task<ValidatePaymentActivityOutput> ExecuteAsync(ValidatePaymentActivityInput input);
}

/// <summary>
/// Enrich order with PIM (Product Information Management) data
/// Timeout: 90s | Retries: 2 | Heartbeat: 30s
/// </summary>
[Activity]
public interface IEnrichOrderActivity : IBaseActivity
{
    [ActivityMethod(Name = "EnrichOrderActivity")]
    Task<EnrichOrderActivityOutput> ExecuteAsync(EnrichOrderActivityInput input);
}

/// <summary>
/// Publish order to Kafka for fulfillment system
/// Timeout: 30s | Retries: 5 | Heartbeat: 10s
/// </summary>
[Activity]
public interface IPublishFulfillmentActivity : IBaseActivity
{
    [ActivityMethod(Name = "PublishFulfillmentActivity")]
    Task<PublishFulfillmentActivityOutput> ExecuteAsync(PublishFulfillmentActivityInput input);
}

/// <summary>
/// Send approval request to manager
/// Timeout: No timeout (waits for human) | Retries: 1 | Heartbeat: 60s
/// </summary>
[Activity]
public interface IRequestApprovalActivity : IBaseActivity
{
    [ActivityMethod(Name = "RequestApprovalActivity")]
    Task<RequestApprovalActivityOutput> ExecuteAsync(RequestApprovalActivityInput input);
}

/// <summary>
/// Publish domain events to Kafka
/// Timeout: 20s | Retries: 3 | Heartbeat: 5s
/// </summary>
[Activity]
public interface IPublishEventActivity : IBaseActivity
{
    [ActivityMethod(Name = "PublishEventActivity")]
    Task<PublishEventActivityOutput> ExecuteAsync(PublishEventActivityInput input);
}

/// <summary>Input for PublishEventActivity</summary>
public class PublishEventActivityInput
{
    public string EventType { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? EventData { get; set; }
}

/// <summary>Output from PublishEventActivity</summary>
public class PublishEventActivityOutput
{
    public int KafkaPartition { get; set; }
    public long KafkaOffset { get; set; }
    public DateTime PublishedAt { get; set; }
}

// ===== Activity Implementations (Skeleton) =====

public class ValidateCommerceActivity : IValidateCommerceActivity
{
    public string ActivityName => "ValidateCommerceActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<ValidateCommerceActivityOutput> ExecuteAsync(ValidateCommerceActivityInput input)
    {
        throw new NotImplementedException();
    }
}

public class CollectRiskActivity : ICollectRiskActivity
{
    public string ActivityName => "CollectRiskActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<CollectRiskActivityOutput> ExecuteAsync(CollectRiskActivityInput input)
    {
        throw new NotImplementedException();
    }
}

public class ValidatePaymentActivity : IValidatePaymentActivity
{
    public string ActivityName => "ValidatePaymentActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<ValidatePaymentActivityOutput> ExecuteAsync(ValidatePaymentActivityInput input)
    {
        throw new NotImplementedException();
    }
}

public class EnrichOrderActivity : IEnrichOrderActivity
{
    public string ActivityName => "EnrichOrderActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<EnrichOrderActivityOutput> ExecuteAsync(EnrichOrderActivityInput input)
    {
        throw new NotImplementedException();
    }
}

public class PublishFulfillmentActivity : IPublishFulfillmentActivity
{
    public string ActivityName => "PublishFulfillmentActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<PublishFulfillmentActivityOutput> ExecuteAsync(PublishFulfillmentActivityInput input)
    {
        throw new NotImplementedException();
    }
}

public class RequestApprovalActivity : IRequestApprovalActivity
{
    public string ActivityName => "RequestApprovalActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<RequestApprovalActivityOutput> ExecuteAsync(RequestApprovalActivityInput input)
    {
        throw new NotImplementedException();
    }
}

public class PublishEventActivity : IPublishEventActivity
{
    public string ActivityName => "PublishEventActivity";
    public int ActivityVersion => 1;

    [Activity]
    public async Task<PublishEventActivityOutput> ExecuteAsync(PublishEventActivityInput input)
    {
        throw new NotImplementedException();
    }
}
