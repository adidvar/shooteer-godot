using Godot;
using System;

/// <summary>
/// Attach to the BulletImpact GPUParticles3D.
/// Emits once, then queues itself free when particles finish.
/// </summary>
public partial class BulletImpact : GpuParticles3D
{
	public override void _Ready()
	{
		OneShot = true;
		Emitting = true;

		// Wait for particles to finish, then remove from tree
		Finished += () => QueueFree();
	}
}
