using Godot;
using System;

/// <summary>
/// Homing Rocket Weapon: Spawns a physical Rocket projectile that homes in on enemies in its cone.
/// </summary>
public partial class HomingRocketWeapon : WeaponBase
{
	[Export]
	public PackedScene RocketScene { get; set; }

	// Fallback speed and blast radius if missing stats
	[Export] public float RocketSpeed = 15f;
	[Export] public float ExplosionRadius = 5f;

	private double _cooldown = 0.0;

	public override void _Ready()
	{
		base._Ready();
		
		// If no rocket scene provided, we can dynamically compile or throw error.
		// For prototype, we'll try to find a scene or we must rely on the Inspector.
		if (RocketScene == null)
		{
			// Try to load a generic if exists
			// RocketScene = GD.Load<PackedScene>("res://Scenes/Weapons/Projectiles/Rocket.tscn");
		}
	}

	public override void _Process(double delta)
	{
		if (_cooldown > 0)
			_cooldown -= delta;
	}

	public override void Fire()
	{
		if (_cooldown > 0) return;
		if (MuzzlePoint == null) return;
		
		_cooldown = Stats != null ? Stats.FireRate : 1.0f;
		ShootSoundPlayer?.Play();

		// For prototype without an attached scene, we can instantiate the Node via script.
		// But CharacterBody requires collision shapes, so it's better if we just create the node.
		Rocket rocket;
		if (RocketScene != null)
		{
			rocket = RocketScene.Instantiate<Rocket>();
		}
		else
		{
			// Stub instantiation if no scene provided
			rocket = new Rocket();
		}

		rocket.OwnerPlayer = OwnerPlayer;
		rocket.Damage = Stats != null ? Stats.Damage : 50;
		rocket.Speed = RocketSpeed;
		rocket.ExplosionRadius = ExplosionRadius;
		
		// Spawn exactly at muzzle
		GetTree().Root.AddChild(rocket);
		rocket.GlobalPosition = MuzzlePoint.GlobalPosition;

		// Orient towards aim
		if (AimDetector != null)
		{
			AimDetector.ForceRaycastUpdate();
			Vector3 aimTarget = GetAimTarget();
			rocket.LookAt(aimTarget, Vector3.Up);
		}
		else
		{
			rocket.GlobalTransform = MuzzlePoint.GlobalTransform;
		}
	}
}
