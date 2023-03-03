namespace TestDIHConsumer.Models;

/// <summary>
/// Canonical "Engagement" entity document definition
/// </summary>
public record CanonicalEngagement : CanonicalDocument
{
	internal const string PayloadType = "dih.engagement.v2";

	/// <inheritdoc/>
	public override int schemaVersion => 2;

	/// <summary>
	/// Container for this engagement's common/shared "prime" data
	/// </summary>
	public EngagementPrimeData engagement { get; set; }

	/// <summary>
	/// Container for this engagement's MPM-only data
	/// </summary>
	public EntitySubData mpm { get; init; }

	#region Constructors
	/// <summary>
	/// Default constructor (should be used by deserializer only)
	/// </summary>
	public CanonicalEngagement() : base()
	{
		engagement = new();
		mpm = new();
	}

	/// <summary>
	/// New record constructor - synchronize creation/update timestamps
	/// </summary>
	public CanonicalEngagement(Guid id, Guid eventId, DateTimeOffset valueDate, string source)
		: base(id, eventId, valueDate, source)
	{
		engagement = new(valueDate);
		mpm = new(valueDate);
	}
	#endregion

	#region Methods
	/// <summary>
	/// Prior to saving modified document to database, update metadata fields
	/// </summary>
	/// <param name="updateValueDate">Value date of the payload triggering this update</param>
	/// <param name="updatePayloadType">Type of payload triggering this update</param>
	/// <param name="updateEventId">Unique ID of the event triggering this update</param>
	public void UpdateDocumentMetadata(DateTimeOffset updateValueDate, string updatePayloadType, Guid updateEventId)
	{
		++documentVersion;
		lastUpdateDateTime = updateValueDate;
		lastUpdateSource = updatePayloadType;
		lastUpdateEventId = updateEventId;

		// Set document-level deleted flag only if Engagement is deleted in MPM (note: will need to be
		// updated as other EngagementSubData objects are added):
		deleted = mpm.deleted;
	}
	#endregion
}
