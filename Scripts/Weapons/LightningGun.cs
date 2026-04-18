using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Lightning Gun: Shoots a beam that hits a wall, reflects, and damages enemies in a cone over time.
/// </summary>
public partial class LightningGun : WeaponBase
{
	[Export] public float TickRate = 0.1f;
	[Export] public float ConeAngleDegrees = 45f;
	[Export] public float ConeLength = 15f;

	private MeshInstance3D _beamMesh;
	private GpuParticles3D _coneParticles;
	private GpuParticles3D _beamParticles;
	private double _tickTimer = 0.0;
	private bool _isFiring = false;

	public override void _Ready()
	{
		base._Ready();

		if (Stats == null)
		{
			Stats = new WeaponStats
			{
				WeaponName = "Lightning Gun",
				Damage = 5,
				FireRate = 0.05f,
			};
		}

		// Setup Beam visual
		_beamMesh = new MeshInstance3D();
		_beamMesh.Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 1f };
		var mat = new StandardMaterial3D { AlbedoColor = new Color(0, 0.8f, 1f), EmissionEnabled = true, Emission = new Color(0, 0.5f, 1f), EmissionEnergyMultiplier = 4f };
		_beamMesh.MaterialOverride = mat;
		_beamMesh.Visible = false;
		AddChild(_beamMesh);

		// Setup cone particles
		_coneParticles = new GpuParticles3D();
		_coneParticles.Emitting = false;
		_coneParticles.Amount = 60;
		_coneParticles.Lifetime = 0.5f;
		_coneParticles.TopLevel = true;
		_coneParticles.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		
		var pProcess = new ParticleProcessMaterial();
		pProcess.Direction = new Vector3(0, 0, -1);
		pProcess.Spread = ConeAngleDegrees / 2f;
		pProcess.InitialVelocityMin = 5f;
		pProcess.InitialVelocityMax = 15f;
		pProcess.Gravity = Vector3.Zero;
		pProcess.ScaleCurve = new CurveTexture { Curve = new Curve() };
		var scaleCurve = (Curve)pProcess.ScaleCurve.Get("curve");
		scaleCurve.AddPoint(new Vector2(0, 1));
		scaleCurve.AddPoint(new Vector2(1, 0));
		_coneParticles.ProcessMaterial = pProcess;

