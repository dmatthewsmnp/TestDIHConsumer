namespace TestDIHConsumer.Models;

/// <summary>
/// Container for checklist of tasks which can be attached to an engagement
/// </summary>
public struct Checklist
{
	public Guid id { get; init; }
	public long checklistNo { get; init; }
	public string? name { get; init; }
	public string? description { get; init; }
	public ValueList<ChecklistItem> items { get; init; }
}
