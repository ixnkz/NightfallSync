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
    // Only plain reads of the game's memory here, never a call into the game's own functions:
    // a game function that moved after a patch would crash the game, a read cannot.
    public unsafe (bool Here, string Where) Location()
    {
        try
        {
            if (!ClientState.IsLoggedIn || !Player.IsLoaded) return (false, "Pas connecté");
            var hm = HousingManager.Instance();
            if (hm == null || hm->IndoorTerritory == null) return (false, "Pas à l'intérieur d'une maison");
            var id = hm->IndoorTerritory->HouseId;
            if (id.IsApartment || id.IsWorkshop) return (false, "Appartement ou atelier, pas une maison");
            var world = Player.CurrentWorld.ValueNullable?.Name.ExtractText() ?? "?";
            int ward = id.WardIndex + 1, plot = id.PlotIndex + 1;
            var district = Places.All.FirstOrDefault(p => p.Id == id.TerritoryTypeId).Name ?? $"zone {id.TerritoryTypeId}";
            var where = $"{world} · {district} · secteur {ward} · parcelle {plot}";
            var here = string.Equals(world, Config.World, StringComparison.OrdinalIgnoreCase)
                       && id.TerritoryTypeId == Config.District && ward == Config.Ward && plot == Config.Plot;
            return (here, where);
        }
        catch (Exception e)
        {
            Log.Error(e, "NightFall Sync: location");
            return (false, "Adresse illisible");
        }
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
            // The network work leaves the chat at once, so the game never waits on the site.
            _ = Task.Run(() => Send(url));
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
    // Runs off the game's thread: only the network waits there. Saving and printing go back to the game's thread.
    public async Task Send(string url)
    {
        Config.LastSent = url;
        var key = Config.OrganizerKey.Trim();
        bool ok = false;
        string status;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"{Relay}/music")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { url, label = "" }), Encoding.UTF8, "application/json"),
            };
            req.Headers.TryAddWithoutValidation("User-Agent", "NightfallPlugin/1.1");
            req.Headers.TryAddWithoutValidation("X-Organizer-Key", key);
            using var res = await Http.SendAsync(req).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            ok = res.IsSuccessStatusCode;
            status = ok
                ? $"{DateTime.Now:HH:mm} · salon mis en ligne : {url}"
                : $"{DateTime.Now:HH:mm} · refusé ({(int)res.StatusCode}) : {Explain(body)}";
        }
        catch (Exception e)
        {
            status = $"{DateTime.Now:HH:mm} · site injoignable : {e.Message}";
            Log.Error(e, "NightFall Sync: send");
        }
        try
        {
            await Framework.RunOnFrameworkThread(() =>
            {
                Status = status;
                if (!ok) Config.LastSent = "";
                Save();
                Chat.Print(ok ? $"[NightFall] Salon du site mis à jour : {url}" : $"[NightFall] {status}");
            }).ConfigureAwait(false);
        }
        catch (Exception e) { Log.Error(e, "NightFall Sync: after send"); }
    }

    private static string Explain(string body) =>
        body.Contains("bad_organizer_key") ? "code organisateur incorrect"
        : body.Contains("invalid_music_link") ? "lien non reconnu (Watch2Gether ou Twitch seulement)"
        : body.Contains("rate_limited") ? "trop d'essais, patiente une minute"
        : body.Length > 80 ? body[..80] : body;
}
