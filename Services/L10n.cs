using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace Hub.Services;

/// <summary>
/// Fournit les chaînes localisées selon la langue du système (fr/en).
/// Charge Resources/Strings.{lang}.resx au démarrage.
/// </summary>
public static class L10n
{
    private static readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    static L10n()
    {
        var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        if (lang != "fr") lang = "en";   // fallback anglais

        // Cherche le fichier resx à côté de l'exe
        var base64 = AppContext.BaseDirectory;
        var path   = Path.Combine(base64, "Resources", $"Strings.{lang}.resx");

        // Fallback sur le répertoire source (développement)
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, $"Strings.{lang}.resx");
        }

        if (File.Exists(path))
        {
            Load(path);
        }
    }

    /// <summary>Retourne la valeur localisée pour la clé donnée.</summary>
    public static string T(string key) =>
        _strings.TryGetValue(key, out var v) ? v : key;

    /// <summary>Retourne la valeur localisée avec des arguments formatés (String.Format).</summary>
    public static string T(string key, params object[] args)
    {
        var template = T(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    private static void Load(string path)
    {
        var doc = XDocument.Load(path);
        foreach (var node in doc.Descendants("data"))
        {
            var name  = node.Attribute("name")?.Value;
            var value = node.Element("value")?.Value;
            if (name is not null && value is not null)
                _strings[name] = value;
        }
    }
}
