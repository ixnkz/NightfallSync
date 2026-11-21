using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace NightfallSync;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private string manual = "";

    public ConfigWindow(Plugin plugin) : base("NightFall Sync###nfsync")
    {
        this.plugin = plugin;
        Size = new Vector2(460, 470);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var c = plugin.Config;
        var (here, where) = plugin.Location();
        ImGui.TextColored(here ? new Vector4(1f, .4f, .7f, 1f) : new Vector4(.6f, .6f, .6f, 1f), here ? "♥ Tu es au NightFall" : "Tu n'es pas au NightFall");
        ImGui.TextDisabled(where);
        ImGui.Separator();

        var enabled = c.Enabled;
        if (ImGui.Checkbox("Envoyer les liens du chat au site", ref enabled)) { c.Enabled = enabled; plugin.Save(); }
        ImGui.TextWrapped("Au NightFall, un lien Watch2Gether (w2g.tv) ou Twitch posté dans le chat devient le salon affiché sur la carte du site.");

        ImGui.Spacing();
        var key = c.OrganizerKey;
        ImGui.SetNextItemWidth(260);
        if (ImGui.InputText("Code organisateur", ref key, 128, ImGuiInputTextFlags.Password)) { c.OrganizerKey = key; plugin.Save(); }
        if (string.IsNullOrWhiteSpace(c.OrganizerKey)) ImGui.TextColored(new Vector4(1f, .5f, .3f, 1f), "Entre le code organisateur du site pour activer l'envoi.");

        if (ImGui.CollapsingHeader("Adresse du NightFall"))
        {
            var world = c.World;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputText("Monde", ref world, 32)) { c.World = world; plugin.Save(); }
            var idx = Array.FindIndex(Places.All, p => p.Id == c.District);
            ImGui.SetNextItemWidth(160);
            if (ImGui.Combo("Quartier", ref idx, Places.All.Select(p => p.Name).ToArray(), Places.All.Length)) { c.District = Places.All[idx].Id; plugin.Save(); }
            var ward = c.Ward;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Secteur", ref ward)) { c.Ward = Math.Clamp(ward, 1, 30); plugin.Save(); }
            var plot = c.Plot;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Parcelle", ref plot)) { c.Plot = Math.Clamp(plot, 1, 60); plugin.Save(); }
            var yard = c.YardCounts;
            if (ImGui.Checkbox("Compter aussi le jardin (devant la maison)", ref yard)) { c.YardCounts = yard; plugin.Save(); }
        }

        if (ImGui.CollapsingHeader("Qui peut poster le lien"))
        {
            var senders = c.AllowedSenders;
            ImGui.TextWrapped("Un nom de personnage par ligne. Vide : n'importe qui au NightFall.");
            if (ImGui.InputTextMultiline("##senders", ref senders, 2000, new Vector2(-1, 80))) { c.AllowedSenders = senders; plugin.Save(); }
        }

        ImGui.Separator();
        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##manual", "Lien à envoyer à la main", ref manual, 300);
        ImGui.SameLine();
        if (ImGui.Button("Envoyer") && manual.Trim().Length > 0 && !string.IsNullOrWhiteSpace(c.OrganizerKey))
        {
            var url = manual.Trim();
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            _ = plugin.Send(url);
            manual = "";
        }
        ImGui.TextWrapped(plugin.Status);
    }
}
