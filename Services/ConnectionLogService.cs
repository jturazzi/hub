using System;
using System.IO;

namespace Hub.Services;

public static class ConnectionLogService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hub", "Logs");

    private static string CurrentFile =>
        Path.Combine(LogDir, $"connexions-{DateTime.Now:yyyy-MM}.csv");

    private const string Header = "Date;Heure;Evenement;Login";

    public static void LogLoginSuccess(string samAccountName) =>
        Append("CONNEXION_OK", samAccountName);

    public static void LogLoginFailed(string samAccountName) =>
        Append("CONNEXION_ECHEC", samAccountName);

    public static void LogLogout(string samAccountName) =>
        Append("DECONNEXION", samAccountName);

    public static void LogWindowClosed(string samAccountName) =>
        Append("FERMETURE", samAccountName);

    public static void LogUserAdded(string samAccountName) =>
        Append("AJOUT_UTILISATEUR", samAccountName);

    public static void LogUserRemoved(string samAccountName) =>
        Append("SUPPRESSION_UTILISATEUR", samAccountName);

    private static void Append(string evenement, string login)
    {
        try
        {
            Directory.CreateDirectory(LogDir);

            var file = CurrentFile;
            if (!File.Exists(file))
                File.AppendAllText(file, Header + Environment.NewLine);

            var now  = DateTime.Now;
            var line = $"{now:yyyy-MM-dd};{now:HH:mm:ss};{evenement};{login}";
            File.AppendAllText(file, line + Environment.NewLine);
        }
        catch { /* non bloquant */ }
    }

    public static string LogDirectory => LogDir;
}
