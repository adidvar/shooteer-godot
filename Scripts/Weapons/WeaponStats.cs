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

	[ExportCategory("FX")]
	[Export]
	public AudioStream ShootSound { get; set; }

	[Export]
	public PackedScene ImpactEffect { get; set; }
}
