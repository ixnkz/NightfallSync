using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace NightfallSync;

public sealed class Plugin : IDalamudPlugin
{
    public const string Relay = "https://nightfall-relay.nightfall-club.workers.dev";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Config { get; }
    private readonly WindowSystem windows = new("NightfallSync");
    private readonly ConfigWindow configWindow;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // A Watch2Gether room or a Twitch channel, with or without https:// in front.
    private static readonly Regex LinkRx = new(
        @"(?:https?://)?(?:www\.)?(?:w2g\.tv/[^\s<>""']+|(?:m\.)?twitch\.tv/[A-Za-z0-9_]{3,25})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>What the last send did, shown in the window.</summary>
    public string Status { get; private set; } = "En attente d'un lien.";

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configWindow = new ConfigWindow(this);
        windows.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;
        PluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        Commands.AddHandler("/nightfall", new CommandInfo((_, _) => ToggleWindow())
        {
            HelpMessage = "Ouvre les réglages de NightFall Sync (salon musical du site).",
        });
        Chat.ChatMessageUnhandled += OnChat;
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("NightfallPlugin/1.0");
    }

    public void Dispose()
    {
        Chat.ChatMessageUnhandled -= OnChat;
        Commands.RemoveHandler("/nightfall");
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleWindow;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        windows.RemoveAllWindows();
    }

    public void ToggleWindow() => configWindow.Toggle();
    public void Save() => PluginInterface.SavePluginConfig(Config);

    /// <summary>Where the character stands, as text for the window, and whether it is the NightFall.</summary>
    public unsafe (bool Here, string Where) Location()
    {
        if (!ClientState.IsLoggedIn || !Player.IsLoaded) return (false, "Pas connecté");
        var hm = HousingManager.Instance();
        if (hm == null) return (false, "Hors quartier résidentiel");
        var world = Player.CurrentWorld.Value.Name.ExtractText();
        var inside = hm->IsInside();
        var id = hm->GetCurrentHouseId();
        int ward = hm->GetCurrentWard() + 1, plot = hm->GetCurrentPlot() + 1;
        if (ward <= 0) return (false, $"{world}, hors quartier résidentiel");
        var district = Places.All.FirstOrDefault(p => p.Id == id.TerritoryTypeId).Name ?? $"zone {id.TerritoryTypeId}";
        var where = $"{world} · {district} · secteur {ward} · parcelle {(plot > 0 ? plot : 0)}{(inside ? " (intérieur)" : "")}";
        var here = string.Equals(world, Config.World, StringComparison.OrdinalIgnoreCase)
                   && id.TerritoryTypeId == Config.District && ward == Config.Ward && plot == Config.Plot
                   && (inside || Config.YardCounts);
        return (here, where);
    }

    private void OnChat(Dalamud.Game.Chat.IChatMessage msg)
    {
        try
        {
            if (!Config.Enabled || string.IsNullOrWhiteSpace(Config.OrganizerKey)) return;
            var m = LinkRx.Match(msg.Message.TextValue);
            if (!m.Success) return;
            if (!SenderAllowed(msg.Sender.TextValue)) return;
            if (!Location().Here) return;
            var url = m.Value.TrimEnd('.', ',', ')', '!', '?');
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            if (url.Equals(Config.LastSent, StringComparison.OrdinalIgnoreCase)) return;
            _ = Send(url);
        }
        catch (Exception e) { Log.Error(e, "NightFall Sync: chat"); }
    }

    private bool SenderAllowed(string sender)
    {
        var names = Config.AllowedSenders.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // The sender text may carry a world icon or a party number around the name.
        return names.Length == 0 || names.Any(n => sender.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Puts the room on the site (the same as the organizer's "Ouvrir le salon" button on the menu).</summary>
    public async Task Send(string url)
    {
        Config.LastSent = url;
        Save();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"{Relay}/music")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { url, label = "" }), Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("X-Organizer-Key", Config.OrganizerKey.Trim());
            using var res = await Http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            Status = res.IsSuccessStatusCode
                ? $"{DateTime.Now:HH:mm} · salon mis en ligne : {url}"
                : $"{DateTime.Now:HH:mm} · refusé ({(int)res.StatusCode}) : {Explain(body)}";
            if (!res.IsSuccessStatusCode) Config.LastSent = "";
        }
        catch (Exception e)
        {
            Status = $"{DateTime.Now:HH:mm} · site injoignable : {e.Message}";
            Config.LastSent = "";
            Log.Error(e, "NightFall Sync: send");
        }
        Save();
        var ok = Status.Contains("mis en ligne");
        await Framework.RunOnFrameworkThread(() => Chat.Print(ok ? $"[NightFall] Salon du site mis à jour : {url}" : $"[NightFall] {Status}"));
    }

    private static string Explain(string body) =>
        body.Contains("bad_organizer_key") ? "code organisateur incorrect"
        : body.Contains("invalid_music_link") ? "lien non reconnu (Watch2Gether ou Twitch seulement)"
        : body.Contains("rate_limited") ? "trop d'essais, patiente une minute"
        : body.Length > 80 ? body[..80] : body;
}
