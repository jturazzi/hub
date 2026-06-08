using System;
using System.IO;
using System.Text.Json;
using Hub.Models;

namespace Hub.Services;

public static class HubConfigService
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "hub.config.json");

    public static HubConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return Default();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<HubConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? Default();
        }
        catch
        {
            return Default();
        }
    }

    private static HubConfig Default() => new()
    {
        AppTitle = "Hub",
        Domain   = "company.lan",
        Apps =
        [
            new AppEntry { Name = "App", Url = "https://exemple.local/" }
        ]
    };
}
