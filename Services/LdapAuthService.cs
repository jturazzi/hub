using System;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Net.NetworkInformation;
using Hub.Models;

namespace Hub.Services;

public sealed class LdapAuthService
{
    /// <summary>
    /// Vérifie que le contrôleur de domaine Active Directory est joignable
    /// (ping ICMP d'abord, puis bind LDAP).
    /// </summary>
    public bool IsReachable(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return false;

        // 1) Test rapide : ping ICMP
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(domain, 1500);
            if (reply.Status != IPStatus.Success)
            {
                Trace.WriteLine($"Ping {domain} failed: {reply.Status}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Ping {domain} exception: {ex.Message}");
            return false;
        }

        // 2) Test LDAP : bind anonyme pour s'assurer que le contrôleur de domaine répond
        try
        {
            using var context = new PrincipalContext(ContextType.Domain, domain);
            _ = context.ConnectedServer;
            return true;
        }
        catch (PrincipalServerDownException ex)
        {
            Trace.WriteLine($"AD unreachable ({domain}): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"AD reachability check error ({domain}): {ex.Message}");
            return false;
        }
    }

    public AuthenticatedUser? Authenticate(string domain, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUser = NormalizeUserName(username);

        try
        {
            using var context = new PrincipalContext(ContextType.Domain, domain);

            if (!context.ValidateCredentials(normalizedUser, password, ContextOptions.Negotiate))
            {
                Trace.WriteLine($"LDAP authentication failed for user {normalizedUser} in domain {domain}.");
                return null;
            }

            using var principal = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, normalizedUser)
                                  ?? UserPrincipal.FindByIdentity(context, IdentityType.UserPrincipalName, normalizedUser);

            if (principal?.Sid is null)
            {
                throw new InvalidOperationException("SID utilisateur introuvable apres authentification LDAP.");
            }

            return new AuthenticatedUser
            {
                DisplayName = principal.DisplayName ?? normalizedUser,
                SamAccountName = principal.SamAccountName ?? normalizedUser,
                SidValue = principal.Sid.Value
            };
        }
        catch (PrincipalServerDownException ex)
        {
            Trace.WriteLine($"LDAP server unreachable: {ex}");
            throw new InvalidOperationException("Le serveur Active Directory est inaccessible.", ex);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"LDAP authentication error: {ex}");
            throw;
        }
    }

    private static string NormalizeUserName(string username)
    {
        var trimmed = username.Trim();

        if (trimmed.Contains("\\", StringComparison.Ordinal))
        {
            var parts = trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            return parts[^1];
        }

        if (trimmed.Contains("@", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed;
    }
}
