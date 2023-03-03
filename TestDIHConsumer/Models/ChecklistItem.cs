namespace TestDIHConsumer.Models;

/// <summary>
/// Individual task within an engagement checklist
/// </summary>
public readonly struct ChecklistItem
{
	public long itemNo { get; init; }
	public byte itemSeqNo { get; init; }
	public string? itemTitle { get; init; }
	public string? itemDescription { get; init; }
	public DateTimeOffset? itemCompletedDate { get; init; }
	public Guid? itemCompletedByContactId { get; init; }
	public int? itemCompletedByContactNo { get; init; }
}
