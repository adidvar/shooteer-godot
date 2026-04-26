using Godot;
using System;

public partial class Rocket : CharacterBody3D
{
	public float Speed          = 14f;
	public int   Damage         = 50;
	public float TurnSpeed      = 2.2f;
	public float ExplosionRadius= 5f;
	public Player OwnerPlayer;

	private Node3D _target;
	private double _lifetime   = 6.0;
	private float  _spawnTimer = 0.25f; // grace period to clear the muzzle

	private const string ParticlePath = "res://Assets/kenney_particle-pack/PNG (Transparent)/";

	public override void _Ready()
	{
		// ── Visual ───────────────────────────────────────────────────────────
		var bodyMesh = new MeshInstance3D();
		bodyMesh.Mesh = new CapsuleMesh { Radius = 0.055f, Height = 0.25f };
		bodyMesh.RotationDegrees = new Vector3(90f, 0f, 0f); // nose forward (-Z)
		bodyMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		var rocketMat = new StandardMaterial3D
		{
			AlbedoColor              = new Color(0.9f, 0.45f, 0.1f),
			EmissionEnabled          = true,
			Emission                 = new Color(1f, 0.3f, 0.0f),
			EmissionEnergyMultiplier = 4f,
			ShadingMode              = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		bodyMesh.MaterialOverride = rocketMat;
		AddChild(bodyMesh);

		// Small engine-glow light
		var engineLight = new OmniLight3D
		{
			LightColor    = new Color(1f, 0.5f, 0.1f),
			LightEnergy   = 1.8f,
			OmniRange     = 2.5f,
			ShadowEnabled = false,
		};
		AddChild(engineLight);

		// Exhaust trail
		SpawnTrailParticles();

		// ── Collision ────────────────────────────────────────────────────────
		var shape = new CollisionShape3D();
		shape.Shape = new SphereShape3D { Radius = 0.10f };
		AddChild(shape);
	}

	private void FindTarget()
	{
		if (_target != null) return;

		var spaceState = GetWorld3D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters3D
		{
			Shape = new SphereShape3D { Radius = 10f },
			Transform = new Transform3D(Basis.Identity, GlobalPosition)
		};

		var hits = spaceState.IntersectShape(query);
		foreach (var hit in hits)
		{
			var body = hit["collider"].As<Node3D>();
			if (body == null || body == OwnerPlayer) continue;

			if (body is Player || body.HasNode("HealthComponent"))
			{
				Vector3 toTarget = (body.GlobalPosition - GlobalPosition).Normalized();
				if (GlobalTransform.Basis.Z.Dot(-toTarget) > 0.5f) // Z is backward in Godot
				{
					_target = body;
					break;
				}
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		_lifetime -= delta;
		if (_lifetime <= 0) { Explode(); return; }

		if (_spawnTimer > 0f)
		{
			_spawnTimer -= (float)delta;

			// Steer even during grace period
			if (_target != null && IsInstanceValid(_target))
				SteerToTarget((float)delta);

			// Move with collision — only skip if we hit our own player
			Velocity = -GlobalTransform.Basis.Z * Speed;
			var c = MoveAndCollide(Velocity * (float)delta);
			if (c != null)
			{
				var col = c.GetCollider();
				// Hit something that isn't us — explode immediately
				if (!(col is Node3D n3 && n3 == OwnerPlayer))
					Explode();
			}
			return;
		}

		FindTarget();

		if (_target != null && IsInstanceValid(_target))
			SteerToTarget((float)delta);

		Velocity = -GlobalTransform.Basis.Z * Speed;
		var collision = MoveAndCollide(Velocity * (float)delta);

		if (collision != null)
		{
			var collider = collision.GetCollider();
			if (collider is Node3D cn && cn == OwnerPlayer) return;
			Explode();
		}
	}

	private void SteerToTarget(float delta)
	{
		var dir = (_target.GlobalPosition - GlobalPosition).Normalized();
		var targetBasis = Basis.LookingAt(dir, Vector3.Up.IsEqualApprox(dir) ? Vector3.Right : Vector3.Up);
		var xf = GlobalTransform;
		xf.Basis = xf.Basis.Slerp(targetBasis, TurnSpeed * delta);
		GlobalTransform = xf;
	}

	private void Explode()
	{
		var pos = GlobalPosition;

		// ── Visual layers ─────────────────────────────────────────────────────
		SpawnExplosionLight(pos);

		// Core fireball — tight, fast, bright
		SpawnLayer(pos,
			tex: "fire_02.png", count: 14, lifetime: 0.45f,
			scaleStart: 0.5f, scaleEnd: 2.2f,
			velMin: 1.5f, velMax: 5.5f, spread: 80f,
			gravity: new Vector3(0, 2f, 0),
			color: new Color(1f, 0.60f, 0.08f), additive: true);

		// Secondary billowing fire — larger, slower
		SpawnLayer(pos,
			tex: "flame_02.png", count: 10, lifetime: 0.70f,
			scaleStart: 1.0f, scaleEnd: 4.5f,
			velMin: 0.4f, velMax: 2.0f, spread: 100f,
			gravity: new Vector3(0, 1.0f, 0),
			color: new Color(1f, 0.38f, 0.04f, 0.85f), additive: true);

		// Rising smoke column — narrow, slow, large
		SpawnLayer(pos + Vector3.Up * 0.2f,
			tex: "smoke_07.png", count: 12, lifetime: 1.6f,
			scaleStart: 0.8f, scaleEnd: 5.5f,
			velMin: 0.2f, velMax: 1.0f, spread: 35f,
			gravity: new Vector3(0, 0.5f, 0),
			color: new Color(0.22f, 0.22f, 0.22f, 0.80f), additive: false);

		// Ground-level smoke puff — wide, very low velocity
		SpawnLayer(pos + Vector3.Down * 0.1f,
			tex: "smoke_03.png", count: 8, lifetime: 1.1f,
			scaleStart: 0.6f, scaleEnd: 3.8f,
			velMin: 0.1f, velMax: 0.6f, spread: 160f,
			gravity: new Vector3(0, 0.3f, 0),
			color: new Color(0.35f, 0.30f, 0.25f, 0.70f), additive: false);

		// Arcing sparks — fast, gravity-pulled, tiny
		SpawnLayer(pos,
			tex: "spark_03.png", count: 45, lifetime: 0.55f,
			scaleStart: 0.15f, scaleEnd: 0.22f,
			velMin: 6f, velMax: 18f, spread: 180f,
			gravity: new Vector3(0, -12f, 0),
			color: new Color(1f, 0.82f, 0.18f), additive: true);

		// Secondary spark burst (slightly different colour and size)
		SpawnLayer(pos,
			tex: "spark_05.png", count: 20, lifetime: 0.40f,
			scaleStart: 0.10f, scaleEnd: 0.18f,
			velMin: 10f, velMax: 25f, spread: 180f,
			gravity: new Vector3(0, -16f, 0),
			color: new Color(1f, 0.5f, 0.1f), additive: true);

		// Shockwave expanding ring
		SpawnLayer(pos,
			tex: "circle_01.png", count: 1, lifetime: 0.20f,
			scaleStart: 0.3f, scaleEnd: 6.0f,
			velMin: 0f, velMax: 0f, spread: 0f,
			gravity: Vector3.Zero,
			color: new Color(1f, 0.75f, 0.3f, 0.45f), additive: true);

		// ── Area damage + knockback ───────────────────────────────────────────
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters3D
		{
			Shape     = new SphereShape3D { Radius = ExplosionRadius },
			Transform = new Transform3D(Basis.Identity, pos),
		};
		var hits = spaceState.IntersectShape(query);

		foreach (var hit in hits)
		{
			var body = (Node)hit["collider"];
			bool isOwner = (body == OwnerPlayer);

			float dist    = pos.DistanceTo(((Node3D)body).GlobalPosition);
			float falloff = Mathf.Clamp(1f - (dist / ExplosionRadius), 0f, 1f);

			// Damage (skip owner)
			if (!isOwner)
			{
				int actualDamage = Mathf.RoundToInt(Damage * falloff);
				if (body is Player p)
					OwnerPlayer?.RequestDamage(p.GetPath(), actualDamage);
				else
					body.GetNodeOrNull<HealthComponent>("HealthComponent")?.TakeDamage(actualDamage);
			}

			// Knockback — ALWAYS applied (including owner for rocket-jump feel)
			Vector3 kDir = ((Node3D)body).GlobalPosition - pos;
			if (kDir.LengthSquared() < 0.01f) kDir = Vector3.Up;
			else kDir = kDir.Normalized();

			// Ensure meaningful upward component — prevents zero-knockback when
			// explosion is directly below the player
			if (kDir.Y < 0.3f) kDir = new Vector3(kDir.X * 0.7f, 0.7f, kDir.Z * 0.7f).Normalized();

			float force = isOwner ? 16f : 11f * falloff;

			if (body is RigidBody3D rb)
				rb.ApplyCentralImpulse(kDir * force);
			else if (body is CharacterBody3D cb)
				cb.Velocity += kDir * force;
		}

		QueueFree();
	}

	// ── Explosion helpers ─────────────────────────────────────────────────────

	private void SpawnExplosionLight(Vector3 pos)
	{
		var light = new OmniLight3D
		{
			LightColor    = new Color(1f, 0.62f, 0.18f),
			LightEnergy   = 8f,
			OmniRange     = ExplosionRadius * 3.5f,
			ShadowEnabled = false,
		};
		GetTree().Root.AddChild(light);
		light.GlobalPosition = pos;

		var tw = light.CreateTween();
		tw.TweenProperty(light, "light_energy", 0f, 0.40f)
		  .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(light)) light.QueueFree(); }));
	}

	/// <summary>Spawn one particle layer of the explosion.</summary>
	private void SpawnLayer(
		Vector3 pos, string tex,
		int count, float lifetime,
		float scaleStart, float scaleEnd,
		float velMin, float velMax, float spread,
		Vector3 gravity, Color color, bool additive)
	{
		var texture = ResourceLoader.Load<Texture2D>(ParticlePath + tex);

		var p = new GpuParticles3D
		{
			Emitting      = true,
			OneShot       = true,
			Amount        = count,
			Lifetime      = lifetime,
			Explosiveness = 0.94f,
			CastShadow    = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		var pm = new ParticleProcessMaterial
		{
			Direction          = Vector3.Up,
			Spread             = spread,
			InitialVelocityMin = velMin,
			InitialVelocityMax = velMax,
			Gravity            = gravity,
			Color              = color,
			AngleMin           = -180f,
			AngleMax           =  180f, // random rotation per sprite
		};

		// Scale: grows from scaleStart → scaleEnd then fades to 0
		float startRatio = scaleStart / Mathf.Max(scaleEnd, 0.001f);
		var sc = new Curve();
		sc.AddPoint(new Vector2(0f, startRatio));
		sc.AddPoint(new Vector2(0.35f, 1.0f));
		sc.AddPoint(new Vector2(1.0f, 0.0f));
		pm.ScaleCurve = new CurveTexture { Curve = sc };
		pm.ScaleMin   = scaleEnd;
		pm.ScaleMax   = scaleEnd * 1.15f; // slight variation

		// Alpha: in → sustain → out
		var ac = new Curve();
		ac.AddPoint(new Vector2(0f,   1.0f));
		ac.AddPoint(new Vector2(0.65f, 0.9f));
		ac.AddPoint(new Vector2(1.0f,  0.0f));
		pm.AlphaCurve = new CurveTexture { Curve = ac };

		p.ProcessMaterial = pm;

		var quad = new QuadMesh { Size = new Vector2(1f, 1f) };
		var qm   = new StandardMaterial3D
		{
			Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode              = additive
				? BaseMaterial3D.BlendModeEnum.Add
				: BaseMaterial3D.BlendModeEnum.Mix,
			ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode          = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true,
			CullMode               = BaseMaterial3D.CullModeEnum.Disabled,
		};
		if (texture != null) qm.AlbedoTexture = texture;
		quad.Material = qm;
		p.DrawPass1 = quad;

		GetTree().Root.AddChild(p);
		p.GlobalPosition = pos;
		GetTree().CreateTimer(lifetime + 0.3f).Timeout +=
			() => { if (IsInstanceValid(p)) p.QueueFree(); };
	}

	/// <summary>Continuous exhaust trail on the rocket.</summary>
	private void SpawnTrailParticles()
	{
		var tex = ResourceLoader.Load<Texture2D>(ParticlePath + "flame_01.png");

		var trail = new GpuParticles3D
		{
			Name          = "Trail",
			Emitting      = true,
			OneShot       = false,
			Amount        = 20,
			Lifetime      = 0.30f,
			Explosiveness = 0f,
			CastShadow    = GeometryInstance3D.ShadowCastingSetting.Off,
		};

		var pm = new ParticleProcessMaterial
		{
			Direction          = Vector3.Back, // +Z = behind the rocket
			Spread             = 18f,
			InitialVelocityMin = 1f,
			InitialVelocityMax = 3f,
			Gravity            = Vector3.Zero,
			Color              = new Color(1f, 0.55f, 0.1f, 0.9f),
		};

		var sc = new Curve();
		sc.AddPoint(new Vector2(0f, 1f));
		sc.AddPoint(new Vector2(1f, 0f));
		pm.ScaleCurve = new CurveTexture { Curve = sc };
		pm.ScaleMin = 0.45f; pm.ScaleMax = 0.65f;

		var ac = new Curve();
		ac.AddPoint(new Vector2(0f, 1f));
		ac.AddPoint(new Vector2(1f, 0f));
		pm.AlphaCurve = new CurveTexture { Curve = ac };

		trail.ProcessMaterial = pm;

		var quad = new QuadMesh { Size = new Vector2(1f, 1f) };
		var qm   = new StandardMaterial3D
		{
			Transparency           = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode              = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode            = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode          = BaseMaterial3D.BillboardModeEnum.Particles,
			VertexColorUseAsAlbedo = true,
		};
		if (tex != null) qm.AlbedoTexture = tex;
		quad.Material = qm;
		trail.DrawPass1 = quad;

		AddChild(trail);
	}
}
