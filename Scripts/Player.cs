using Godot;
using System;

public partial class Player : CharacterBody3D
{
	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;
	public const int MaxHealth = 100;
	public const int ShootDamage = 25;

	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	private Camera3D _camera;
	private RayCast3D _gunRay;
	private MeshInstance3D _mesh;
	private CollisionShape3D _collision;
	private const float MouseSensitivity = 0.002f;

	// Synchronized health property
	[Export]
	public int Health { get; set; } = MaxHealth;

	private bool _isDead = false;
	private double _respawnTimer = 0.0;
	private const double RespawnDelay = 3.0;

	[Signal]
	public delegate void HealthChangedEventHandler(int newHealth);

	public override void _EnterTree()
	{
		SetMultiplayerAuthority(int.Parse(Name));

		if (!InputMap.HasAction("move_left"))
		{
			InputMap.AddAction("move_left");
			InputMap.ActionAddEvent("move_left", new InputEventKey { PhysicalKeycode = Key.A });

			InputMap.AddAction("move_right");
			InputMap.ActionAddEvent("move_right", new InputEventKey { PhysicalKeycode = Key.D });

			InputMap.AddAction("move_forward");
			InputMap.ActionAddEvent("move_forward", new InputEventKey { PhysicalKeycode = Key.W });

			InputMap.AddAction("move_backward");
			InputMap.ActionAddEvent("move_backward", new InputEventKey { PhysicalKeycode = Key.S });

			InputMap.AddAction("jump");
			InputMap.ActionAddEvent("jump", new InputEventKey { PhysicalKeycode = Key.Space });

			InputMap.AddAction("shoot");
			InputMap.ActionAddEvent("shoot", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
		}
	}

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
		_gunRay = GetNode<RayCast3D>("Camera3D/GunRayCast3D");
		_mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		_collision = GetNode<CollisionShape3D>("CollisionShape3D");

		if (IsMultiplayerAuthority())
		{
			_camera.Current = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;

			// Connect HUD
			var hud = GetNodeOrNull<HUD>("/root/Main/HUD");
			if (hud != null)
			{
				HealthChanged += hud.UpdateHealth;
				hud.SetMaxHealth(MaxHealth);
				hud.UpdateHealth(Health);
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority()) return;
		if (_isDead) return;

		if (@event is InputEventMouseMotion mouseMotion)
		{
			RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			_camera.RotateX(-mouseMotion.Relative.Y * MouseSensitivity);

			// Clamp camera rotation to prevent flipping
			Vector3 cameraRot = _camera.Rotation;
			cameraRot.X = Mathf.Clamp(cameraRot.X, -Mathf.Pi / 2.5f, Mathf.Pi / 2.5f);
			_camera.Rotation = cameraRot;
		}
		else if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		else if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
		{
			if (Input.MouseMode == Input.MouseModeEnum.Visible)
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
			else
			{
				TryShoot();
			}
		}
	}

	private void TryShoot()
	{
		if (_gunRay == null || !_gunRay.IsColliding()) return;

		var collider = _gunRay.GetCollider();
		if (collider is Player hitPlayer && hitPlayer != this)
		{
			// Ask server to apply damage on the hit player
			RpcId(1, MethodName.ServerTakeDamage, hitPlayer.GetPath(), ShootDamage);
		}
	}

	/// <summary>Called on the server only – apply damage and replicate health.</summary>
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerTakeDamage(NodePath targetPath, int damage)
	{
		if (!Multiplayer.IsServer()) return;

		var target = GetNodeOrNull<Player>(targetPath);
		if (target == null || target._isDead) return;

		target.Health = Mathf.Max(target.Health - damage, 0);
		GD.Print($"Player {target.Name} took {damage} damage, HP now {target.Health}");

		// Notify all clients about new health
		target.Rpc(MethodName.ClientSyncHealth, target.Health);

		if (target.Health <= 0)
		{
			target.Rpc(MethodName.ClientDie);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSyncHealth(int newHealth)
	{
		Health = newHealth;

		if (IsMultiplayerAuthority())
		{
			EmitSignal(SignalName.HealthChanged, Health);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientDie()
	{
		_isDead = true;
		_respawnTimer = RespawnDelay;

		// Hide model visually
		_mesh.Hide();

		if (IsMultiplayerAuthority())
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GD.Print("You died! Respawning in 3 seconds...");
		}

		// Disable collision so body doesn't block
		_collision.Disabled = true;
	}

	private void Respawn()
	{
		Health = MaxHealth;
		_isDead = false;

		// Random spawn position (same as initial spawn logic)
		Position = new Vector3(GD.Randf() * 10 - 5, 2, GD.Randf() * 10 - 5);
		Velocity = Vector3.Zero;

		_mesh.Show();
		_collision.Disabled = false;

		if (IsMultiplayerAuthority())
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
			EmitSignal(SignalName.HealthChanged, Health);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Handle respawn countdown (run on all peers so visuals stay correct)
		if (_isDead)
		{
			if (IsMultiplayerAuthority())
			{
				_respawnTimer -= delta;
				if (_respawnTimer <= 0.0)
				{
					// Tell server to respawn us
					RpcId(1, MethodName.ServerRespawn);
				}
			}
			return;
		}

		if (!IsMultiplayerAuthority()) return;

		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;

		if (Input.IsActionJustPressed("jump") && IsOnFloor())
			velocity.Y = JumpVelocity;

		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerRespawn()
	{
		if (!Multiplayer.IsServer()) return;

		// Broadcast respawn to all clients
		Rpc(MethodName.ClientRespawn);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientRespawn()
	{
		Respawn();
		// Sync new health to all clients
		Rpc(MethodName.ClientSyncHealth, Health);
	}
}
