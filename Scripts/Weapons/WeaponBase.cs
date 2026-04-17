using Godot;
using System;

/// <summary>
/// Abstract base for all weapons. Must be the root node of a weapon scene (Node3D).
/// Requires a child RayCast3D named "AimDetector" and a child Marker3D named "MuzzlePoint".
/// </summary>
public abstract partial class WeaponBase : Node3D
{
	[Export] public WeaponStats Stats { get; set; }
	[Export] public RayCast3D AimDetector { get; set; }
	[Export] public Marker3D MuzzlePoint { get; set; }
	[Export] public AudioStreamPlayer3D ShootSoundPlayer { get; set; }

	[Signal]
	public delegate void HitMarkerEventHandler(bool isKill); // Stub for hit marker UI/Sound

	protected Player OwnerPlayer { get; private set; }

	/// <summary>Called by WeaponController after instantiation.</summary>
	public virtual void Initialize(Player owner)
	{
		OwnerPlayer = owner;
	}

	public override void _Ready()
	{
		AimDetector ??= GetNodeOrNull<RayCast3D>("AimDetector");
		MuzzlePoint ??= GetNodeOrNull<Marker3D>("MuzzlePoint");
		ShootSoundPlayer ??= GetNodeOrNull<AudioStreamPlayer3D>("ShootSound");

		if (Stats != null && ShootSoundPlayer != null && Stats.ShootSound != null)
		{
			ShootSoundPlayer.Stream = Stats.ShootSound;
		}
	}

	/// <summary>Pull the trigger. Implemented by each concrete weapon.</summary>
	public abstract void Fire();

	/// <summary>Helper: returns the world-space point the aim detector is pointing at.</summary>
	protected Vector3 GetAimTarget()
	{
		if (AimDetector == null) return GlobalPosition + GlobalTransform.Basis.Z * -50f;

		if (AimDetector.IsColliding())
			return AimDetector.GetCollisionPoint();

		return AimDetector.GlobalPosition + AimDetector.GlobalTransform.Basis.Z * AimDetector.TargetPosition.Length();
	}

	protected void NotifyHit(bool isKill = false)
	{
		EmitSignal(SignalName.HitMarker, isKill);
	}
}

