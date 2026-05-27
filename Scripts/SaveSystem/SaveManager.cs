using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// SaveManager — autoload singleton that orchestrates the entire save/load subsystem.
///
/// Responsibilities:
///   • Keeps one active <see cref="ISaveSystem"/> implementation (A / B / C).
///   • Manages up to <see cref="MaxSlots"/> named save slots.
///   • Gathers live game state from <see cref="GlobalSettings"/>, <see cref="MatchManager"/>,
///     and the local <see cref="Player"/> node before saving.
///   • Restores settings immediately on load; restores match state when the
///     game scene is active.
///   • Persists settings (sensitivity, sound, preferred save-system) to slot 0
///     automatically whenever they change.
/// </summary>
public partial class SaveManager : Node
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static SaveManager Instance { get; private set; }

    // ── Constants ─────────────────────────────────────────────────────────────
    public const int MaxSlots     = 5;  // slots 0-4 (slot 0 = settings auto-save)
    public const int SettingsSlot = 0;

    // ── Active save system ────────────────────────────────────────────────────
    private ISaveSystem _system = new SaveSystem_A();

    public ISaveSystem ActiveSystem => _system;

    public static readonly ISaveSystem[] AllSystems = new ISaveSystem[]
    {
        new SaveSystem_A(),
        new SaveSystem_B(),
        new SaveSystem_C(),
    };

    /// <summary>
    /// Switch the active save system.
    /// The preference is persisted to the settings slot immediately.
    /// </summary>
    public void SetSaveSystem(int index)
    {
        if (index < 0 || index >= AllSystems.Length) return;
        _system = AllSystems[index];
        if (GlobalSettings.Instance != null)
            GlobalSettings.Instance.SaveSystemIndex = index;
        SaveSettings();
    }

    // ── Session play-time tracking ────────────────────────────────────────────
    private double _sessionSeconds = 0.0;

    public override void _Process(double delta)
    {
        _sessionSeconds += delta;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        if (Instance != null)
        {
            GD.PushWarning("SaveManager: duplicate autoload — removing extra instance.");
            QueueFree();
            return;
        }
        Instance = this;

        // Load persisted settings from slot 0 on startup.
        LoadSettings();
    }

    // ── Settings persistence ──────────────────────────────────────────────────

    /// <summary>Save only settings-relevant fields to slot 0.</summary>
    public void SaveSettings()
    {
        var existing = _system.Load(SettingsSlot) ?? new SaveData { SlotIndex = SettingsSlot };

        existing.SaveName        = "Settings";
        existing.SaveDate        = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        existing.SaveSystemType  = GetSystemTag();
        existing.SlotIndex       = SettingsSlot;

        if (GlobalSettings.Instance != null)
        {
            existing.MouseSensitivity = GlobalSettings.Instance.MouseSensitivity;
            existing.IsSoundEnabled   = GlobalSettings.Instance.IsSoundEnabled;
        }

        _system.Save(existing, SettingsSlot);
    }

    /// <summary>Restore settings from slot 0 (called at startup).</summary>
    public void LoadSettings()
    {
        // Try all three systems in turn, since the user might have switched.
        SaveData data = null;
        foreach (var sys in AllSystems)
        {
            data = sys.Load(SettingsSlot);
            if (data != null) break;
        }
        if (data == null) return;

        // Restore save-system preference.
        int sysIdx = data.SaveSystemType switch { "B" => 1, "C" => 2, _ => 0 };
        _system = AllSystems[sysIdx];

        if (GlobalSettings.Instance == null) return;
        GlobalSettings.Instance.MouseSensitivity = data.MouseSensitivity;
        GlobalSettings.Instance.IsSoundEnabled   = data.IsSoundEnabled;
        GlobalSettings.Instance.SaveSystemIndex  = sysIdx;
        GlobalSettings.Instance.ApplySoundSettings();
    }

    // ── Slot management ───────────────────────────────────────────────────────

    /// <summary>Save the current game state to the specified slot (1-4).</summary>
    public bool SaveGame(int slotIndex, string saveName = null)
    {
        if (slotIndex < 1 || slotIndex >= MaxSlots)
        {
            GD.PushWarning($"SaveManager: invalid slot index {slotIndex} (valid: 1-{MaxSlots - 1}).");
            return false;
        }

        var data = CollectGameState();
        data.SlotIndex      = slotIndex;
        data.SaveName       = saveName ?? $"Slot {slotIndex}";
        data.SaveDate       = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.PlayTimeSeconds = (int)_sessionSeconds;
        data.SaveSystemType = GetSystemTag();

        _system.Save(data, slotIndex);
        GD.Print($"SaveManager: saved slot {slotIndex} via {_system.SystemName}.");
        return true;
    }

    /// <summary>
    /// Load the game state from the specified slot and apply it.
    /// Settings are applied immediately; match/player state is returned
    /// for the caller (e.g. <see cref="MatchManager"/>) to consume.
    /// </summary>
    public SaveData LoadGame(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex >= MaxSlots) return null;

        // Try the active system first, then fall back to the others.
        SaveData data = _system.Load(slotIndex);
        if (data == null)
        {
            foreach (var sys in AllSystems)
            {
                data = sys.Load(slotIndex);
                if (data != null) break;
            }
        }
        if (data == null)
        {
            GD.PushWarning($"SaveManager: slot {slotIndex} not found.");
            return null;
        }

        // Restore settings immediately.
        if (GlobalSettings.Instance != null)
        {
            GlobalSettings.Instance.MouseSensitivity = data.MouseSensitivity;
            GlobalSettings.Instance.IsSoundEnabled   = data.IsSoundEnabled;
            GlobalSettings.Instance.ApplySoundSettings();
        }

        GD.Print($"SaveManager: loaded slot {slotIndex}.");
        return data;
    }

    public bool SlotExists(int slotIndex)    => _system.SlotExists(slotIndex)
        || new SaveSystem_A().SlotExists(slotIndex)
        || new SaveSystem_B().SlotExists(slotIndex)
        || new SaveSystem_C().SlotExists(slotIndex);

    public void DeleteSlot(int slotIndex)
    {
        // Delete from all systems to avoid stale files from a previous system.
        foreach (var sys in AllSystems)
            sys.DeleteSlot(slotIndex);
        GD.Print($"SaveManager: deleted slot {slotIndex}.");
    }

    /// <summary>
    /// Returns basic metadata for all non-empty slots (1-4).
    /// Each entry is (slotIndex, saveName, saveDate, playTimeSeconds, isEmpty).
    /// </summary>
    public List<(int slot, string name, string date, int playtime, bool empty)> GetAllSlotsMeta()
    {
        var result = new List<(int, string, string, int, bool)>();
        for (int i = 1; i < MaxSlots; i++)
        {
            if (!SlotExists(i))
            {
                result.Add((i, $"Slot {i}", "", 0, true));
                continue;
            }
            // Try to read only metadata cheaply.
            SaveData d = null;
            foreach (var sys in AllSystems)
            {
                d = sys.Load(i);
                if (d != null) break;
            }
            if (d == null)
                result.Add((i, $"Slot {i}", "", 0, true));
            else
                result.Add((i, d.SaveName, d.SaveDate, d.PlayTimeSeconds, false));
        }
        return result;
    }

    // ── Auto-save ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Automatic save triggered by MatchManager at match end.
    /// Updates career stats and re-saves to slot 0 (settings/career slot).
    /// </summary>
    public void AutoSaveMatchEnd(int myFrags, bool isBestGame)
    {
        var existing = _system.Load(SettingsSlot) ?? new SaveData { SlotIndex = SettingsSlot };
        existing.TotalGames  += 1;
        existing.TotalKills  += myFrags;
        if (isBestGame) existing.BestFrags = myFrags;

        if (GlobalSettings.Instance != null)
        {
            existing.MouseSensitivity = GlobalSettings.Instance.MouseSensitivity;
            existing.IsSoundEnabled   = GlobalSettings.Instance.IsSoundEnabled;
        }

        existing.SaveDate       = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        existing.PlayTimeSeconds = (int)_sessionSeconds;
        existing.SaveSystemType  = GetSystemTag();
        existing.SaveName        = "Settings";

        _system.Save(existing, SettingsSlot);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetSystemTag() => _system switch
    {
        SaveSystem_B => "B",
        SaveSystem_C => "C",
        _            => "A",
    };

    /// <summary>Gather a snapshot of the current live game state.</summary>
    private SaveData CollectGameState()
    {
        var data = new SaveData();

        // Settings
        if (GlobalSettings.Instance != null)
        {
            data.MouseSensitivity = GlobalSettings.Instance.MouseSensitivity;
            data.IsSoundEnabled   = GlobalSettings.Instance.IsSoundEnabled;
        }

        // Career stats from settings slot
        var career = _system.Load(SettingsSlot);
        if (career != null)
        {
            data.BestFrags   = career.BestFrags;
            data.TotalKills  = career.TotalKills;
            data.TotalDeaths = career.TotalDeaths;
            data.TotalGames  = career.TotalGames;
        }

        // Match state
        var matchMgr = GetNodeOrNull<MatchManager>("/root/Main/MatchManager");
        if (matchMgr != null)
        {
            var snapshot = matchMgr.GetSaveSnapshot();
            data.TimeRemaining = snapshot.timeRemaining;
            data.MatchActive   = snapshot.matchActive;
            data.FragCounts    = snapshot.fragCounts;
        }

        // Local player state
        var localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            data.PlayerHealth = localPlayer.HealthComp?.CurrentHealth ?? 100;
            data.WeaponIndex  = localPlayer.WeaponCtrl?.CurrentWeaponIndex ?? 0;
            data.WeaponAmmo   = localPlayer.WeaponCtrl?.GetAmmoData() ?? Array.Empty<int>();
            var pos           = localPlayer.GlobalPosition;
            data.PlayerX = pos.X;
            data.PlayerY = pos.Y;
            data.PlayerZ = pos.Z;
            // Body yaw (Y) is on the CharacterBody3D itself.
            data.PlayerRotationY = localPlayer.Rotation.Y;
            // Camera pitch (X) is on the child Camera3D node.
            var cam = localPlayer.GetNodeOrNull<Camera3D>("Camera3D");
            data.CameraRotationX = cam?.Rotation.X ?? 0f;
        }

        return data;
    }

    private Player FindLocalPlayer()
    {
        // Players are manual children of Map/Players (see Map.cs).
        var container = GetNodeOrNull("/root/Main/LevelContainer/Map/Players");
        if (container == null) return null;
        foreach (Node child in container.GetChildren())
            if (child is Player p && p.IsMultiplayerAuthority())
                return p;
        return null;
    }
}
