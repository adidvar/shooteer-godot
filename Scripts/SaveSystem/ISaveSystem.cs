/// <summary>
/// Common contract for all save-system implementations (A, B, C).
/// Each implementation serialises a <see cref="SaveData"/> object to a
/// different format but exposes the same operations to the rest of the game.
/// </summary>
public interface ISaveSystem
{
    /// <summary>Human-readable implementation name shown in Settings.</summary>
    string SystemName { get; }

    /// <summary>One-line description shown in Settings.</summary>
    string SystemDescription { get; }

    /// <summary>Persist <paramref name="data"/> to the given slot index.</summary>
    void Save(SaveData data, int slotIndex);

    /// <summary>
    /// Load data from the given slot.
    /// Returns <c>null</c> if the slot does not exist or cannot be read.
    /// </summary>
    SaveData Load(int slotIndex);

    /// <summary>Returns true if a save file exists for the given slot.</summary>
    bool SlotExists(int slotIndex);

    /// <summary>Permanently removes the save file for the given slot.</summary>
    void DeleteSlot(int slotIndex);
}
