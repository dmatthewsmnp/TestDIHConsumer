using System.ComponentModel.DataAnnotations;
using TestDIHConsumer.Models;

namespace TestDIHConsumer.PayloadHandlers.MPM;

/// <summary>
/// IPayloadHandler for applying updates from an mpm/engagement/v2 payload
/// </summary>
public class MpmEngagementV2PayloadHandler : IPayloadHandler
{
	public const string PayloadType = "mpm.engagement.v2";

	#region Fields and constructor
	private readonly ICosmosContainerHandle _cosmosContainerHandle;
	private readonly ILogger? _logger;
	public MpmEngagementV2PayloadHandler(
		ICosmosContainerHandle cosmosContainerHandle, ILoggerFactory? loggerFactory = null)
	{
		_cosmosContainerHandle = cosmosContainerHandle.SelectContainer(
			Environment.GetEnvironmentVariable("DBContainerEngagement") ?? throw new InvalidOperationException("DBContainerEngagement not configured"));
		_logger = loggerFactory?.CreateLogger<MpmEngagementV2PayloadHandler>();
	}
	#endregion

	/// <inheritdoc/>
	public async Task HandlePayload(
		JsonElement payload,
		DateTimeOffset payloadValueDate,
		Guid eventId,
		Operation operation,
		CancellationToken cancellationToken = default)
	{
		// Deserialize Json into payload object and validate:
		var mpmPayload = payload.Deserialize<MpmEngagementV2CrudPayload>()
			?? throw new DeserializationException($"Failed to deserialize {PayloadType} payload");
		try
		{
			var validationContext = new ValidationContext(mpmPayload);
			Validator.ValidateObject(mpmPayload, validationContext, true);
		}
		catch (Exception ex)
		{
			throw new DeserializationException($"Invalid {PayloadType} payload", ex);
		}

		#region Load existing CanonicalEngagement document from database or create new
		CanonicalEngagement? canonicalEngagement;
		var upsertOptions = new ItemRequestOptions { EnableContentResponseOnWrite = false }; // Don't need to see resulting doc
		bool engagementFieldChanged = false; // By default, upsert will not be required
		double requestCharge = 0;
		try
		{
			var itemResponse = await _cosmosContainerHandle.ReadItemAsync<CanonicalEngagement>((Guid)mpmPayload.engagementId!, cancellationToken: cancellationToken);
			canonicalEngagement = itemResponse.Resource;
			upsertOptions.IfMatchEtag = itemResponse.ETag; // Add ETag value to request options (optimistic concurrency)
			_logger?.LogDebug("Updating existing Engagement {engagementId}, {requestCharge} RUs",
				canonicalEngagement.id, itemResponse.RequestCharge);
			requestCharge += itemResponse.RequestCharge;
		}
		catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
		{
			// Document does not exist - if the incoming event is a delete, no further action required:
			if (operation == Operation.Delete)
			{
				return;
			}

			// Otherwise this is a net-new engagement with consumer-provided id value:
			canonicalEngagement = new CanonicalEngagement((Guid)mpmPayload.engagementId!, eventId, payloadValueDate, PayloadType);
			canonicalEngagement.mpm.id = canonicalEngagement.id; // Promote MPM id to top-level id
			_logger?.LogDebug("Creating new Engagement with consumer-provided id {engagementId}", canonicalEngagement.id);
			engagementFieldChanged = true; // We will definitely need to perform DB update
		}
		#endregion

		#region Compare "engagement" (prime) properties and apply updates as needed
		if (payloadValueDate >= canonicalEngagement.engagement.lastUpdateDateTime)
		{
			var updatedEngagement = canonicalEngagement.engagement with
			{
				engagementNo = (int)mpmPayload.engagementNo!,
				clientId = (Guid)mpmPayload.clientId!,
				clientNo = (int)mpmPayload.clientNo!,
				clientName = mpmPayload.clientName,
				description = mpmPayload.description,
				typeNo = mpmPayload.typeNo,
				typeDesc = mpmPayload.typeDesc,
				subTypeNo = mpmPayload.subTypeNo,
				subTypeDesc = mpmPayload.subTypeDesc,
				natureNo = mpmPayload.natureNo,
				natureDesc = mpmPayload.natureDesc,
				priority = mpmPayload.priority,
				statusCodeNo = mpmPayload.statusCodeNo,
				statusCodeDesc = mpmPayload.statusCodeDesc,
				startDate = mpmPayload.startDate,
				dueDate = mpmPayload.dueDate,
				completedDate = mpmPayload.completedDate,
				complexity = mpmPayload.complexity,
				budgetedFees = mpmPayload.budgetedFees,
				filingYear = mpmPayload.filingYear,
				officeNo = mpmPayload.officeNo,
				officeName = mpmPayload.officeName,
				deptNo = mpmPayload.deptNo,
				deptName = mpmPayload.deptName,
				regionNo = mpmPayload.regionNo,
				regionName = mpmPayload.regionName,
				businessUnitNo = mpmPayload.businessUnitNo,
				businessUnitName = mpmPayload.businessUnitName,
				practiceUnitNo = mpmPayload.practiceUnitNo,
				practiceUnitName = mpmPayload.practiceUnitName,
				managedByPersonnelId = mpmPayload.managedByPersonnelId,
				managedByPersonnelNo = mpmPayload.managedByPersonnelNo,
				managedByName = mpmPayload.managedByName,
				team = mpmPayload.team,
				checklists = mpmPayload.checklists,
			};
			if (canonicalEngagement.engagement != updatedEngagement)
			{
				canonicalEngagement.engagement = updatedEngagement;
				canonicalEngagement.engagement.lastUpdateDateTime = payloadValueDate;
				engagementFieldChanged = true;
			}
		}
		#endregion

		// Update MPM-specific data as needed:
		bool mpmFieldChanged = canonicalEngagement.mpm.UpdateElements((Guid)mpmPayload.engagementId, operation == Operation.Delete, mpmPayload.extensionData, payloadValueDate);

		// If document was updated, set metadata fields and perform database upsert now:
		if (engagementFieldChanged || mpmFieldChanged)
		{
			canonicalEngagement.UpdateDocumentMetadata(payloadValueDate, PayloadType, eventId);
			var upsertResponse = await _cosmosContainerHandle.UpsertItemAsync(
				canonicalEngagement, requestOptions: upsertOptions, cancellationToken: cancellationToken);
			_logger?.LogInformation("UpsertEngagementAsync result {StatusCode} for Engagement {engagementId} from event {eventId}, {requestCharge} RUs",
				upsertResponse.StatusCode, canonicalEngagement.id, eventId, upsertResponse.RequestCharge);
		}
		else
		{
			_logger?.LogInformation("Discarded no-change event {eventId} for Engagement {engagementId} from {payloadValueDate}",
				eventId, canonicalEngagement.id, payloadValueDate);
		}
	}

