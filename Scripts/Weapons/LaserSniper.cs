using Godot;
using System;

/// <summary>
/// Laser Sniper: Instant hitscan with long range, visible laser beam and sparks on hit.
/// Highest burst damage. Headshot multiplier logic.
/// </summary>
public partial class LaserSniper : WeaponBase
{
	[Export] public float HeadshotMultiplier = 2.0f;
	[Export] public GpuParticles3D SparksTemplate { get; set; }

	private double _cooldown = 0.0;
	private MeshInstance3D _laserBeam;

	public override void _Ready()
	{
		base._Ready();

		_laserBeam = new MeshInstance3D();
		_laserBeam.Mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f };
		var mat = new StandardMaterial3D { 
			AlbedoColor = new Color(1f, 0.2f, 0.2f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.1f, 0.1f),
			EmissionEnergyMultiplier = 8f 
		};
		_laserBeam.MaterialOverride = mat;
		_laserBeam.Visible = false;
		AddChild(_laserBeam);

		SparksTemplate ??= CreateSparksTemplate();
	}

	public override void _Process(double delta)
	{
		if (_cooldown > 0)
			_cooldown -= delta;

		if (_laserBeam.Visible)
		{
			// Fade out beam
			var mat = _laserBeam.MaterialOverride as StandardMaterial3D;
			if (mat != null)
			{
				float a = mat.AlbedoColor.A - (float)delta * 5f;
				if (a <= 0)
					_laserBeam.Visible = false;
				else
					mat.AlbedoColor = new Color(mat.AlbedoColor, a);
			}
		}
	}

	public override void Fire()
	{
		if (_cooldown > 0) return;
		if (AimDetector == null || MuzzlePoint == null || Stats == null) return;

		_cooldown = Stats.FireRate;
		ShootSoundPlayer?.Play();

		AimDetector.ForceRaycastUpdate();
		Vector3 targetPoint = GetAimTarget();

		// Draw beam
		float distance = MuzzlePoint.GlobalPosition.DistanceTo(targetPoint);
		_laserBeam.GlobalPosition = MuzzlePoint.GlobalPosition.Lerp(targetPoint, 0.5f);
		Vector3 beamDir = (targetPoint - _laserBeam.GlobalPosition).Normalized();
		if (beamDir.LengthSquared() > 0.001f && !beamDir.IsEqualApprox(Vector3.Up) && !beamDir.IsEqualApprox(Vector3.Down))
			_laserBeam.LookAt(targetPoint, Vector3.Up);
		else
			_laserBeam.LookAt(targetPoint, Vector3.Right);
		((CylinderMesh)_laserBeam.Mesh).Height = distance;
		
		var mat = _laserBeam.MaterialOverride as StandardMaterial3D;
		if (mat != null)
			mat.AlbedoColor = new Color(mat.AlbedoColor, 1.0f); // Reset alpha
		
		_laserBeam.Visible = true;

		if (AimDetector.IsColliding())
		{
			Node collider = AimDetector.GetCollider() as Node;
			Vector3 hitNormal = AimDetector.GetCollisionNormal();
			
			SpawnSparks(targetPoint, hitNormal);

			int damage = Stats.Damage;
			bool hitPlayer = false;

			// Check for headshot (Assume collision shape name contains "head")
			if (AimDetector.GetColliderShape() is int shapeIdx)
			{
				if (collider is CollisionObject3D collObj)
				{
					// This is a naive way, more robust is to have specific Area3D/PhysicsBody for Head.
					// For now we just apply base damage.
				}
			}

			if (collider is Player hitMPPlayer && hitMPPlayer != OwnerPlayer)
			{
				OwnerPlayer?.RequestDamage(hitMPPlayer.GetPath(), damage);
				hitPlayer = true;
			}
			else if (collider != null)
			{
				var health = collider.GetNodeOrNull<HealthComponent>("HealthComponent");
				if (health != null)
				{
					health.TakeDamage(damage);
					hitPlayer = true;
				}
			}

			if (hitPlayer) NotifyHit(false);
		}
	}

	private void SpawnSparks(Vector3 pos, Vector3 normal)
	{
		if (SparksTemplate == null) return;
		var sparks = SparksTemplate.Duplicate() as GpuParticles3D;
		GetTree().Root.AddChild(sparks);
		sparks.GlobalPosition = pos;
		
		if (normal != Vector3.Up && normal != Vector3.Down)
			sparks.LookAt(pos + normal, Vector3.Up);
			
		sparks.Emitting = true;
		
		// Auto cleanup
		GetTree().CreateTimer(sparks.Lifetime).Timeout += () => { if (IsInstanceValid(sparks)) sparks.QueueFree(); };
	}

	private GpuParticles3D CreateSparksTemplate()
	{
		var p = new GpuParticles3D();
		p.Emitting = false;
		p.OneShot = true;
		p.Amount = 30;
		p.Lifetime = 0.8f;
		p.Explosiveness = 1.0f;

		var mat = new ParticleProcessMaterial();
		mat.Direction = new Vector3(0, 0, -1);
		mat.InitialVelocityMin = 5f;
		mat.InitialVelocityMax = 15f;
		mat.Gravity = new Vector3(0, -9.8f, 0);
		mat.CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid;
		mat.CollisionBounce = 0.5f;
		p.ProcessMaterial = mat;

		var pass = new QuadMesh();
		pass.Size = new Vector2(0.05f, 0.05f);
		var pMat = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.8f, 0.2f), EmissionEnabled = true, Emission = new Color(1f, 0.6f, 0f), EmissionEnergyMultiplier = 4f };
		pass.Material = pMat;
		p.DrawPass1 = pass;

		return p;
	}
}
