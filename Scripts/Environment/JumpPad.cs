using Godot;

/// <summary>
/// JumpPad — launches any CharacterBody3D that touches it straight up.
/// The pad flashes and plays particles on activation.
/// A short cooldown prevents repeated triggers in the same frame.
/// </summary>
public partial class JumpPad : Area3D
{
	[Export] public float LaunchForce  = 16f;
	[Export] public float Cooldown     = 0.5f;  // seconds between activations

	private float  _cooldownTimer = 0f;
	private MeshInstance3D  _mesh;
	private GpuParticles3D  _particles;
	private StandardMaterial3D _material;
	// Tween for the flash effect
	private Tween _flashTween;

	public override void _Ready()
	{
		_mesh      = GetNodeOrNull<MeshInstance3D>("Mesh");
		_particles = GetNodeOrNull<GpuParticles3D>("Particles");

		if (_mesh != null && _mesh.GetActiveMaterial(0) is StandardMaterial3D mat)
		{
			_material = (StandardMaterial3D)mat.Duplicate();
			_mesh.MaterialOverride = _material;
		}

		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
		if (_cooldownTimer > 0f)
			_cooldownTimer -= (float)delta;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_cooldownTimer > 0f) return;
		if (body is not CharacterBody3D cb) return;

		_cooldownTimer = Cooldown;

		// Apply vertical impulse — preserve horizontal momentum.
		cb.Velocity = new Vector3(cb.Velocity.X, LaunchForce, cb.Velocity.Z);

		// Visual flash
		if (_material != null)
		{
			_flashTween?.Kill();
			_flashTween = CreateTween().SetTrans(Tween.TransitionType.Expo);
			_material.EmissionEnergyMultiplier = 4f;
			_flashTween.TweenProperty(_material, "emission_energy_multiplier", 1f, 0.4f);
		}

		// Burst particles
		if (_particles != null)
		{
			_particles.Restart();
			_particles.Emitting = true;
		}
	}
}
