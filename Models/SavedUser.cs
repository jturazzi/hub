using System;

namespace Hub.Models;

public sealed class SavedUser
{
    public string DisplayName    { get; set; } = "";
    public string SamAccountName { get; set; } = "";
    public string SidValue       { get; set; } = "";
    public DateTime LastLoginUtc { get; set; }
}
