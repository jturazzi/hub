using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Models;

namespace Hub.Services;

public static class UserHistoryService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hub", "users.dat");   // .dat = binaire chiffré

    // ─── Lecture ─────────────────────────────────────────────────────────────

    public static List<SavedUser> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var cipher  = File.ReadAllBytes(FilePath);
            var plain   = ProtectedData.Unprotect(cipher, null, DataProtectionScope.LocalMachine);
            var json    = Encoding.UTF8.GetString(plain);
            return JsonSerializer.Deserialize<List<SavedUser>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    // ─── Écriture (interne) ───────────────────────────────────────────────────

    private static void Persist(List<SavedUser> users)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var json   = JsonSerializer.Serialize(users);
        var plain  = Encoding.UTF8.GetBytes(json);
        var cipher = ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(FilePath, cipher);
    }

    // ─── Sauvegarde ──────────────────────────────────────────────────────────

    public static bool Save(AuthenticatedUser authenticated)
    {
        try
        {
            var users = Load();

            var existing = users.FirstOrDefault(u =>
                string.Equals(u.SamAccountName, authenticated.SamAccountName, StringComparison.OrdinalIgnoreCase));

            bool isNew = existing is null;

            if (existing is not null)
            {
                existing.DisplayName  = authenticated.DisplayName;
                existing.SidValue     = authenticated.SidValue;
                existing.LastLoginUtc = DateTime.UtcNow;
            }
            else
            {
                users.Add(new SavedUser
                {
                    DisplayName    = authenticated.DisplayName,
                    SamAccountName = authenticated.SamAccountName,
                    SidValue       = authenticated.SidValue,
                    LastLoginUtc   = DateTime.UtcNow
                });
            }

            users = [.. users.OrderByDescending(u => u.LastLoginUtc)];
            Persist(users);
            return isNew;
        }
        catch
        {
            // Non bloquant
            return false;
        }
    }

    // ─── Suppression ─────────────────────────────────────────────────────────

    public static void Remove(string samAccountName)
    {
        try
        {
            var users = Load();
            users.RemoveAll(u => string.Equals(u.SamAccountName, samAccountName, StringComparison.OrdinalIgnoreCase));
            Persist(users);
        }
        catch
        {
            // Non bloquant
        }
    }
}
