using Godot;
using System;

[GlobalClass]
public partial class WeaponStats : Resource
{
	[Export]
	public string WeaponName { get; set; } = "Weapon";

	[Export]
	public int Damage { get; set; } = 25;

	[Export]
	public float FireRate { get; set; } = 0.15f;

	[ExportCategory("Ammo")]
	/// <summary>Maximum ammo this weapon can hold.</summary>
	[Export]
	public int MaxAmmo { get; set; } = 30;

	/// <summary>How much ammo a single pickup restores.</summary>
	[Export]
	public int AmmoPerPickup { get; set; } = 15;

	/// <summary>
	/// Weapon slot index (0-based). AmmoPickup nodes match on this value
	/// so only the correct ammo box restores ammo for this weapon.
	/// </summary>
	[Export]
	public int WeaponIndex { get; set; } = 0;

	[ExportCategory("FX")]
	[Export]
	public AudioStream ShootSound { get; set; }

	[Export]
	public PackedScene ImpactEffect { get; set; }
}
