using Godot;
using System;

public partial class Rocket : CharacterBody3D
{
	public float Speed = 15f;
	public int Damage = 50;
	public float TurnSpeed = 2f;
	public float ExplosionRadius = 5f;
	public Player OwnerPlayer;

	private Node3D _target;
	private double _lifetime = 5.0;

	public override void _Ready()
	{
		// Visulizations, particles, etc
		var mesh = new MeshInstance3D();
		mesh.Mesh = new CapsuleMesh { Radius = 0.1f, Height = 0.4f };
		mesh.RotationDegrees = new Vector3(90, 0, 0); // Point forward
		AddChild(mesh);

		var shape = new CollisionShape3D();
		shape.Shape = new SphereShape3D { Radius = 0.15f };
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
		FindTarget();

		_lifetime -= delta;
		if (_lifetime <= 0)
		{
			Explode();
			return;
		}

		if (_target != null && IsInstanceValid(_target))
		{
			Vector3 directionToTarget = (_target.GlobalPosition - GlobalPosition).Normalized();
			var targetBasis = Basis.LookingAt(directionToTarget, Vector3.Up);
			Transform3D xform = GlobalTransform;
			xform.Basis = xform.Basis.Slerp(targetBasis, TurnSpeed * (float)delta);
			GlobalTransform = xform;
		}

		Velocity = -GlobalTransform.Basis.Z * Speed;
		var collision = MoveAndCollide(Velocity * (float)delta);
		
		if (collision != null)
		{
			Explode();
		}
	}

	private void Explode()
	{
		// Spawn explosion effect
		var explosionParticles = CreateExplosion();
		GetTree().Root.AddChild(explosionParticles);
		explosionParticles.GlobalPosition = GlobalPosition;

		// Area damage and knockback
		var spaceState = GetWorld3D().DirectSpaceState;
		var shape = new SphereShape3D { Radius = ExplosionRadius };
		var query = new PhysicsShapeQueryParameters3D();
		query.Shape = shape;
		query.Transform = new Transform3D(Basis.Identity, GlobalPosition);
		
		var hits = spaceState.IntersectShape(query);

		foreach (var hit in hits)
		{
			Node body = (Node)hit["collider"];
			if (body == OwnerPlayer) continue; // No self-damage

			float dist = GlobalPosition.DistanceTo(((Node3D)body).GlobalPosition);
			float falloff = Mathf.Clamp(1.0f - (dist / ExplosionRadius), 0f, 1f);
			int actualDamage = Mathf.RoundToInt(Damage * falloff);

			bool applyKnockback = false;

			if (body is Player p)
			{
				OwnerPlayer?.RequestDamage(p.GetPath(), actualDamage);
				applyKnockback = true;
			}
			else
			{
				var hp = body.GetNodeOrNull<HealthComponent>("HealthComponent");
				if (hp != null)
				{
					hp.TakeDamage(actualDamage);
					applyKnockback = true;
				}
			}

			if (applyKnockback && body is RigidBody3D rb)
			{
				Vector3 knockbackDir = (((Node3D)body).GlobalPosition - GlobalPosition).Normalized();
				rb.ApplyCentralImpulse(knockbackDir * 10f * falloff);
			}
			else if (applyKnockback && body is CharacterBody3D cb)
			{
				Vector3 knockbackDir = (((Node3D)body).GlobalPosition - GlobalPosition).Normalized();
				cb.Velocity += knockbackDir * 10f * falloff;
			}
		}

		QueueFree();
	}

	private GpuParticles3D CreateExplosion()
	{
		var p = new GpuParticles3D();
		p.Emitting = true;
		p.OneShot = true;
		p.Amount = 30;
		p.Lifetime = 0.6f;
		p.Explosiveness = 1.0f;
		p.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		var mat = new ParticleProcessMaterial();
		mat.Direction = new Vector3(0, 1, 0);
		mat.Spread = 180f;
		mat.InitialVelocityMin = 2f;
		mat.InitialVelocityMax = 6f;
		mat.Gravity = new Vector3(0, 2f, 0); // smoke drifts up
		mat.Color = new Color(1f, 0.5f, 0.1f);
		mat.CollisionMode = ParticleProcessMaterial.CollisionModeEnum.Rigid;
		mat.CollisionFriction = 0.5f;
		mat.CollisionBounce = 0.5f;

		var scaleCurve = new Curve();
		scaleCurve.AddPoint(new Vector2(0f, 1f));
		scaleCurve.AddPoint(new Vector2(1f, 0f));
		mat.ScaleCurve = new CurveTexture { Curve = scaleCurve };

		var alphaCurve = new Curve();
		alphaCurve.AddPoint(new Vector2(0f, 1f));
		alphaCurve.AddPoint(new Vector2(1f, 0f));
		mat.AlphaCurve = new CurveTexture { Curve = alphaCurve };

		p.ProcessMaterial = mat;

		var pass = new BoxMesh();
		pass.Size = new Vector3(0.04f, 0.04f, 0.04f); // Tiny squares (sparks)
		var pMat = new StandardMaterial3D 
		{ 
			AlbedoColor = new Color(1f, 0.8f, 0.2f), // Yellow-orange sparks
			EmissionEnabled = true, 
			Emission = new Color(1f, 0.5f, 0f), 
			EmissionEnergyMultiplier = 4f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		pass.Material = pMat;
		p.DrawPass1 = pass;

		GetTree().CreateTimer(p.Lifetime).Timeout += () => { if (IsInstanceValid(p)) p.QueueFree(); };
		return p;
	}
}
