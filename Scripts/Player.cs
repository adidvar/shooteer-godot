using Godot;

/// <summary>
/// Player — root of the Player scene (CharacterBody3D).
/// Owns: multiplayer authority, mouse-look input, movement orchestration,
/// death/respawn lifecycle, health sync RPCs, and HUD wiring.
///
/// Weapon firing is fully delegated to WeaponController.
/// Movement physics are fully delegated to MovementComponent.
/// Health tracking is fully delegated to HealthComponent.
/// </summary>
public partial class Player : CharacterBody3D
{
	// ── Component references (resolved in _Ready from scene children) ────────
	public HealthComponent HealthComp   { get; private set; }
	public MovementComponent MovementComp { get; private set; }
	public WeaponController WeaponCtrl  { get; private set; }

	private Camera3D _camera;
	private MeshInstance3D _mesh;
	private CollisionShape3D _collision;

	// ── State ─────────────────────────────────────────────────────────────────
	private bool _isDead = false;
	private double _respawnTimer = 0.0;
	private const double RespawnDelay = 3.0;

	/// <summary>
	/// Synchronized by MultiplayerSynchronizer so late-joining clients
	/// receive the correct HP value immediately on spawn.
	/// </summary>
	[Export]
	public int Health
	{
		get => HealthComp?.CurrentHealth ?? 100;
		set { if (HealthComp != null) HealthComp.CurrentHealth = value; }
	}

	[Signal] public delegate void HealthChangedEventHandler(int newHealth);

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _EnterTree()
	{
		// Node name is set to the peer ID by MultiplayerSpawner.
		SetMultiplayerAuthority(int.Parse(Name));
	}

	public override void _Ready()
	{
		// Resolve scene-provided components (defined in Player.tscn).
		HealthComp   = GetNode<HealthComponent>("HealthComponent");
		MovementComp = GetNode<MovementComponent>("MovementComponent");
		WeaponCtrl   = GetNodeOrNull<WeaponController>("WeaponController");

		_camera    = GetNode<Camera3D>("Camera3D");
		_mesh      = GetNode<MeshInstance3D>("MeshInstance3D");
		_collision = GetNode<CollisionShape3D>("CollisionShape3D");

		// Relay component health changes as a Player-level signal.
		HealthComp.HealthChanged += (hp) => EmitSignal(SignalName.HealthChanged, hp);

		// Authority-only setup: camera, mouse capture, HUD wiring.
		if (!IsMultiplayerAuthority()) return;

		_camera.Current = true;
		CaptureMouse();

		var hud = GetNodeOrNull<HUD>("/root/Main/HUD");
		if (hud == null) return;

		HealthChanged += hud.UpdateHealth;
		hud.SetMaxHealth(HealthComp.MaxHealth);
		hud.UpdateHealth(HealthComp.CurrentHealth);

		if (WeaponCtrl != null)
		{
			WeaponCtrl.WeaponSwitched += hud.OnWeaponSwitched;
			hud.OnWeaponSwitched(WeaponCtrl.CurrentWeaponIndex, WeaponCtrl.CurrentWeaponName);
			// Deferred so WeaponController has had time to instantiate weapon nodes.
			Callable.From(() => ConnectWeaponHitMarkers(hud)).CallDeferred();
		}
	}

	/// <summary>
	/// Subscribe every loaded weapon's HitMarker signal to the HUD.
	/// Called deferred so weapon nodes are guaranteed to be in the tree.
	/// </summary>
	private void ConnectWeaponHitMarkers(HUD hud)
	{
		// WeaponController places the "Weapons" holder under Camera3D.
		var holder = GetNodeOrNull<Node3D>("Camera3D/Weapons");
		if (holder == null) return;

		foreach (Node child in holder.GetChildren())
			if (child is WeaponBase weapon)
				weapon.HitMarker += hud.ShowHitMarker;
	}

