using ErpSystem.Core.Entities;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace ErpSystem.Core.Services;

public interface ILdapAuthenticationService
{
    Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, Tenant tenant);
    Task<LdapUser?> GetLdapUserAsync(string username, Tenant tenant);
    Task<LdapAuthenticationResult> TestConnectionAsync(Tenant tenant);
    Task<IReadOnlyList<LdapDirectoryUser>> SearchUsersAsync(string? query, int limit, Tenant tenant);
}

public class LdapAuthenticationService : ILdapAuthenticationService
{
    private readonly ILogger<LdapAuthenticationService> _logger;
    private static readonly string[] attrs = new[] { "distinguishedName", "displayName", "mail", "givenName", "sn" };
    private static readonly string[] listUserAttrs = new[] { "distinguishedName", "displayName", "mail", "givenName", "sn", "sAMAccountName", "userPrincipalName" };

    public LdapAuthenticationService(ILogger<LdapAuthenticationService> logger)
    {
        _logger = logger;
    }

    public async Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, Tenant tenant)
    {
	        if (!tenant.LdapEnabled || string.IsNullOrEmpty(tenant.LdapServer))
	        {
	            _logger.LogInformation(
	                "Skipping LDAP authentication for user {Username}: LDAP is disabled or server not configured for tenant {TenantId} ({TenantCode}).",
	                username,
	                tenant.Id,
	                tenant.Code);
	            return new LdapAuthenticationResult
	            {
	                Success = false,
	                ErrorMessage = "LDAP is not configured for this tenant"
	            };
	        }

        return await Task.Run(() =>
        {
            try
            {
	                _logger.LogInformation(
	                    "Starting LDAP authentication for user {Username} against server {Server}:{Port} (TenantId={TenantId}).",
	                    username,
	                    tenant.LdapServer,
	                    tenant.LdapPort ?? 389,
	                    tenant.Id);

                using var connection = new LdapConnection();

                // Connect to LDAP server
                connection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);
	                _logger.LogDebug(
	                    "LDAP connection established to {Server}:{Port} for user {Username}.",
	                    tenant.LdapServer,
	                    tenant.LdapPort ?? 389,
	                    username);

                // First, bind with the service account to search for the user
	                if (!string.IsNullOrEmpty(tenant.LdapBindDn) && !string.IsNullOrEmpty(tenant.LdapBindPassword))
	                {
	                    _logger.LogDebug("Binding to LDAP with service account DN {BindDn}", tenant.LdapBindDn);
	                    connection.Bind(tenant.LdapBindDn, tenant.LdapBindPassword);
	                }
	                else
	                {
	                    _logger.LogDebug(
	                        "No LDAP bind DN/password configured for tenant {TenantId}. Performing anonymous search for user {Username}.",
	                        tenant.Id,
	                        username);
	                }

                // Search for the user
	                var searchFilter = $"(sAMAccountName={username})";
	                _logger.LogDebug(
	                    "Searching LDAP for user {Username} with filter {Filter} in base DN {BaseDn}.",
	                    username,
	                    searchFilter,
	                    tenant.LdapBaseDn);
                var searchResults = connection.Search(
                    tenant.LdapBaseDn,
                    2, // LdapConnection.SCOPE_SUB
                    searchFilter,
                    attrs,
                    false
                );

	                if (!searchResults.HasMore())
	                {
	                    _logger.LogWarning(
	                        "LDAP search did not find user {Username} in base DN {BaseDn}.",
	                        username,
	                        tenant.LdapBaseDn);
	                    return new LdapAuthenticationResult
	                    {
	                        Success = false,
	                        ErrorMessage = "User not found in LDAP"
	                    };
	                }

                var userEntry = searchResults.Next();
                var userDn = userEntry.Dn;
	                _logger.LogDebug(
	                    "Found LDAP user entry for {Username} with DN {UserDn}.",
	                    username,
	                    userDn);

                // Now authenticate the user with their credentials using a new connection
	                using var userConnection = new LdapConnection();
	                userConnection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);
	                _logger.LogDebug(
	                    "Connecting secondary LDAP connection to {Server}:{Port} for user bind of {Username}.",
	                    tenant.LdapServer,
	                    tenant.LdapPort ?? 389,
	                    username);
	                userConnection.Bind(userDn, password);
	                _logger.LogInformation("LDAP bind successful for user {Username}", username);

                // If we get here, authentication was successful
	                var ldapUser = new LdapUser
                {
                    Username = username,
                    DistinguishedName = userDn,
                    DisplayName = GetAttributeValue(userEntry, "displayName") ?? username,
                    Email = GetAttributeValue(userEntry, "mail") ?? "",
                    FirstName = GetAttributeValue(userEntry, "givenName") ?? "",
                    LastName = GetAttributeValue(userEntry, "sn") ?? ""
                };

                    if (string.IsNullOrWhiteSpace(ldapUser.Email))
                    {
                        _logger.LogWarning("LDAP user {Username} has no email ('mail') attribute configured.", username);
                    }

	                _logger.LogInformation("LDAP authentication succeeded for user {Username}", username);
	                return new LdapAuthenticationResult { Success = true, User = ldapUser };
            }
            catch (LdapException ldapEx)
            {
	                _logger.LogWarning(
	                    ldapEx,
	                    "LDAP authentication failed for user {Username} against {Server}:{Port}. LDAP error: {LdapError}",
	                    username,
	                    tenant.LdapServer,
	                    tenant.LdapPort ?? 389,
	                    ldapEx.LdapErrorMessage);
	                return new LdapAuthenticationResult
	                {
	                    Success = false,
	                    ErrorMessage = "Invalid username or password"
	                };
            }
            catch (Exception ex)
            {
	                _logger.LogError(
	                    ex,
	                    "Unexpected error during LDAP authentication for user {Username} against {Server}:{Port}",
	                    username,
	                    tenant.LdapServer,
	                    tenant.LdapPort ?? 389);
	                return new LdapAuthenticationResult
	                {
	                    Success = false,
	                    ErrorMessage = "Authentication service error"
	                };
            }
        });
    }

    public async Task<LdapUser?> GetLdapUserAsync(string username, Tenant tenant)
    {
        if (!tenant.LdapEnabled || string.IsNullOrEmpty(tenant.LdapServer))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var connection = new LdapConnection();
                connection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);

                if (!string.IsNullOrEmpty(tenant.LdapBindDn) && !string.IsNullOrEmpty(tenant.LdapBindPassword))
                {
                    connection.Bind(tenant.LdapBindDn, tenant.LdapBindPassword);
                }

                var searchFilter = $"(sAMAccountName={username})";
                var searchResults = connection.Search(
                    tenant.LdapBaseDn,
                    2, // LdapConnection.SCOPE_SUB
                    searchFilter,
                    attrs,
                    false
                );

                if (!searchResults.HasMore())
                {
                    return null;
                }

                var userEntry = searchResults.Next();
                return new LdapUser
                {
                    Username = username,
                    DistinguishedName = userEntry.Dn,
                    DisplayName = GetAttributeValue(userEntry, "displayName") ?? username,
                    Email = GetAttributeValue(userEntry, "mail") ?? "",
                    FirstName = GetAttributeValue(userEntry, "givenName") ?? "",
                    LastName = GetAttributeValue(userEntry, "sn") ?? ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving LDAP user {Username}", username);
                return null;
            }
        });
    }

    public async Task<LdapAuthenticationResult> TestConnectionAsync(Tenant tenant)
    {
        if (!tenant.LdapEnabled || string.IsNullOrEmpty(tenant.LdapServer))
        {
            return new LdapAuthenticationResult { Success = false, ErrorMessage = "LDAP is not enabled or server is not configured" };
        }

        return await Task.Run(() =>
        {
            try
            {
                using var connection = new LdapConnection();

                // Test basic connection to LDAP server
                connection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);

                // Test bind with service account if provided
                if (!string.IsNullOrEmpty(tenant.LdapBindDn) && !string.IsNullOrEmpty(tenant.LdapBindPassword))
                {
                    connection.Bind(tenant.LdapBindDn, tenant.LdapBindPassword);

                    // Test a basic search to verify Base DN and permissions
                    if (!string.IsNullOrEmpty(tenant.LdapBaseDn))
                    {
                        try
                        {
                            var searchResults = connection.Search(
                                tenant.LdapBaseDn,
                                2, // LdapConnection.SCOPE_SUB
                                "(objectClass=*)", // Simple search filter
                                new[] { "objectClass" },
                                false
                            );

                            // If we can search, the configuration is working
                            return new LdapAuthenticationResult
                            {
                                Success = true,
                                ErrorMessage = "LDAP connection successful. Server accessible, bind successful, and Base DN is valid."
                            };
                        }
                        catch (LdapException ldapEx)
                        {
                            _logger.LogWarning(ldapEx, "LDAP search test failed");
                            return new LdapAuthenticationResult
                            {
                                Success = false,
                                ErrorMessage = $"LDAP connection established but search failed. Please verify Base DN: {ldapEx.LdapErrorMessage}"
                            };
                        }
                    }
                    else
                    {
                        return new LdapAuthenticationResult
                        {
                            Success = true,
                            ErrorMessage = "LDAP connection and bind successful. Note: Base DN not configured - add Base DN for full functionality."
                        };
                    }
                }
                else
                {
                    // Anonymous bind test (some LDAP servers allow this)
                    try
                    {
                        connection.Bind("", ""); // Anonymous bind
                        return new LdapAuthenticationResult
                        {
                            Success = true,
                            ErrorMessage = "LDAP server accessible (anonymous bind). Consider adding service account credentials for better security."
                        };
                    }
                    catch
                    {
                        return new LdapAuthenticationResult
                        {
                            Success = false,
                            ErrorMessage = "LDAP server accessible but authentication failed. Please provide valid Bind DN and password."
                        };
                    }
                }
            }
            catch (LdapException ldapEx)
            {
                _logger.LogWarning(ldapEx, "LDAP connection test failed");
                return new LdapAuthenticationResult
                {
                    Success = false,
                    ErrorMessage = $"LDAP connection failed: {ldapEx.LdapErrorMessage}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LDAP connection test");
                return new LdapAuthenticationResult
                {
                    Success = false,
                    ErrorMessage = $"Connection test failed: {ex.Message}"
                };
            }
        });
    }

    public async Task<IReadOnlyList<LdapDirectoryUser>> SearchUsersAsync(string? query, int limit, Tenant tenant)
    {
        if (!tenant.LdapEnabled || string.IsNullOrEmpty(tenant.LdapServer))
        {
            throw new InvalidOperationException("LDAP is not configured for this tenant");
        }

        if (string.IsNullOrWhiteSpace(tenant.LdapBaseDn))
        {
            throw new InvalidOperationException("LDAP Base DN is required");
        }

        var cappedLimit = Math.Clamp(limit, 1, 500);
        var normalizedQuery = (query ?? string.Empty).Trim();

        return await Task.Run(() =>
        {
            using var connection = new LdapConnection();
            connection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);

            if (!string.IsNullOrEmpty(tenant.LdapBindDn) && !string.IsNullOrEmpty(tenant.LdapBindPassword))
            {
                connection.Bind(tenant.LdapBindDn, tenant.LdapBindPassword);
            }
            else
            {
                connection.Bind("", "");
            }

            var filter = BuildUserSearchFilter(normalizedQuery);
            var searchResults = connection.Search(
                tenant.LdapBaseDn,
                2, // LdapConnection.SCOPE_SUB
                filter,
                listUserAttrs,
                false
            );

            var users = new List<LdapDirectoryUser>(Math.Min(cappedLimit, 100));
            while (searchResults.HasMore() && users.Count < cappedLimit)
            {
                LdapEntry entry;
                try
                {
                    entry = searchResults.Next();
                }
                catch (LdapException ex)
                {
                    _logger.LogWarning(ex, "LDAP user search iteration failed");
                    break;
                }

                var username = GetAttributeValue(entry, "sAMAccountName") ?? string.Empty;
                var upn = GetAttributeValue(entry, "userPrincipalName") ?? string.Empty;
                var dn = entry.Dn ?? string.Empty;
                var displayName = GetAttributeValue(entry, "displayName") ?? string.Empty;
                var email = GetAttributeValue(entry, "mail") ?? string.Empty;
                var firstName = GetAttributeValue(entry, "givenName") ?? string.Empty;
                var lastName = GetAttributeValue(entry, "sn") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(upn))
                {
                    username = upn;
                }

                users.Add(new LdapDirectoryUser
                {
                    Username = username,
                    UserPrincipalName = upn,
                    DistinguishedName = dn,
                    DisplayName = displayName,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName
                });
            }

            return users;
        });
    }

    private static string BuildUserSearchFilter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "(&(objectCategory=person)(objectClass=user))";
        }

        var q = EscapeLdapFilterValue(query);
        return $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName=*{q}*)(userPrincipalName=*{q}*)(displayName=*{q}*)(mail=*{q}*)))";
    }

    private static string EscapeLdapFilterValue(string value)
    {
        return value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }

    private static string? GetAttributeValue(LdapEntry entry, string attributeName)
    {
        try
        {
            var attribute = entry.GetAttribute(attributeName);
            return attribute?.StringValue;
        }
        catch (KeyNotFoundException)
        {
            // Novell.Directory.Ldap may throw when an attribute is not present on the entry.
            return null;
        }
        catch
        {
            // Best-effort: missing/malformed attributes should not break authentication.
            return null;
        }
    }
}

public class LdapAuthenticationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public LdapUser? User { get; set; }
}

public class LdapUser
{
    public string Username { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class LdapDirectoryUser
{
    public string Username { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
