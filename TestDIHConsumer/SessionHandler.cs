using Azure.Messaging.ServiceBus;

namespace TestDIHConsumer;

public static class SessionHandler
{
	private static int _sessionCount = 0;

	public static Task SessionInitializingAsync(ProcessSessionEventArgs args, IServiceProvider serviceProvider)
	{
		int sessionCount = Interlocked.Increment(ref _sessionCount);
		serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SessionHandler))
			.LogInformation("Initializing Session: '{SessionId}' ({sessionCount} active)", args.SessionId, sessionCount);
		return Task.CompletedTask;
	}
	public static Task SessionClosingAsync(ProcessSessionEventArgs args, IServiceProvider serviceProvider)
	{
		Interlocked.Decrement(ref _sessionCount);
		serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(nameof(SessionHandler))?
			.LogInformation("Closing Session: '{SessionId}'", args.SessionId);
		return Task.CompletedTask;
	}

	public static async Task ProcessMessageAsync(ProcessSessionMessageEventArgs args, IServiceProvider serviceProvider)
	{
		var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(nameof(SessionHandler));
		try
		{
			var message = args.Message.Body.ToString();

			logger?.LogInformation("Received for Session: '{SessionId}', attempt {attempt}", args.SessionId, args.Message.DeliveryCount);
			// Deserialize event string into generic CrudMessageEnvelope, ensure payload is present:
			var envelope = CrudMessageEnvelope.Deserialize(message);
			if (envelope.payload is null)
			{
				throw new DeserializationException("No payload received");
			}

			// Retrieve payloadType from envelope tags:
			string payloadType = envelope.envelopeTags != null && envelope.envelopeTags.ContainsKey("payloadType")
				? envelope.envelopeTags["payloadType"] : throw new DeserializationException("Missing required tag payloadType");

			// Determine timestamp that applies to this change event - earlier of envelope time and enqueued time (to allow
			// connectors to publish "old" events, but prevent them from writing future-dated events):
			var payloadValueDate = envelope.eventDateTime < args.Message.EnqueuedTime ? envelope.eventDateTime.Value : args.Message.EnqueuedTime;

			var payloadHandler = serviceProvider.GetRequiredService<IPayloadHandlerFactory>().GetPayloadHandler(payloadType);
			await payloadHandler.HandlePayload(
				envelope.payload.Value,
				payloadValueDate,
				envelope.eventId!.Value,
				envelope.operation!.Value,
				args.CancellationToken);

			await args.CompleteMessageAsync(args.Message, args.CancellationToken);
		}
		catch (DeserializationException dex)
		{
			logger?.LogError("Deserialization failed on session: '{SessionId}' ({message})", args.SessionId, dex.Message);
			await args.DeadLetterMessageAsync(args.Message, nameof(DeserializationException), dex.Message, args.CancellationToken);
		}
		catch (Exception ex)
		{
			logger?.LogError(ex, "Exception on session: '{SessionId}' attempt {attempt}", args.SessionId, args.Message.DeliveryCount);
			if (args.Message.DeliveryCount >= 5)
			{
				await args.DeadLetterMessageAsync(args.Message, ex.GetType().Name, ex.Message, args.CancellationToken);
			}
			else
			{
				await args.AbandonMessageAsync(args.Message, null, args.CancellationToken);
			}
		}
	}

	public static Task ProcessErrorAsync(ProcessErrorEventArgs args, IServiceProvider serviceProvider)
	{
		serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SessionHandler))
			.LogError(args.Exception, "Service bus exception caught");
		return Task.CompletedTask;
	}
}