	#region Internal payload record definition
	/// <summary>
	/// Definition for mpm.engagement.v2 payload
	/// </summary>
	/// <remarks>
	/// Needs to be in sync with payload produced by MPM_Data's Engagement assembler procedure
	/// </remarks>
	internal sealed record MpmEngagementV2CrudPayload : IValidatableObject
	{
		/// <summary>
		/// Unique identifier of this Engagement in MPM
		/// </summary>
		[Required]
		public Guid? engagementId { get; init; }

		#region Engagement "prime" data
		[Required]
		public int? engagementNo { get; init; }
		[Required]
		public Guid? clientId { get; init; }
		[Required]
		public int? clientNo { get; init; }
		public string? clientName { get; init; }
		public string? description { get; init; }
		public int? typeNo { get; init; }
		public string? typeDesc { get; init; }
		public int? subTypeNo { get; init; }
		public string? subTypeDesc { get; init; }
		public int? natureNo { get; init; }
		public string? natureDesc { get; init; }
		public string? priority { get; init; }
		public int? statusCodeNo { get; init; }
		public string? statusCodeDesc { get; init; }
		public DateTimeOffset? startDate { get; init; }
		public DateTimeOffset? dueDate { get; init; }
		public DateTimeOffset? completedDate { get; init; }
		public int? complexity { get; init; }
		public int? budgetedFees { get; init; }
		public int? filingYear { get; init; }
		public int? officeNo { get; init; }
		public string? officeName { get; init; }
		public int? deptNo { get; init; }
		public string? deptName { get; init; }
		public int? regionNo { get; init; }
		public string? regionName { get; init; }
		public int? businessUnitNo { get; init; }
		public string? businessUnitName { get; init; }
		public int? practiceUnitNo { get; init; }
		public string? practiceUnitName { get; init; }
		public Guid? managedByPersonnelId { get; init; }
		public int? managedByPersonnelNo { get; init; }
		public string? managedByName { get; init; }
		public EngagementTeam team { get; init; } = new();
		public ValueList<Checklist> checklists { get; init; } = new();
		#endregion

		/// <summary>
		/// Tolerant reader collection for any fields provided by MPM which are non-prime data
		/// </summary>
		[JsonExtensionData]
		public Dictionary<string, JsonElement>? extensionData { get; init; }

		/// <summary>
		/// Custom validation method - check for duplicate members in Team collection
		/// </summary>
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (team.members.GroupBy(m => m.personnelId).Any(g => g.Count() > 1))
			{
				yield return new ValidationResult("Team contains duplicate members", new[] { nameof(team) });
			}
		}
	}
	#endregion
}
