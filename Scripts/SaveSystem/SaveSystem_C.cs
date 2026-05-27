using Godot;
using System.Collections.Generic;

/// <summary>
/// SaveSystem_C — Godot <see cref="ConfigFile"/> implementation.
/// Saves data in a human-readable INI-style <c>.cfg</c> file with sections
/// <c>[meta]</c>, <c>[settings]</c>, <c>[match]</c>, <c>[player]</c>, and <c>[stats]</c>.
/// </summary>
public class SaveSystem_C : ISaveSystem
{
    private const string SaveDir       = "user://saves/";
    private const string FileExtension = ".cfg";

    public string SystemName        => "ConfigFile (C)";
    public string SystemDescription => "INI-style .cfg files using Godot's built-in ConfigFile";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetFilePath(int slotIndex)
        => $"{SaveDir}slot_{slotIndex}{FileExtension}";

    private static void EnsureSaveDir()
    {
        using var dir = DirAccess.Open("user://");
        if (dir != null && !dir.DirExists("saves"))
            dir.MakeDir("saves");
    }

    // ── ISaveSystem ───────────────────────────────────────────────────────────

    public void Save(SaveData data, int slotIndex)
    {
        EnsureSaveDir();
        var cfg = new ConfigFile();

        // [meta]
        cfg.SetValue("meta", "slot_index",       data.SlotIndex);
        cfg.SetValue("meta", "save_name",        data.SaveName);
        cfg.SetValue("meta", "save_date",        data.SaveDate);
        cfg.SetValue("meta", "play_time_seconds",data.PlayTimeSeconds);
        cfg.SetValue("meta", "save_system_type", data.SaveSystemType);

        // [settings]
        cfg.SetValue("settings", "mouse_sensitivity", data.MouseSensitivity);
        cfg.SetValue("settings", "is_sound_enabled",  data.IsSoundEnabled);

        // [match]
        cfg.SetValue("match", "time_remaining", data.TimeRemaining);
        cfg.SetValue("match", "match_active",   data.MatchActive);

        // Store frag counts as a Godot Array of "peerId:frags" strings.
        var fragArr = new Godot.Collections.Array();
        foreach (var kv in data.FragCounts)
            fragArr.Add($"{kv.Key}:{kv.Value}");
        cfg.SetValue("match", "frag_counts", fragArr);

        // [player]
        cfg.SetValue("player", "health",       data.PlayerHealth);
        cfg.SetValue("player", "weapon_index", data.WeaponIndex);
        cfg.SetValue("player", "pos_x",        data.PlayerX);
        cfg.SetValue("player", "pos_y",        data.PlayerY);
        cfg.SetValue("player", "pos_z",        data.PlayerZ);

        var ammoArr = new Godot.Collections.Array();
        foreach (int a in data.WeaponAmmo)
            ammoArr.Add(a);
        cfg.SetValue("player", "weapon_ammo", ammoArr);

        // [stats]
        cfg.SetValue("stats", "best_frags",   data.BestFrags);
        cfg.SetValue("stats", "total_kills",  data.TotalKills);
        cfg.SetValue("stats", "total_deaths", data.TotalDeaths);
        cfg.SetValue("stats", "total_games",  data.TotalGames);

        var err = cfg.Save(GetFilePath(slotIndex));
        if (err != Error.Ok)
            GD.PushError($"SaveSystem_C: ConfigFile.Save failed with error {err} for slot {slotIndex}.");
    }

    public SaveData Load(int slotIndex)
    {
        string path = GetFilePath(slotIndex);
        if (!FileAccess.FileExists(path)) return null;

        var cfg = new ConfigFile();
        var err = cfg.Load(path);
        if (err != Error.Ok)
        {
            GD.PushError($"SaveSystem_C: ConfigFile.Load failed with error {err} for slot {slotIndex}.");
            return null;
        }

        // Parse frag counts.
        var fragCounts = new Dictionary<string, int>();
        if (cfg.HasSectionKey("match", "frag_counts"))
        {
            var fragArr = cfg.GetValue("match", "frag_counts").AsGodotArray();
            foreach (var item in fragArr)
            {
                string s = item.AsString();
                int sep = s.LastIndexOf(':');
                if (sep >= 0 && int.TryParse(s[(sep + 1)..], out int frags))
                    fragCounts[s[..sep]] = frags;
            }
        }

        // Parse weapon ammo.
        var ammoList = new System.Collections.Generic.List<int>();
        if (cfg.HasSectionKey("player", "weapon_ammo"))
        {
            var ammoArr = cfg.GetValue("player", "weapon_ammo").AsGodotArray();
            foreach (var item in ammoArr)
                ammoList.Add(item.AsInt32());
        }

        return new SaveData
        {
            SlotIndex        = cfg.GetValue("meta", "slot_index",        0).AsInt32(),
            SaveName         = cfg.GetValue("meta", "save_name",         "New Save").AsString(),
            SaveDate         = cfg.GetValue("meta", "save_date",         "").AsString(),
            PlayTimeSeconds  = cfg.GetValue("meta", "play_time_seconds", 0).AsInt32(),
            SaveSystemType   = cfg.GetValue("meta", "save_system_type",  "C").AsString(),
            MouseSensitivity = cfg.GetValue("settings", "mouse_sensitivity", 0.002f).AsSingle(),
            IsSoundEnabled   = cfg.GetValue("settings", "is_sound_enabled",  true).AsBool(),
            TimeRemaining    = cfg.GetValue("match", "time_remaining", 900f).AsSingle(),
            MatchActive      = cfg.GetValue("match", "match_active",   false).AsBool(),
            FragCounts       = fragCounts,
            PlayerHealth     = cfg.GetValue("player", "health",       100).AsInt32(),
            WeaponIndex      = cfg.GetValue("player", "weapon_index", 0).AsInt32(),
            PlayerX          = cfg.GetValue("player", "pos_x", 0f).AsSingle(),
            PlayerY          = cfg.GetValue("player", "pos_y", 0f).AsSingle(),
            PlayerZ          = cfg.GetValue("player", "pos_z", 0f).AsSingle(),
            WeaponAmmo       = ammoList.ToArray(),
            BestFrags        = cfg.GetValue("stats", "best_frags",   0).AsInt32(),
            TotalKills       = cfg.GetValue("stats", "total_kills",  0).AsInt32(),
            TotalDeaths      = cfg.GetValue("stats", "total_deaths", 0).AsInt32(),
            TotalGames       = cfg.GetValue("stats", "total_games",  0).AsInt32(),
        };
    }

    public bool SlotExists(int slotIndex)
        => FileAccess.FileExists(GetFilePath(slotIndex));

    public void DeleteSlot(int slotIndex)
    {
        if (!SlotExists(slotIndex)) return;
        using var dir = DirAccess.Open(SaveDir);
        dir?.Remove($"slot_{slotIndex}{FileExtension}");
    }
}
