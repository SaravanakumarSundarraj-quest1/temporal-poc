namespace Oms.Api.Configuration;

using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Oms.Worker;
using Oms.Temporal.Workflows;
using Oms.Temporal.Activities;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Temporal SDK dependency injection setup
/// Register Temporal client, worker, and activities
/// </summary>
public static class TemporalServiceExtensions
{
    /// <summary>Add Temporal services to DI container</summary>
    public static IServiceCollection AddTemporalServices(
        this IServiceCollection services,
        string serverAddress = "localhost:7233")
    {
        // Register Temporal client
        services.AddSingleton(async provider =>
        {
            var clientOptions = new TemporalClientOptions
            {
                TargetHost = serverAddress
            };
            return await TemporalClient.ConnectAsync(clientOptions);
        });

        // Register worker hosted service
        services.AddHostedService<TemporalWorkerHostedService>();

        // Register workflow
        services.AddScoped<IOrderProcessingWorkflow, OrderProcessingWorkflow>();

        // Register activity implementations
        services.AddScoped<IValidateCommerceActivity, ValidateCommerceActivity>();
        services.AddScoped<ICollectRiskActivity, CollectRiskActivity>();
        services.AddScoped<IValidatePaymentActivity, ValidatePaymentActivity>();
        services.AddScoped<IEnrichOrderActivity, EnrichOrderActivity>();
        services.AddScoped<IPublishFulfillmentActivity, PublishFulfillmentActivity>();
        services.AddScoped<IRequestApprovalActivity, RequestApprovalActivity>();
        services.AddScoped<IPublishEventActivity, PublishEventActivity>();

        return services;
    }
}

/// <summary>
/// Usage in Program.cs:
/// 
/// var builder = WebApplicationBuilder.CreateBuilder(args);
/// 
/// builder.Services.AddTemporalServices("temporal.example.com:7233");
/// 
/// // Other service registrations...
/// 
/// var app = builder.Build();
/// await app.RunAsync();
/// </summary>
