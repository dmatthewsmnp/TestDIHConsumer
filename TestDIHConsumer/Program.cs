using Azure.Identity;
using Azure.Messaging.ServiceBus;
using TestDIHConsumer;
using TestDIHConsumer.Models;
using TestDIHConsumer.PayloadHandlers.MPM;

string dbName = Environment.GetEnvironmentVariable("DBName") ?? throw new ArgumentException("DBName not configured");
string containerName = Environment.GetEnvironmentVariable("DBContainerEngagement") ?? throw new ArgumentException("DBContainerEngagement not configured");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCosmosContainerHandle(
	cosmosEndpoint: Environment.GetEnvironmentVariable("CosmosDB__accountEndpoint") ?? throw new ArgumentException("CosmosDB__accountEndpoint not configured"),
	cosmosClientId: Environment.GetEnvironmentVariable("CosmosDB__clientId"), // Optional; not required if using system-assigned managed identity
	defaultDbName: dbName);
builder.Services.AddPayloadHandlerFactory(builder =>
{
	builder.Register<MpmEngagementV2PayloadHandler>(MpmEngagementV2PayloadHandler.PayloadType);
});

// Create singleton service bus client and SessionProcessor:
await using var serviceBusClient = new ServiceBusClient(
	fullyQualifiedNamespace: Environment.GetEnvironmentVariable("ServiceBus__fullyQualifiedNamespace") ?? throw new ArgumentException("ServiceBus__fullyQualifiedNamespace not configured"),
	credential: new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = Environment.GetEnvironmentVariable("ServiceBus__clientId") }), // clientId optional (not required if using system-managed identity)
	options: new ServiceBusClientOptions()
	{
	});

builder.Services.AddSingleton(serviceBusClient);
builder.Services.AddSingleton(isp => isp.GetRequiredService<ServiceBusClient>().CreateSender(Environment.GetEnvironmentVariable("TopicName") ?? throw new ArgumentException("TopicName not configured")));

var app = builder.Build();
app.MapGet("/healthz", (HttpContext httpContext, ILoggerFactory loggerFactory) =>
{
	var logger = loggerFactory.CreateLogger("Program");
	using var logscope = logger.BeginScope(new Dictionary<string, object?>() { { "ip", httpContext.Connection.RemoteIpAddress } });
	logger.LogInformation("Health check at {timestamp}", DateTimeOffset.UtcNow);
	return Results.Ok();
});

var cosmosClient = app.Services.GetRequiredService<CosmosClient>();
var changeFeedProcessor = cosmosClient.GetContainer(dbName, containerName)
	.GetChangeFeedProcessorBuilder(
		processorName: "TestDIHConsumer",
		onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<CanonicalEngagement> changes, CancellationToken cancellationToken) => ChangeFeed.HandleChangesAsync(context, changes, cancellationToken, app.Services)
	)
	.WithErrorNotification((string leaseToken, Exception exception) => ChangeFeed.OnError(leaseToken, exception, app.Services))
	.WithInstanceName("consoleHost") // TODO: should be unique per process
	.WithLeaseContainer(cosmosClient.GetContainer(dbName, $"{containerName}Leases"))
	.Build();

await changeFeedProcessor.StartAsync();

await using (var serviceBusSessionProcessor = serviceBusClient.CreateSessionProcessor(
	queueName: Environment.GetEnvironmentVariable("QueueName") ?? throw new ArgumentException("QueueName not configured"),
	options: new ServiceBusSessionProcessorOptions()
	{
		AutoCompleteMessages = false,
		Identifier = "TestDIHConsumer",
		MaxConcurrentCallsPerSession = 1,
		MaxConcurrentSessions = 32,
		PrefetchCount = 20,
		ReceiveMode = ServiceBusReceiveMode.PeekLock,
		SessionIdleTimeout = TimeSpan.FromSeconds(1), // In most cases, a given SessionId should have only one message at a time
	}))
{
	// Assign delegates to SessionProcessor events and start processor:
	serviceBusSessionProcessor.SessionInitializingAsync += args => SessionHandler.SessionInitializingAsync(args, app.Services);
	serviceBusSessionProcessor.SessionClosingAsync += args => SessionHandler.SessionClosingAsync(args, app.Services);
	serviceBusSessionProcessor.ProcessMessageAsync += args =>
	{
		using var scope = app.Services.CreateScope();
		return SessionHandler.ProcessMessageAsync(args, scope.ServiceProvider);
	};
	serviceBusSessionProcessor.ProcessErrorAsync += args => SessionHandler.ProcessErrorAsync(args, app.Services);
	await serviceBusSessionProcessor.StartProcessingAsync(app.Lifetime.ApplicationStopping);

	// Run for lifetime of app:
	await app.RunAsync();

	// Allow up to 2 seconds for clean shutdown of session processor before it is disposed:
	using var cleanupCancellation = new CancellationTokenSource(2000);
	try
	{
		await serviceBusSessionProcessor.StopProcessingAsync(cleanupCancellation.Token);
	}
	catch (TaskCanceledException)
	{
		app.Logger.LogWarning("ServiceBusSessionProcessor clean shutdown timed out");
	}
}

await changeFeedProcessor.StopAsync();