	// ── Input ─────────────────────────────────────────────────────────────────

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority() || _isDead) return;

		// Re-capture mouse on click when it has been released (e.g. after dying).
		if (@event.IsActionPressed("capture_mouse") && Input.MouseMode == Input.MouseModeEnum.Visible)
		{
			CaptureMouse();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

		// Mouse-look: rotate player yaw, camera pitch.
		if (@event is InputEventMouseMotion mouseMotion)
		{
			float sens = GlobalSettings.Instance?.MouseSensitivity ?? 0.002f;
			RotateY(-mouseMotion.Relative.X * sens);
			_camera.RotateX(-mouseMotion.Relative.Y * sens);

			// Clamp vertical look.
			Vector3 rot = _camera.Rotation;
			rot.X = Mathf.Clamp(rot.X, -Mathf.Pi / 2.5f, Mathf.Pi / 2.5f);
			_camera.Rotation = rot;
		}
		// Note: weapon firing is handled entirely by WeaponController._Process
		// which checks Input.IsActionPressed("shoot") every frame.
	}

	// ── Physics ───────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
		{
			if (IsMultiplayerAuthority())
			{
				_respawnTimer -= delta;
				if (_respawnTimer <= 0.0)
					RpcId(1, MethodName.ServerRespawn);
			}
			return;
		}

		if (!IsMultiplayerAuthority()) return;

		bool active = Input.MouseMode == Input.MouseModeEnum.Captured;
		Vector2 dir = active
			? Input.GetVector("move_left", "move_right", "move_forward", "move_backward")
			: Vector2.Zero;

		MovementComp.ProcessMovement(
			dir,
			Input.IsActionPressed("jump"),
			Input.IsActionJustPressed("jump"),
			Input.IsActionJustReleased("jump"),
			active,
			(float)delta);
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Ask the server to apply damage to a target player.
	/// Called by weapons (via OwnerPlayer.RequestDamage) on the authority client.
	/// </summary>
	public void RequestDamage(NodePath targetPath, int damage)
	{
		RpcId(1, MethodName.ServerTakeDamage, targetPath, damage);
	}

	// ── RPCs ──────────────────────────────────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerTakeDamage(NodePath targetPath, int damage)
	{
		if (!Multiplayer.IsServer()) return;

		var target = GetNodeOrNull<Player>(targetPath);
		if (target == null || target._isDead) return;

		target.HealthComp.TakeDamage(damage);
		GD.Print($"[Server] {target.Name} took {damage} dmg → {target.HealthComp.CurrentHealth} HP");

		target.Rpc(MethodName.ClientSyncHealth, target.HealthComp.CurrentHealth);

		if (target.HealthComp.CurrentHealth <= 0)
			target.Rpc(MethodName.ClientDie);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSyncHealth(int newHealth)
	{
		HealthComp.CurrentHealth = newHealth;
		if (IsMultiplayerAuthority())
			EmitSignal(SignalName.HealthChanged, newHealth);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientDie()
	{
		if (_isDead) return;

		_isDead = true;
		_respawnTimer = RespawnDelay;

		_mesh.Hide();
		_collision.Disabled = true;

		if (IsMultiplayerAuthority())
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GD.Print("You died! Respawning in 3 seconds...");
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerRespawn()
	{
		if (!Multiplayer.IsServer()) return;
		Rpc(MethodName.ClientRespawn);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientRespawn()
	{
		Respawn();
		Rpc(MethodName.ClientSyncHealth, HealthComp.CurrentHealth);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private void Respawn()
	{
		HealthComp.Respawn();
		_isDead = false;
		Position = new Vector3(GD.Randf() * 10 - 5, 2, GD.Randf() * 10 - 5);
		Velocity = Vector3.Zero;
		_mesh.Show();
		_collision.Disabled = false;

		if (IsMultiplayerAuthority())
		{
			CaptureMouse();
			EmitSignal(SignalName.HealthChanged, HealthComp.CurrentHealth);
		}
	}

	private void CaptureMouse() => Input.MouseMode = Input.MouseModeEnum.Captured;
}
