namespace Oms.Temporal.ErrorHandling;

using Temporalio.Client;
using Temporalio.Exceptions;
using Microsoft.Extensions.Logging;

/// <summary>Activity error handling base class</summary>
public abstract class BaseActivityWithErrorHandling
{
    protected readonly ILogger Logger;

    protected BaseActivityWithErrorHandling(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>Determine if error is retryable</summary>
    protected bool IsRetryableError(Exception ex)
    {
        // Retryable: network, timeout, transient service errors
        if (ex is HttpRequestException || ex is TimeoutException)
            return true;

        if (ex.InnerException is HttpRequestException or TimeoutException)
            return true;

        // Non-retryable: validation errors, not found, access denied
        if (ex is ArgumentException || ex is InvalidOperationException)
            return false;

        // Default to retryable for safety
        return true;
    }

    /// <summary>Handle activity failure with appropriate exception</summary>
    protected void HandleActivityFailure(Exception ex, string activityName)
    {
        Logger.LogError(ex, "Activity {ActivityName} failed", activityName);

        bool isRetryable = IsRetryableError(ex);

        if (isRetryable)
        {
            // Temporal will retry based on retry policy
            throw new ApplicationException($"{activityName} failed temporarily", ex);
        }
        else
        {
            // Non-retryable error - fail immediately
            throw new InvalidOperationException($"{activityName} failed permanently", ex);
        }
    }
}

/// <summary>Activity retry and timeout policies</summary>
public static class ActivityPolicies
{
    /// <summary>Fast external API calls - 30s timeout, 3 retries</summary>
    public static ActivityOptions FastApiPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(30),
        StartToCloseTimeout = TimeSpan.FromSeconds(25),
        HeartbeatTimeout = TimeSpan.FromSeconds(10),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffCoefficient = 2.0,
            MaximumInterval = TimeSpan.FromSeconds(30),
            MaximumAttempts = 3
        }
    };

    /// <summary>Slow API calls - 60s timeout, 3 retries</summary>
    public static ActivityOptions SlowApiPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(60),
        StartToCloseTimeout = TimeSpan.FromSeconds(55),
        HeartbeatTimeout = TimeSpan.FromSeconds(20),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(2),
            BackoffCoefficient = 2.0,
            MaximumInterval = TimeSpan.FromSeconds(60),
            MaximumAttempts = 3
        }
    };

    /// <summary>Message publishing - 30s timeout, 5 retries (idempotent)</summary>
    public static ActivityOptions PublishingPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(30),
        StartToCloseTimeout = TimeSpan.FromSeconds(25),
        HeartbeatTimeout = TimeSpan.FromSeconds(5),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            BackoffCoefficient = 1.0,
            MaximumInterval = TimeSpan.FromSeconds(5),
            MaximumAttempts = 5
        }
    };

    /// <summary>Long-running operations - 90s timeout, 2 retries</summary>
    public static ActivityOptions LongRunningPolicy => new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(90),
        StartToCloseTimeout = TimeSpan.FromSeconds(85),
        HeartbeatTimeout = TimeSpan.FromSeconds(30),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(5),
            BackoffCoefficient = 2.0,
            MaximumInterval = TimeSpan.FromSeconds(60),
            MaximumAttempts = 2
        }
    };

    /// <summary>Approval workflows - no timeout (heartbeat only)</summary>
    public static ActivityOptions ApprovalPolicy => new()
    {
        ScheduleToCloseTimeout = null,
        StartToCloseTimeout = null,
        HeartbeatTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumAttempts = 1
        }
    };
}

/// <summary>Workflow error handling utilities</summary>
public static class WorkflowErrorHandling
{
    /// <summary>Execute activity with standardized error handling</summary>
    public static async Task<T> ExecuteActivityWithErrorHandlingAsync<T>(
        Func<Task<T>> activityFunc,
        string activityName,
        ILogger logger)
    {
        try
        {
            return await activityFunc();
        }
        catch (ActivityFailureException ex)
        {
            logger.LogError(ex, "Activity {ActivityName} failed after retries", activityName);
            throw;
        }
        catch (ApplicationException ex) when (ex.Message.Contains("temporarily"))
        {
            logger.LogWarning(ex, "Retryable error in {ActivityName}", activityName);
            throw;
        }
    }

    /// <summary>Handle workflow timeout</summary>
    public static void HandleWorkflowTimeout(string orderId, TimeSpan elapsed, ILogger logger)
    {
        logger.LogWarning("Workflow timeout for order {OrderId} after {Elapsed}ms", orderId, elapsed.TotalMilliseconds);
    }

    /// <summary>Handle workflow cancellation</summary>
    public static void HandleWorkflowCancellation(string orderId, string reason, ILogger logger)
    {
        logger.LogInformation("Workflow cancelled for order {OrderId}: {Reason}", orderId, reason);
    }
}
