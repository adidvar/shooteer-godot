using Godot;
using System;

public partial class Player : CharacterBody3D
{
	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;

	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	private Camera3D _camera;
	private const float MouseSensitivity = 0.002f;

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
		}
	}

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");

		if (IsMultiplayerAuthority())
		{
			_camera.Current = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsMultiplayerAuthority()) return;

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
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsMultiplayerAuthority()) return;

		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;

		if (Input.IsActionJustPressed("jump") && IsOnFloor())
			velocity.Y = JumpVelocity;

		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		// Update direction based on character's rotation
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
}
