using Godot;

/// <summary>
/// HealthPickup — rotating green sphere.
/// Heals the first player who enters its Area3D trigger.
/// After pickup it hides, shows a respawn ring, and re-spawns after <see cref="RespawnTime"/> seconds.
/// </summary>
public partial class HealthPickup : Area3D
{
	[Export] public int   HealAmount   = 25;
	[Export] public float RespawnTime  = 15f;
	[Export] public float RotateSpeed  = 1.5f;  // rad/s
	[Export] public float BobAmplitude = 0.15f;
	[Export] public float BobFrequency = 1.0f;

	private MeshInstance3D _mesh;
	private Node3D         _respawnRoot;   // contains ring + label, shown during respawn
	private ShaderMaterial _ringMaterial;
	private Label3D        _countdownLabel;

	private bool   _available    = true;
	private float  _respawnTimer = 0f;
	private float  _bobTime      = 0f;
	private float  _baseY;

	public override void _Ready()
	{
		_mesh        = GetNodeOrNull<MeshInstance3D>("Mesh");
		_respawnRoot = GetNodeOrNull<Node3D>("RespawnIndicator");
		_countdownLabel = _respawnRoot?.GetNodeOrNull<Label3D>("CountdownLabel");

		// Grab shader material from the ring quad.
		var ringMesh = _respawnRoot?.GetNodeOrNull<MeshInstance3D>("RingMesh");
		_ringMaterial = ringMesh?.GetActiveMaterial(0) as ShaderMaterial;

		if (_respawnRoot != null)
			_respawnRoot.Visible = false;

		_baseY = Position.Y;

		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
		if (_available)
		{
			// Bob up and down
			_bobTime += (float)delta * BobFrequency;
			Position = new Vector3(Position.X, _baseY + Mathf.Sin(_bobTime) * BobAmplitude, Position.Z);
			// Spin
			if (_mesh != null)
				_mesh.RotateY((float)delta * RotateSpeed);
		}
		else
		{
			_respawnTimer -= (float)delta;
			float fraction = 1f - Mathf.Clamp(_respawnTimer / RespawnTime, 0f, 1f);
			_ringMaterial?.SetShaderParameter("progress", fraction);

			if (_countdownLabel != null)
				_countdownLabel.Text = Mathf.CeilToInt(Mathf.Max(_respawnTimer, 0f)).ToString();

			if (_respawnTimer <= 0f)
				Respawn();
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (!_available) return;
		if (body is not Player player) return;
		if (!player.IsMultiplayerAuthority()) return;

		bool healed = player.HealthComp.CurrentHealth < player.HealthComp.MaxHealth;
		if (!healed) return; // already full HP — don't consume pickup

		player.HealthComp.Heal(HealAmount);
		StartRespawn();
	}

	private void StartRespawn()
	{
		_available    = false;
		_respawnTimer = RespawnTime;

		if (_mesh        != null) _mesh.Visible        = false;
		if (_respawnRoot != null) _respawnRoot.Visible  = false;  // fully hidden during countdown

		_ringMaterial?.SetShaderParameter("progress", 0f);
	}

	private void Respawn()
	{
		_available = true;
		_bobTime   = 0f;
		if (_mesh        != null) _mesh.Visible        = true;
		if (_respawnRoot != null) _respawnRoot.Visible  = false;
	}
}
