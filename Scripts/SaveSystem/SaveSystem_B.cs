using Godot;
using System.Collections.Generic;

/// <summary>
/// SaveSystem_B — binary implementation using Godot's <c>var_to_bytes</c>.
/// Data is packed into a Godot <see cref="Godot.Collections.Dictionary"/> and
/// serialised to a compact binary blob.  Files use the <c>.sav</c> extension.
/// </summary>
public class SaveSystem_B : ISaveSystem
{
    private const string SaveDir       = "user://saves/";
    private const string FileExtension = ".sav";

    public string SystemName        => "Binary (B)";
    public string SystemDescription => "Compact binary format using Godot var_to_bytes";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetFilePath(int slotIndex)
        => $"{SaveDir}slot_{slotIndex}{FileExtension}";

    private static void EnsureSaveDir()
    {
        using var dir = DirAccess.Open("user://");
        if (dir != null && !dir.DirExists("saves"))
            dir.MakeDir("saves");
    }

    /// <summary>Pack a <see cref="SaveData"/> into a Godot Dictionary for binary serialisation.</summary>
    private static Godot.Collections.Dictionary Pack(SaveData d)
    {
        var fragDict = new Godot.Collections.Dictionary();
        foreach (var kv in d.FragCounts)
            fragDict[kv.Key] = kv.Value;

        var ammoArr = new Godot.Collections.Array();
        foreach (int a in d.WeaponAmmo)
            ammoArr.Add(a);

        return new Godot.Collections.Dictionary
        {
            ["SlotIndex"]        = d.SlotIndex,
            ["SaveName"]         = d.SaveName,
            ["SaveDate"]         = d.SaveDate,
            ["PlayTimeSeconds"]  = d.PlayTimeSeconds,
            ["SaveSystemType"]   = d.SaveSystemType,
            ["MouseSensitivity"] = d.MouseSensitivity,
            ["IsSoundEnabled"]   = d.IsSoundEnabled,
            ["TimeRemaining"]    = d.TimeRemaining,
            ["MatchActive"]      = d.MatchActive,
            ["FragCounts"]       = fragDict,
            ["PlayerHealth"]     = d.PlayerHealth,
            ["WeaponIndex"]      = d.WeaponIndex,
            ["WeaponAmmo"]       = ammoArr,
            ["PlayerX"]          = d.PlayerX,
            ["PlayerY"]          = d.PlayerY,
            ["PlayerZ"]          = d.PlayerZ,
            ["BestFrags"]        = d.BestFrags,
            ["TotalKills"]       = d.TotalKills,
            ["TotalDeaths"]      = d.TotalDeaths,
            ["TotalGames"]       = d.TotalGames,
        };
    }

    /// <summary>Unpack a Godot Dictionary back into a <see cref="SaveData"/>.</summary>
    private static SaveData Unpack(Godot.Collections.Dictionary dict)
    {
        var fragCounts = new Dictionary<string, int>();
        if (dict.ContainsKey("FragCounts") && dict["FragCounts"].Obj is Godot.Collections.Dictionary fd)
            foreach (var key in fd.Keys)
                fragCounts[key.ToString()] = fd[key].AsInt32();

        var ammoList = new System.Collections.Generic.List<int>();
        if (dict.ContainsKey("WeaponAmmo") && dict["WeaponAmmo"].Obj is Godot.Collections.Array aa)
            foreach (var item in aa)
                ammoList.Add(item.AsInt32());

        return new SaveData
        {
            SlotIndex        = dict["SlotIndex"].AsInt32(),
            SaveName         = dict["SaveName"].AsString(),
            SaveDate         = dict["SaveDate"].AsString(),
            PlayTimeSeconds  = dict["PlayTimeSeconds"].AsInt32(),
            SaveSystemType   = dict["SaveSystemType"].AsString(),
            MouseSensitivity = dict["MouseSensitivity"].AsSingle(),
            IsSoundEnabled   = dict["IsSoundEnabled"].AsBool(),
            TimeRemaining    = dict["TimeRemaining"].AsSingle(),
            MatchActive      = dict["MatchActive"].AsBool(),
            FragCounts       = fragCounts,
            PlayerHealth     = dict["PlayerHealth"].AsInt32(),
            WeaponIndex      = dict["WeaponIndex"].AsInt32(),
            WeaponAmmo       = ammoList.ToArray(),
            PlayerX          = dict["PlayerX"].AsSingle(),
            PlayerY          = dict["PlayerY"].AsSingle(),
            PlayerZ          = dict["PlayerZ"].AsSingle(),
            BestFrags        = dict["BestFrags"].AsInt32(),
            TotalKills       = dict["TotalKills"].AsInt32(),
            TotalDeaths      = dict["TotalDeaths"].AsInt32(),
            TotalGames       = dict["TotalGames"].AsInt32(),
        };
    }

    // ── ISaveSystem ───────────────────────────────────────────────────────────

    public void Save(SaveData data, int slotIndex)
    {
        EnsureSaveDir();
        byte[] bytes = GD.VarToBytes(Pack(data));
        using var file = FileAccess.Open(GetFilePath(slotIndex), FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"SaveSystem_B: cannot open '{GetFilePath(slotIndex)}' for writing.");
            return;
        }
        file.Store32((uint)bytes.Length);
        file.StoreBuffer(bytes);
    }

    public SaveData Load(int slotIndex)
    {
        string path = GetFilePath(slotIndex);
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        try
        {
            uint length = file.Get32();
            byte[] bytes = file.GetBuffer((long)length);
            var variant = GD.BytesToVar(bytes);
            if (variant.Obj is not Godot.Collections.Dictionary dict) return null;
            return Unpack(dict);
        }
        catch (System.Exception ex)
        {
            GD.PushError($"SaveSystem_B: error reading slot {slotIndex}: {ex.Message}");
            return null;
        }
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
