using Godot;
using System.Collections.Generic;

public partial class GlobalSettings : Node
{
	public static GlobalSettings Instance { get; private set; }

	public float MouseSensitivity { get; set; } = 0.002f;
	public bool  IsSoundEnabled   { get; set; } = true;

	/// <summary>Index into <see cref="SaveManager.AllSystems"/> (0=A,1=B,2=C).</summary>
	public int SaveSystemIndex { get; set; } = 0;

	[Export]
	public Godot.Collections.Array<WeaponStats> WeaponStatsList { get; set; } = new();

	private readonly Dictionary<string, WeaponStats> _weaponRegistry = new();

	public override void _Ready()
	{
		if (Instance != null)
		{
			GD.PushWarning("GlobalSettings: duplicate autoload instance detected, removing.");
			QueueFree();
			return;
		}
		Instance = this;
		BuildWeaponRegistry();
		ApplySoundSettings();
	}

	private void BuildWeaponRegistry()
	{
		_weaponRegistry.Clear();
		foreach (var stats in WeaponStatsList)
			if (stats != null && !string.IsNullOrEmpty(stats.WeaponName))
				_weaponRegistry[stats.WeaponName] = stats;
	}

	public WeaponStats GetWeaponStats(string weaponName)
	{
		_weaponRegistry.TryGetValue(weaponName, out var stats);
		return stats;
	}

	public void ApplySoundSettings()
	{
		int bus = AudioServer.GetBusIndex("Master");
		if (bus >= 0)
			AudioServer.SetBusMute(bus, !IsSoundEnabled);
	}
}
