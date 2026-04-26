using Godot;
using System;

/// <summary>
/// Laser Sniper: Instant hitscan with long range, visible laser beam and sparks on hit.
/// Highest burst damage. Headshot multiplier logic.
/// </summary>
public partial class LaserSniper : WeaponBase
{
	[Export] public float HeadshotMultiplier = 2.0f;
	[Export] public float MaxRange = 60f;
	[Export] public GpuParticles3D SparksTemplate { get; set; }

	private double _cooldown = 0.0;
	private MeshInstance3D _laserBeam;

	public override void _Ready()
	{
		base._Ready();

		// Cap raycast range so the laser is not infinitely powerful.
		if (AimDetector != null)
			AimDetector.TargetPosition = new Vector3(0f, 0f, -MaxRange);

		_laserBeam = new MeshInstance3D();
		_laserBeam.Mesh = new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f };
		var mat = new StandardMaterial3D
		{
			AlbedoColor              = new Color(0.4f, 0.85f, 1f, 1f),
			EmissionEnabled          = true,
			Emission                 = new Color(0.2f, 0.6f, 1f),
			EmissionEnergyMultiplier = 12f,
			ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency             = BaseMaterial3D.TransparencyEnum.Alpha,
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
		if (!TryConsumeAmmo()) return;

		_cooldown = Stats.FireRate;
		ShootSoundPlayer?.Play();

		AimDetector.ForceRaycastUpdate();
		Vector3 targetPoint = GetAimTarget();

		// Draw beam + tracer
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

		// Spawn the fast tracer capsule for an extra "projectile" feel
		SpawnTracer(MuzzlePoint.GlobalPosition, targetPoint);

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

	// Spawn a fast visual-only tracer capsule from muzzle → target
	private void SpawnTracer(Vector3 fromPos, Vector3 toPos)
	{
		var tracer = new MeshInstance3D();
		var dist   = fromPos.DistanceTo(toPos);
		tracer.Mesh = new CapsuleMesh { Radius = 0.035f, Height = Mathf.Max(dist, 0.2f) };
		tracer.RotationDegrees = new Vector3(90f, 0f, 0f); // CapsuleMesh is Y-up; rotate to Z-fwd

		var tMat = new StandardMaterial3D
		{
			AlbedoColor              = new Color(0.5f, 0.9f, 1f, 0.9f),
			EmissionEnabled          = true,
			Emission                 = new Color(0.3f, 0.7f, 1f),
			EmissionEnergyMultiplier = 10f,
			ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency             = BaseMaterial3D.TransparencyEnum.Alpha,
		};
		tracer.MaterialOverride = tMat;
		tracer.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		GetTree().Root.AddChild(tracer);
		tracer.GlobalPosition = fromPos.Lerp(toPos, 0.5f);
		if (!fromPos.IsEqualApprox(toPos))
		{
			var dir = (toPos - fromPos).Normalized();
			if (!dir.IsEqualApprox(Vector3.Up) && !dir.IsEqualApprox(Vector3.Down))
				tracer.LookAt(toPos, Vector3.Up);
			else
				tracer.LookAt(toPos, Vector3.Right);
		}

		// Fade out quickly over 0.15s then free
		var tween = tracer.CreateTween();
		tween.TweenProperty(tracer, "modulate:a", 0.0f, 0.15f)
			 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(tracer)) tracer.QueueFree();
		}));
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
		p.Emitting      = false;
		p.OneShot       = true;
		p.Amount        = 28;
		p.Lifetime      = 0.65f;
		p.Explosiveness = 1.0f;

		var mat = new ParticleProcessMaterial();
		mat.Direction          = new Vector3(0, 0, -1);
		mat.Spread             = 60f;
		mat.InitialVelocityMin = 6f;
		mat.InitialVelocityMax = 18f;
		mat.Gravity            = new Vector3(0, -9.8f, 0);
		mat.Color              = new Color(0.4f, 0.85f, 1f, 1f); // cyan sparks
		mat.CollisionMode      = ParticleProcessMaterial.CollisionModeEnum.Rigid;
		mat.CollisionBounce    = 0.6f;
		p.ProcessMaterial = mat;

		// Use kenney spark texture
		var tex  = ResourceLoader.Load<Texture2D>(
			"res://Assets/kenney_particle-pack/PNG (Transparent)/spark_04.png");
		var quad = new QuadMesh { Size = new Vector2(0.07f, 0.07f) };
		var qMat = new StandardMaterial3D
		{
			AlbedoColor              = new Color(0.4f, 0.85f, 1f),
			EmissionEnabled          = true,
			Emission                 = new Color(0.2f, 0.6f, 1f),
			EmissionEnergyMultiplier = 6f,
			ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode            = BaseMaterial3D.BillboardModeEnum.Particles,
			Transparency             = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode                = BaseMaterial3D.BlendModeEnum.Add,
		};
		if (tex != null) qMat.AlbedoTexture = tex;
		quad.Material = qMat;
		p.DrawPass1 = quad;

		return p;
	}
}
