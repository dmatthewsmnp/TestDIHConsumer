namespace TestDIHConsumer.Models;

/// <summary>
/// Container for team members (i.e. personnel) assigned to an engagement
/// </summary>
public record EngagementTeam
{
	public ValueList<EngagementTeamMember> members { get; init; } = new();
}
