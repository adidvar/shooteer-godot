using Godot;
using System;

/// <summary>
/// Bullet projectile – CharacterBody3D that travels in a given direction,
/// deals damage on impact, and spawns a GPUParticles3D burst.
/// </summary>
public partial class Bullet : CharacterBody3D
{
	private const string ImpactScenePath = "res://Scenes/Weapons/Projectiles/BulletImpact.tscn";

	[Export] public float Speed = 60f;

	private Vector3 _direction;
	private int _damage;
	private Player _shooter;
	private bool _hit = false;

	private static PackedScene _impactScene;

	public override void _Ready()
	{
		// Cache impact scene (static so it's only loaded once)
		if (_impactScene == null)
			_impactScene = GD.Load<PackedScene>(ImpactScenePath);

		// Auto-free after a timeout so stray bullets don't live forever
		var timer = new Timer { WaitTime = 5.0, OneShot = true, Autostart = true };
		timer.Timeout += QueueFree;
		AddChild(timer);
	}

	/// <summary>Called by the weapon right after instantiation to initialise flight parameters.</summary>
	public void Launch(Vector3 direction, int damage, Player shooter)
	{
		_direction = direction.Normalized();
		_damage = damage;
		_shooter = shooter;
		Velocity = _direction * Speed;

		// Rotate bullet to face travel direction
		if (_direction != Vector3.Zero)
			LookAt(GlobalPosition + _direction, Vector3.Up);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hit) return;

		var collision = MoveAndCollide(Velocity * (float)delta);
		if (collision != null)
		{
			_hit = true;
			OnHit(collision);
		}
	}

	private void OnHit(KinematicCollision3D collision)
	{
		// Spawn impact particles at the collision point
		SpawnImpact(collision.GetPosition());

		// Try to deal damage to whatever we hit
		var collider = collision.GetCollider();
		if (collider is Player hitPlayer && hitPlayer != _shooter)
		{
			// Route through shooter's RPC so server handles damage
			_shooter?.RequestDamage(hitPlayer.GetPath(), _damage);
		}
		else if (collider is Node node)
		{
			// Generic: look for HealthComponent in the hit node
			var health = node.GetNodeOrNull<HealthComponent>("HealthComponent");
			health?.TakeDamage(_damage);
		}

		QueueFree();
	}

	private void SpawnImpact(Vector3 position)
	{
		if (_impactScene == null) return;

		var impact = _impactScene.Instantiate<Node3D>();
		if (impact == null) return;

		GetTree().Root.AddChild(impact);
		impact.GlobalPosition = position;
	}
}
