using System.Collections.Generic;

namespace Hub.Models;

public sealed class HubConfig
{
    public string AppTitle           { get; init; } = "Hub";
    public string Domain             { get; init; } = "company.lan";
    public string? CacheFolder       { get; init; }
    public string? FirstConnectionUrl { get; init; }
    public List<AppEntry> Apps       { get; init; } = [];
}

public sealed class AppEntry
{
    public string Name           { get; init; } = "";
    public string Url            { get; init; } = "";
    public bool   OpenOnStartup  { get; init; }
}
