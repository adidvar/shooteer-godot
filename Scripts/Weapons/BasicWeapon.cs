using Godot;
using System;

/// <summary>
/// Concrete weapon: Assault Rifle (hitscan + mesh tracer line).
/// </summary>
public partial class BasicWeapon : WeaponBase
{
	private double _cooldown = 0.0;

	public override void _Ready()
	{
		base._Ready();

		// Auto-create default stats if none assigned in Inspector
		if (Stats == null)
		{
			Stats = new WeaponStats
			{
				WeaponName = "Assault Rifle",
				Damage = 25,
				FireRate = 0.1f,
			};
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
		if (AimDetector == null || MuzzlePoint == null) return;

		_cooldown = Stats.FireRate;

		ShootSoundPlayer?.Play();

		AimDetector.ForceRaycastUpdate();
		Vector3 from = MuzzlePoint.GlobalPosition;
		Vector3 to = GetAimTarget();

		// Tracer line — visible even on miss
		WeaponHelpers.SpawnTracerLine(this, from, to);

		if (AimDetector.IsColliding())
		{
			Node collider = AimDetector.GetCollider() as Node;
			WeaponHelpers.SpawnImpact(this, Stats?.ImpactEffect, to);

			bool didHit = false;

			if (collider is Player hitMPPlayer && hitMPPlayer != OwnerPlayer)
			{
				OwnerPlayer?.RequestDamage(hitMPPlayer.GetPath(), Stats.Damage);
				didHit = true;
			}
			else if (collider != null)
			{
				var health = collider.GetNodeOrNull<HealthComponent>("HealthComponent");
				if (health != null)
				{
					health.TakeDamage(Stats.Damage);
					didHit = true;
				}
			}

			if (didHit)
				NotifyHit(false);
		}
	}

	// Tracer and impact logic moved to WeaponHelpers.cs
}
