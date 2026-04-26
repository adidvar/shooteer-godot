using Godot;

/// <summary>
/// DestructibleCrate — a physics crate that reacts to bullet impacts and
/// shatters when its HP reaches zero (spawning a Sparks particle burst).
///
/// The crate relies on the existing <see cref="HealthComponent"/> child node
/// for HP management.  BasicWeapon already calls
/// <c>collider.GetNodeOrNull&lt;HealthComponent&gt;().TakeDamage()</c> —
/// no weapon changes are required.
/// </summary>
public partial class DestructibleCrate : RigidBody3D
{
	[Export] public PackedScene SparksScene { get; set; }

	private HealthComponent _health;

	public override void _Ready()
	{
		_health = GetNode<HealthComponent>("HealthComponent");
		_health.Died         += OnDestroyed;
		_health.HealthChanged += OnDamaged;
	}

	private void OnDamaged(int _)
	{
		// Each bullet hit applies a small random impulse so the crate visibly
		// reacts to gunfire.
		ApplyCentralImpulse(new Vector3(
			GD.Randf() - 0.5f,
			0.25f,
			GD.Randf() - 0.5f) * 3.5f);

		ApplyTorqueImpulse(new Vector3(
			GD.Randf() - 0.5f,
			GD.Randf() - 0.5f,
			GD.Randf() - 0.5f) * 1.5f);
	}

	private void OnDestroyed()
	{
		if (SparksScene != null)
		{
			var sparks = SparksScene.Instantiate<GpuParticles3D>();
			GetTree().Root.AddChild(sparks);
			sparks.GlobalPosition = GlobalPosition;
			sparks.Emitting = true;
			// Auto-free after the burst completes.
			sparks.Finished += sparks.QueueFree;
		}
		QueueFree();
	}
}
