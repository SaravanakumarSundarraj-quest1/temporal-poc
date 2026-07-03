namespace Oms.Worker;

using Temporalio.Client;
using Temporalio.Worker;
using Oms.Temporal.Workflows;
using Oms.Temporal.Activities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service managing Temporal worker lifecycle
/// Starts worker on service start, gracefully shuts down on service stop
/// </summary>
public class TemporalWorkerHostedService : BackgroundService
{
    private readonly ITemporalClient _temporalClient;
    private readonly ILogger<TemporalWorkerHostedService> _logger;
    private TemporalWorker? _worker;

    public TemporalWorkerHostedService(
        ITemporalClient temporalClient,
        ILogger<TemporalWorkerHostedService> logger)
    {
        _temporalClient = temporalClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Temporal worker...");

        try
        {
            var options = new TemporalWorkerOptions()
                .AddWorkflow<OrderProcessingWorkflow>()
                .AddActivity<ValidateCommerceActivity>()
                .AddActivity<CollectRiskActivity>()
                .AddActivity<ValidatePaymentActivity>()
                .AddActivity<EnrichOrderActivity>()
                .AddActivity<PublishFulfillmentActivity>()
                .AddActivity<RequestApprovalActivity>()
                .AddActivity<PublishEventActivity>();

            _worker = new TemporalWorker(
                _temporalClient,
                taskQueue: "OMS_QUEUE",
                options);

            await _worker.ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Temporal worker stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Temporal worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Temporal worker...");
        
        if (_worker != null)
        {
            await _worker.ShutdownAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>Task queue definitions</summary>
public static class TaskQueues
{
    /// <summary>Main workflow task queue - processes order workflows</summary>
    public const string OmsQueue = "OMS_QUEUE";

    /// <summary>External services queue - prevents head-of-line blocking</summary>
    public const string CommerceQueue = "COMMERCE_QUEUE";

    /// <summary>Event publishing queue - Kafka publications</summary>
    public const string FulfillmentQueue = "FULFILLMENT_QUEUE";

    /// <summary>Approval workflows - human approvals</summary>
    public const string ApprovalQueue = "APPROVAL_QUEUE";
}

/// <summary>Worker configuration for different task queues</summary>
public static class WorkerConfiguration
{
    /// <summary>Main workflow worker - 100 concurrent workflows</summary>
    public static TemporalWorkerOptions ConfigureMainWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 10;
        options.MaxConcurrentWorkflowTaskExecutors = 100;
        options.MaxConcurrentLocalActivityExecutors = 50;

        return options;
    }

    /// <summary>Commerce queue worker - 20 concurrent external calls</summary>
    public static TemporalWorkerOptions ConfigureCommerceWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 20;
        options.MaxConcurrentWorkflowTaskExecutors = 5;
        options.MaxConcurrentLocalActivityExecutors = 10;

        return options;
    }

    /// <summary>Fulfillment queue worker - 50 concurrent publications</summary>
    public static TemporalWorkerOptions ConfigureFulfillmentWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 50;
        options.MaxConcurrentWorkflowTaskExecutors = 5;
        options.MaxConcurrentLocalActivityExecutors = 20;

        return options;
    }

    /// <summary>Approval queue worker - 5 concurrent approvals</summary>
    public static TemporalWorkerOptions ConfigureApprovalWorker(TemporalWorkerOptions options)
    {
        options.MaxConcurrentActivityTaskExecutors = 5;
        options.MaxConcurrentWorkflowTaskExecutors = 5;
        options.MaxConcurrentLocalActivityExecutors = 2;
        options.HeartbeatTimeout = TimeSpan.FromMinutes(5);

        return options;
    }
}
