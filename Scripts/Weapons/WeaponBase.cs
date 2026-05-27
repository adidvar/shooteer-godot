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

	[Signal] public delegate void HitMarkerEventHandler(bool isKill);
	[Signal] public delegate void AmmoChangedEventHandler(int current, int max);

	// ── Ammo ─────────────────────────────────────────────────────────────────
	public int CurrentAmmo  { get; private set; }
	public int MaxAmmo      { get; private set; }

	protected Player OwnerPlayer { get; private set; }

	/// <summary>Called by WeaponController after instantiation.</summary>
	public virtual void Initialize(Player owner)
	{
		OwnerPlayer = owner;
		// Initialise ammo after Stats may have been set in _Ready
		InitAmmo();
	}

	private void InitAmmo()
	{
		MaxAmmo     = Stats?.MaxAmmo ?? 30;
		CurrentAmmo = MaxAmmo;
		EmitSignal(SignalName.AmmoChanged, CurrentAmmo, MaxAmmo);
	}

	/// <summary>
	/// Adds ammo up to MaxAmmo. Returns true if ammo was actually added.
	/// </summary>
	public bool RefillAmmo(int amount)
	{
		if (CurrentAmmo >= MaxAmmo) return false;
		CurrentAmmo = Mathf.Min(CurrentAmmo + amount, MaxAmmo);
		EmitSignal(SignalName.AmmoChanged, CurrentAmmo, MaxAmmo);
		return true;
	}

	/// <summary>Directly set ammo (used when restoring a save). Clamped to [0, MaxAmmo].</summary>
	public void SetAmmo(int amount)
	{
		CurrentAmmo = Mathf.Clamp(amount, 0, MaxAmmo);
		EmitSignal(SignalName.AmmoChanged, CurrentAmmo, MaxAmmo);
	}

	/// <summary>
	/// Tries to consume <paramref name="count"/> ammo. Returns false (and fires nothing)
	/// if there is not enough ammo.
	/// </summary>
	protected bool TryConsumeAmmo(int count = 1)
	{
		if (CurrentAmmo < count) return false;
		CurrentAmmo -= count;
		EmitSignal(SignalName.AmmoChanged, CurrentAmmo, MaxAmmo);
		return true;
	}

	public override void _Ready()
	{
		AimDetector ??= GetNodeOrNull<RayCast3D>("AimDetector");
		MuzzlePoint ??= GetNodeOrNull<Marker3D>("MuzzlePoint");
		ShootSoundPlayer ??= GetNodeOrNull<AudioStreamPlayer3D>("ShootSound");

		if (Stats != null && ShootSoundPlayer != null && Stats.ShootSound != null)
			ShootSoundPlayer.Stream = Stats.ShootSound;

		// Initialise ammo from Stats so it's ready even before Initialize() is called.
		InitAmmo();
	}

	/// <summary>Pull the trigger. Implemented by each concrete weapon.</summary>
	public abstract void Fire();

	/// <summary>Helper: returns the world-space point the aim detector is pointing at.</summary>
	protected Vector3 GetAimTarget()
	{
		if (AimDetector == null) return GlobalPosition - GlobalTransform.Basis.Z * 50f;

		if (AimDetector.IsColliding())
			return AimDetector.GetCollisionPoint();

		// Convert the local TargetPosition to world space (handles any aim direction correctly).
		return AimDetector.GlobalTransform * AimDetector.TargetPosition;
	}

	protected void NotifyHit(bool isKill = false)
	{
		EmitSignal(SignalName.HitMarker, isKill);
	}
}

