namespace ErpSystem.Core.DTOs.HR;

public class EmployeeRelieverDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid RelieverEmployeeId { get; set; }
    public string RelieverName { get; set; } = string.Empty;
    public string? RelieverPositionName { get; set; }
    public string? RelieverOrganizationUnitName { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmployeeRelieverDto
{
    public Guid EmployeeId { get; set; }
    public Guid RelieverEmployeeId { get; set; }
    public int Priority { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeRelieverDto
{
    public Guid RelieverEmployeeId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
