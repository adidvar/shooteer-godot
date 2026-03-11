using Godot;
using System;

public partial class Player : CharacterBody3D
{
	public const int ShootDamage = 25;

	public HealthComponent HealthComp { get; private set; }
	public MovementComponent MovementComp { get; private set; }

	private Camera3D _camera;
	private RayCast3D _gunRay;
	private MeshInstance3D _mesh;
	private CollisionShape3D _collision;

	private bool _mouseCaptured = false;
	private bool _isDead = false;
	private double _respawnTimer = 0.0;
	private const double RespawnDelay = 3.0;

	// Synchronized health property for MultiplayerSynchronizer
	[Export]
	public int Health
	{
		get => HealthComp?.CurrentHealth ?? 100;
		set
		{
			if (HealthComp != null)
				HealthComp.CurrentHealth = value;
		}
	}

	[Signal]
	public delegate void HealthChangedEventHandler(int newHealth);

	public override void _EnterTree()
	{
		SetMultiplayerAuthority(int.Parse(Name));

		// Instantiate components
		HealthComp = new HealthComponent();
		HealthComp.Name = "HealthComponent";
		AddChild(HealthComp);

		MovementComp = new MovementComponent();
		MovementComp.Name = "MovementComponent";
		AddChild(MovementComp);

		// Setup MovementComponent
		MovementComp.Setup(this);

		// Forward health signals
		HealthComp.HealthChanged += (newHealth) => EmitSignal(SignalName.HealthChanged, newHealth);

		SetupInputMap();
	}

	private void SetupInputMap()
	{
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

			var hud = GetNodeOrNull<HUD>("/root/Main/HUD");
			if (hud != null)
			{
				HealthChanged += hud.UpdateHealth;
				hud.SetMaxHealth(HealthComp.MaxHealth);
				hud.UpdateHealth(HealthComp.CurrentHealth);
			}

			_mouseCaptured = true;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority()) return;
		if (_isDead) return;

		bool isCaptured = Input.MouseMode == Input.MouseModeEnum.Captured;
		if (!isCaptured)
		{
			_mouseCaptured = false;
			return;
		}

		if (@event is InputEventMouseMotion mouseMotion)
		{
			if (!_mouseCaptured)
			{
				_mouseCaptured = true;
				return;
			}

			// Using standard sensitivity as a fallback. 
			// In a complete Godot implementation we might query the GlobalSettings singleton here.
			float sensitivity = 0.002f;

			RotateY(-mouseMotion.Relative.X * sensitivity);
			_camera.RotateX(-mouseMotion.Relative.Y * sensitivity);

			Vector3 cameraRot = _camera.Rotation;
			cameraRot.X = Mathf.Clamp(cameraRot.X, -Mathf.Pi / 2.5f, Mathf.Pi / 2.5f);
			_camera.Rotation = cameraRot;
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
			RpcId(1, MethodName.ServerTakeDamage, hitPlayer.GetPath(), ShootDamage);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerTakeDamage(NodePath targetPath, int damage)
	{
		if (!Multiplayer.IsServer()) return;

		var target = GetNodeOrNull<Player>(targetPath);
		if (target == null || target._isDead) return;

		target.HealthComp.TakeDamage(damage);
		GD.Print($"Player {target.Name} took {damage} damage, HP now {target.HealthComp.CurrentHealth}");

		target.Rpc(MethodName.ClientSyncHealth, target.HealthComp.CurrentHealth);

		if (target.HealthComp.CurrentHealth <= 0)
		{
			target.Rpc(MethodName.ClientDie); // Ensure ClientDie is triggered
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientSyncHealth(int newHealth)
	{
		HealthComp.CurrentHealth = newHealth;

		if (IsMultiplayerAuthority())
		{
			EmitSignal(SignalName.HealthChanged, HealthComp.CurrentHealth);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientDie()
	{
		if (_isDead) return;

		_isDead = true;
		_respawnTimer = RespawnDelay;

		_mesh.Hide();

		if (IsMultiplayerAuthority())
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GD.Print("You died! Respawning in 3 seconds...");
		}

		_collision.Disabled = true;
	}

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
			Input.MouseMode = Input.MouseModeEnum.Captured;
			EmitSignal(SignalName.HealthChanged, HealthComp.CurrentHealth);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
		{
			if (IsMultiplayerAuthority())
			{
				_respawnTimer -= delta;
				if (_respawnTimer <= 0.0)
				{
					RpcId(1, MethodName.ServerRespawn);
				}
			}
			return;
		}

		if (!IsMultiplayerAuthority()) return;

		bool inputsActive = Input.MouseMode == Input.MouseModeEnum.Captured;
		Vector2 inputDir = inputsActive ? Input.GetVector("move_left", "move_right", "move_forward", "move_backward") : Vector2.Zero;
		bool isJumpPressed = Input.IsActionPressed("jump");
		bool isJumpJustPressed = Input.IsActionJustPressed("jump");
		bool isJumpJustReleased = Input.IsActionJustReleased("jump");

		MovementComp.ProcessMovement(inputDir, isJumpPressed, isJumpJustPressed, isJumpJustReleased, inputsActive, (float)delta);
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
}
