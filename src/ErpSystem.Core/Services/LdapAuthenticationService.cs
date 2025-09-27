using Novell.Directory.Ldap;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Services;

public interface ILdapAuthenticationService
{
    Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, Tenant tenant);
    Task<LdapUser?> GetLdapUserAsync(string username, Tenant tenant);
    Task<LdapAuthenticationResult> TestConnectionAsync(Tenant tenant);
}

public class LdapAuthenticationService : ILdapAuthenticationService
{
    private readonly ILogger<LdapAuthenticationService> _logger;

    public LdapAuthenticationService(ILogger<LdapAuthenticationService> logger)
    {
        _logger = logger;
    }

    public async Task<LdapAuthenticationResult> AuthenticateAsync(string username, string password, Tenant tenant)
    {
        if (!tenant.LdapEnabled || string.IsNullOrEmpty(tenant.LdapServer))
        {
            return new LdapAuthenticationResult { Success = false, ErrorMessage = "LDAP is not configured for this tenant" };
        }

        return await Task.Run(() =>
        {
            try
            {
                using var connection = new LdapConnection();
                
                // Connect to LDAP server
                connection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);
                
                // First, bind with the service account to search for the user
                if (!string.IsNullOrEmpty(tenant.LdapBindDn) && !string.IsNullOrEmpty(tenant.LdapBindPassword))
                {
                    connection.Bind(tenant.LdapBindDn, tenant.LdapBindPassword);
                }

                // Search for the user
                var searchFilter = $"(sAMAccountName={username})";
                var searchResults = connection.Search(
                    tenant.LdapBaseDn,
                    2, // LdapConnection.SCOPE_SUB
                    searchFilter,
                    new[] { "distinguishedName", "displayName", "mail", "givenName", "sn" },
                    false
                );
                
                if (!searchResults.HasMore())
                {
                    return new LdapAuthenticationResult { Success = false, ErrorMessage = "User not found in LDAP" };
                }

                var userEntry = searchResults.Next();
                var userDn = userEntry.Dn;

                // Now authenticate the user with their credentials using a new connection
                using var userConnection = new LdapConnection();
                userConnection.Connect(tenant.LdapServer, tenant.LdapPort ?? 389);
                userConnection.Bind(userDn, password);

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

                return new LdapAuthenticationResult { Success = true, User = ldapUser };
            }
            catch (LdapException ldapEx)
            {
                _logger.LogWarning(ldapEx, "LDAP authentication failed for user {Username}", username);
                return new LdapAuthenticationResult { Success = false, ErrorMessage = "Invalid username or password" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during LDAP authentication for user {Username}", username);
                return new LdapAuthenticationResult { Success = false, ErrorMessage = "Authentication service error" };
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
                    new[] { "distinguishedName", "displayName", "mail", "givenName", "sn" },
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

    private string? GetAttributeValue(LdapEntry entry, string attributeName)
    {
        var attribute = entry.GetAttribute(attributeName);
        return attribute?.StringValue;
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