namespace TestDIHConsumer.Models;

/// <summary>
/// Container for all CanonicalEngagement data determined to be "prime", i.e. shared
/// across all systems/subscribers
/// </summary>
public record EngagementPrimeData
{
	/// <summary>
	/// Timestamp of last write of any field in this container
	/// </summary>
	public DateTimeOffset lastUpdateDateTime { get; set; }

	/// <summary>
	/// Numeric identifier of engagement (legacy field)
	/// </summary>
	public int engagementNo { get; init; }

	#region Client properties
	public Guid clientId { get; init; }
	public int clientNo { get; init; }
	public string? clientName { get; init; }
	#endregion

	#region Descriptor properties
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
	#endregion

	#region Complex properties
	public EngagementTeam team { get; init; } = new();
	public ValueList<Checklist> checklists { get; init; } = new();
	#endregion

	#region Constructors
	/// <summary>
	/// Default constructor (should be used by deserializer only)
	/// </summary>
	public EngagementPrimeData()
	{
	}

	/// <summary>
	/// New record constructor - synchronize creation/update timestamps
	/// </summary>
	public EngagementPrimeData(DateTimeOffset lastUpdatedDateTime)
		=> lastUpdateDateTime = lastUpdatedDateTime;
	#endregion
}
