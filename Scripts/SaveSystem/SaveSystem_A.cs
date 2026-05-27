using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// SaveSystem_A — JSON-based implementation.
/// Each slot is saved as a human-readable, indented JSON file under
/// <c>user://saves/</c>.  File names follow the pattern <c>slot_N.json</c>.
/// </summary>
public class SaveSystem_A : ISaveSystem
{
    private const string SaveDir       = "user://saves/";
    private const string FileExtension = ".json";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented              = true,
        DefaultIgnoreCondition     = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public string SystemName        => "JSON (A)";
    public string SystemDescription => "Human-readable JSON files — easy to inspect and edit";

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
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        using var file = FileAccess.Open(GetFilePath(slotIndex), FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"SaveSystem_A: cannot open '{GetFilePath(slotIndex)}' for writing.");
            return;
        }
        file.StoreString(json);
    }

    public SaveData Load(int slotIndex)
    {
        string path = GetFilePath(slotIndex);
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        try
        {
            return JsonSerializer.Deserialize<SaveData>(file.GetAsText(), _jsonOptions);
        }
        catch (JsonException ex)
        {
            GD.PushError($"SaveSystem_A: JSON parse error in slot {slotIndex}: {ex.Message}");
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
