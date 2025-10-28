namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Service to provide current user context information
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ID
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current user's username
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets the current user's email
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Gets the current user's employee ID (if linked to an employee)
    /// </summary>
    Guid? EmployeeId { get; }

    /// <summary>
    /// Checks if the current user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user's roles
    /// </summary>
    IEnumerable<string> Roles { get; }

    /// <summary>
    /// Checks if the current user has a specific role
    /// </summary>
    /// <param name="role">The role to check</param>
    /// <returns>True if the user has the role, false otherwise</returns>
    bool IsInRole(string role);

    /// <summary>
    /// Gets the current user's IP address
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// Gets the current user agent
    /// </summary>
    string? UserAgent { get; }
}