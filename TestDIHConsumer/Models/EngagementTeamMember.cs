namespace TestDIHConsumer.Models;

/// <summary>
/// Model for information on an individual assigned to an engagement team
/// </summary>
public struct EngagementTeamMember
{
	public Guid personnelId { get; init; }
	public int personnelNo { get; init; }
	public string? firstName { get; init; }
	public string? lastName { get; init; }
	public string? displayName { get; init; }
	public string? displayInitials { get; init; }
	public string? userName { get; init; }
	public string? jobCode { get; init; }
	public string? jobTitle { get; init; }
	public string? positionType { get; init; }
	public int? officeNo { get; init; }
	public int? officeLocationNo { get; init; }
	public string? officeLocationDesc { get; init; }
	public string? officeCity { get; init; }
	public string? officeProvince { get; init; }
	public decimal? baseHoursPerDay { get; init; }
	public Guid? managerId { get; init; }
	public string? managerName { get; init; }
	public int? businessUnitNo { get; init; }
	public string? businessUnitName { get; init; }
	public int? regionNo { get; init; }
	public string? regionName { get; init; }
	public int? evpRegionNo { get; init; }
	public string? evpGroupName { get; init; }
	public string? primaryServiceAreaName { get; init; }
	public string? subServiceAreaName { get; init; }
	public string? classificationDesc { get; init; }
	public int? roleNo { get; init; }
	public string? roleType { get; init; }
	public string? roleName { get; init; }
}
