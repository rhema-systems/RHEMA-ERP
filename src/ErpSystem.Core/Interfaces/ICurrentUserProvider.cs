namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Provides information about the current authenticated user
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>
    /// Gets the current user ID
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// Gets the current user's username/email
    /// </summary>
    string Username { get; }

    /// <summary>
    /// Gets the current user's full name
    /// </summary>
    string FullName { get; }

    /// <summary>
    /// Gets whether the current user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the user roles
    /// </summary>
    IEnumerable<string> Roles { get; }

    /// <summary>
    /// Checks if the current user has a specific role
    /// </summary>
    /// <param name="role">Role to check</param>
    /// <returns>True if user has the role</returns>
    bool HasRole(string role);

    /// <summary>
    /// Gets user claims
    /// </summary>
    IDictionary<string, string> Claims { get; }
}
