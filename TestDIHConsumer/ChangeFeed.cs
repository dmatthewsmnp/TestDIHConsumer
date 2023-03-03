using Azure.Messaging.ServiceBus;
using TestDIHConsumer.Models;

namespace TestDIHConsumer;

public static class ChangeFeed
{
	public static async Task HandleChangesAsync(
		ChangeFeedProcessorContext context,
		IReadOnlyCollection<CanonicalEngagement> changes,
		CancellationToken cancellationToken,
		IServiceProvider serviceProvider)
	{
		var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ChangeFeed));
		logger.LogInformation("Change Feed request consumed {requestCharge} RU for {count} items", context.Headers.RequestCharge, changes.Count);

		var serviceBusSender = serviceProvider.GetRequiredService<ServiceBusSender>();
		foreach (var item in changes)
		{
			var publishOutput = JsonSerializer.Serialize(
				CrudMessageEnvelope<CanonicalEngagement>.CreateEnvelope()
					.ForOperation(Operation.Update)
					.WithPayload(item)
					.ForTraceId(Guid.NewGuid().ToString())
					.WithTag("payloadType", "dm.engagement.v2")
					.Create()
			);
			await serviceBusSender.SendMessageAsync(new ServiceBusMessage(publishOutput) { SessionId = item.id.ToString() }, cancellationToken);
			logger.LogDebug("Detected operation for item with id {id}", item.id);
		}
		logger.LogDebug("Finished handling changes");
	}
	public static Task OnError(string leaseToken, Exception exception, IServiceProvider serviceProvider)
	{
		serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ChangeFeed)).LogError(exception, "Error caught from change feed");
		return Task.CompletedTask;
	}
}