		var pass = new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.03f) };
		var pMat = new StandardMaterial3D 
		{ 
			AlbedoColor = new Color(0.2f, 0.8f, 1f), 
			EmissionEnabled = true, 
			Emission = new Color(0.1f, 0.6f, 1f), 
			EmissionEnergyMultiplier = 4f, 
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded 
		};
		pass.Material = pMat;
		_coneParticles.DrawPass1 = pass;
		AddChild(_coneParticles);

		// Setup beam particles
		_beamParticles = new GpuParticles3D();
		_beamParticles.Emitting = false;
		_beamParticles.Amount = 100;
		_beamParticles.Lifetime = 0.3f;
		_beamParticles.TopLevel = true;
		_beamParticles.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		
		var bpProcess = new ParticleProcessMaterial();
		bpProcess.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		bpProcess.EmissionBoxExtents = new Vector3(0.1f, 1f, 0.1f);
		bpProcess.Gravity = Vector3.Zero;
		bpProcess.ScaleCurve = new CurveTexture { Curve = scaleCurve }; // Reuse shrinking curve
		_beamParticles.ProcessMaterial = bpProcess;

		var bpPass = new BoxMesh { Size = new Vector3(0.015f, 0.015f, 0.015f) };
		var bpMat = new StandardMaterial3D 
		{ 
			AlbedoColor = new Color(0.1f, 0.5f, 1f), 
			EmissionEnabled = true, 
			Emission = new Color(0.2f, 0.6f, 1f), 
			EmissionEnergyMultiplier = 2f, 
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded 
		};
		bpPass.Material = bpMat;
		_beamParticles.DrawPass1 = bpPass;
		AddChild(_beamParticles);
	}

	public override void _Process(double delta)
	{
		// Snapshot and immediately reset the flag so Fire() must be called every
		// frame to keep the beam alive. This is order-independent: it doesn't
		// matter whether WeaponController._Process runs before or after us.
		bool wasFiring = _isFiring;
		_isFiring = false;

		if (!wasFiring)
		{
			_beamMesh.Visible = false;
			_coneParticles.Emitting = false;
			_beamParticles.Emitting = false;
			if (ShootSoundPlayer != null && ShootSoundPlayer.Playing)
				ShootSoundPlayer.Stop();
			return;
		}

		_tickTimer -= delta;
		if (_tickTimer <= 0)
		{
			_tickTimer = TickRate;
			ApplyConeDamage();
		}
	}

	public override void Fire()
	{
		if (AimDetector == null || MuzzlePoint == null || Stats == null) return;

		_isFiring = true;
		_beamMesh.Visible = true;

		if (!ShootSoundPlayer.Playing)
			ShootSoundPlayer.Play();

		AimDetector.ForceRaycastUpdate();
		Vector3 targetPoint = GetAimTarget();

		// Draw beam
		float distance = MuzzlePoint.GlobalPosition.DistanceTo(targetPoint);
		_beamMesh.GlobalPosition = MuzzlePoint.GlobalPosition.Lerp(targetPoint, 0.5f);
		if (distance > 0.001f)
		{
			Vector3 dir = (targetPoint - MuzzlePoint.GlobalPosition).Normalized();
			if (dir != Vector3.Up && dir != Vector3.Down)
				_beamMesh.LookAt(targetPoint, Vector3.Up);
			else
				_beamMesh.LookAt(targetPoint, Vector3.Right);
			_beamMesh.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);
		}
		
		var cyl = (CylinderMesh)_beamMesh.Mesh;
		cyl.Height = distance;
		// Jitter thickness dynamically
		cyl.TopRadius = (float)GD.RandRange(0.02, 0.08);
		cyl.BottomRadius = (float)GD.RandRange(0.02, 0.08);

		// Draw beam particles
		_beamParticles.GlobalPosition = _beamMesh.GlobalPosition;
		_beamParticles.GlobalBasis = _beamMesh.GlobalBasis;
		var bMat = (ParticleProcessMaterial)_beamParticles.ProcessMaterial;
		bMat.EmissionBoxExtents = new Vector3(0.1f, distance / 2f, 0.1f);
		_beamParticles.Emitting = true;

		if (AimDetector.IsColliding())
		{
			Vector3 hitNormal = AimDetector.GetCollisionNormal();
			Vector3 rayDir = (targetPoint - MuzzlePoint.GlobalPosition).Normalized();
			Vector3 reflectDir = rayDir.Bounce(hitNormal).Normalized();

			// Store information for tick
			_lastHitPoint = targetPoint;
			_lastReflectDir = reflectDir;
			_isCollidingThisFrame = true;

			// Particles
			_coneParticles.GlobalPosition = targetPoint;
			if (reflectDir.Length() > 0.01f && reflectDir != Vector3.Up && reflectDir != Vector3.Down)
				_coneParticles.LookAt(targetPoint + reflectDir, Vector3.Up);
			else
				_coneParticles.LookAt(targetPoint + reflectDir, Vector3.Right);
			_coneParticles.Emitting = true;
		}
		else
		{
			_isCollidingThisFrame = false;
			_coneParticles.Emitting = false;
		}
	}

	private Vector3 _lastHitPoint;
	private Vector3 _lastReflectDir;
	private bool _isCollidingThisFrame;

	private void ApplyConeDamage()
	{
		if (!_isCollidingThisFrame) return;

		var spaceState = GetWorld3D().DirectSpaceState;
		var shape = new SphereShape3D { Radius = ConeLength };
		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = shape,
			Transform = new Transform3D(Basis.Identity, _lastHitPoint)
		};

		var hits = spaceState.IntersectShape(query);
		bool hitAnyone = false;
		
		foreach (var hit in hits)
		{
			var body = hit["collider"].As<Node3D>();
			if (body == null || body == OwnerPlayer) continue;

			Vector3 toBody = (body.GlobalPosition - _lastHitPoint).Normalized();
			float dot = _lastReflectDir.Dot(toBody);
			if (dot > Mathf.Cos(Mathf.DegToRad(ConeAngleDegrees / 2f)))
			{
				if (body is Player p)
				{
					OwnerPlayer?.RequestDamage(p.GetPath(), Stats.Damage);
					hitAnyone = true;
				}
				else
				{
					var hp = body.GetNodeOrNull<HealthComponent>("HealthComponent");
					if (hp != null)
					{
						hp.TakeDamage(Stats.Damage);
						hitAnyone = true;
					}
				}
			}
		}

		if (hitAnyone) NotifyHit(false);
	}
}
