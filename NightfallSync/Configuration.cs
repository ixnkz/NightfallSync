using System;
using Dalamud.Configuration;

namespace NightfallSync;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Send links to the site at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The organizer code of the site (the same one as on neo-nightfall.net). Kept only on this PC.</summary>
    public string OrganizerKey { get; set; } = "";

    /// <summary>Where the NightFall stands: world, district (outdoor territory id), ward and plot as shown in game.</summary>
    public string World { get; set; } = "Moogle";
    public uint District { get; set; } = Places.Goblet;
    public int Ward { get; set; } = 12;
    public int Plot { get; set; } = 41;

    /// <summary>Inside the house only, or the plot's yard too.</summary>
    public bool YardCounts { get; set; } = false;

    /// <summary>Only links posted by these characters count (one name per line). Empty: anyone.</summary>
    public string AllowedSenders { get; set; } = "";

    /// <summary>The last link sent, so the same one is not sent again.</summary>
    public string LastSent { get; set; } = "";
}

public static class Places
{
    // Outdoor territory of each residential district.
    public const uint Mist = 339, LavenderBeds = 340, Goblet = 341, Shirogane = 641, Empyreum = 979;
    public static readonly (uint Id, string Name)[] All =
    {
        (Mist, "Brumée"), (LavenderBeds, "Lavandière"), (Goblet, "La Coupe"), (Shirogane, "Shirogane"), (Empyreum, "Empyrée"),
    };
}
