using System.Collections.Generic;

/// <summary>
/// Serialisable snapshot of the full game state stored in one save slot.
/// Uses only plain C# types so that all three serialisation backends
/// (JSON, binary, ConfigFile) can handle it without special adapters.
/// </summary>
public class SaveData
{
    // ── Slot metadata ─────────────────────────────────────────────────────────
    public int    SlotIndex       { get; set; } = 0;
    public string SaveName        { get; set; } = "New Save";
    /// <summary>ISO-8601 timestamp: "yyyy-MM-dd HH:mm:ss".</summary>
    public string SaveDate        { get; set; } = "";
    /// <summary>Total time the player has spent in-game this session (seconds).</summary>
    public int    PlayTimeSeconds { get; set; } = 0;
    /// <summary>Which ISaveSystem implementation wrote this file ("A", "B", or "C").</summary>
    public string SaveSystemType  { get; set; } = "A";

    // ── Settings ──────────────────────────────────────────────────────────────
    public float MouseSensitivity { get; set; } = 0.002f;
    public bool  IsSoundEnabled   { get; set; } = true;

    // ── Match state ───────────────────────────────────────────────────────────
    /// <summary>Seconds remaining on the match clock at time of save.</summary>
    public float TimeRemaining { get; set; } = 900f;
    public bool  MatchActive   { get; set; } = false;
    /// <summary>Maps peer-ID (as string) → frag count.</summary>
    public Dictionary<string, int> FragCounts { get; set; } = new();

    // ── Local player state ────────────────────────────────────────────────────
    public int   PlayerHealth  { get; set; } = 100;
    public int   WeaponIndex   { get; set; } = 0;
    /// <summary>Ammo remaining per weapon slot (index matches weapon order).</summary>
    public int[] WeaponAmmo    { get; set; } = System.Array.Empty<int>();
    public float PlayerX       { get; set; } = 0f;
    public float PlayerY       { get; set; } = 0f;
    public float PlayerZ       { get; set; } = 0f;
    /// <summary>Player body Y-axis rotation (yaw, radians).</summary>
    public float PlayerRotationY  { get; set; } = 0f;
    /// <summary>Camera X-axis rotation (pitch, radians). Clamped to ±Pi/2.5 by Player._Input.</summary>
    public float CameraRotationX  { get; set; } = 0f;

    // ── Career / best stats ───────────────────────────────────────────────────
    public int BestFrags   { get; set; } = 0;
    public int TotalKills  { get; set; } = 0;
    public int TotalDeaths { get; set; } = 0;
    public int TotalGames  { get; set; } = 0;
}
