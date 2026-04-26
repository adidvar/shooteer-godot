using Godot;

/// <summary>
/// AmmoPickup — rotating coloured box that refills ammo for a specific weapon slot.
/// After pickup it hides, shows a respawn ring loader, and re-spawns after
/// <see cref="RespawnTime"/> seconds.
///
/// Set <see cref="WeaponIndex"/> to match the weapon slot you want to refill
/// (must equal WeaponStats.WeaponIndex of the target weapon).
/// </summary>
public partial class AmmoPickup : Area3D
{
	[Export] public int   WeaponIndex  = 0;     // matches WeaponStats.WeaponIndex
	[Export] public int   AmmoAmount   = 15;
	[Export] public float RespawnTime  = 10f;
	[Export] public float RotateSpeed  = 2.0f;
	[Export] public float BobAmplitude = 0.12f;
	[Export] public float BobFrequency = 1.2f;

	// Tint colours per weapon index (0=rifle/cyan, 1=sniper/red, 2=rocket/orange, 3=lightning/yellow)
	private static readonly Color[] WeaponColors =
	{
		new Color(0f,   0.78f, 1f,   1f),  // 0 — cyan
		new Color(1f,   0.2f,  0.2f, 1f),  // 1 — red
		new Color(1f,   0.5f,  0f,   1f),  // 2 — orange
		new Color(0.9f, 0.9f,  0f,   1f),  // 3 — yellow
	};

	private MeshInstance3D _mesh;
	private Node3D         _respawnRoot;
	private ShaderMaterial _ringMaterial;
	private Label3D        _countdownLabel;

	private bool  _available    = true;
	private float _respawnTimer = 0f;
	private float _bobTime      = 0f;
	private float _baseY;

	public override void _Ready()
	{
		_mesh           = GetNodeOrNull<MeshInstance3D>("Mesh");
		_respawnRoot    = GetNodeOrNull<Node3D>("RespawnIndicator");
		_countdownLabel = _respawnRoot?.GetNodeOrNull<Label3D>("CountdownLabel");

		var ringMesh  = _respawnRoot?.GetNodeOrNull<MeshInstance3D>("RingMesh");
		_ringMaterial = ringMesh?.GetActiveMaterial(0) as ShaderMaterial;

		// Apply weapon-specific tint to the box mesh.
		ApplyWeaponTint();

		// Update ring fill colour to match weapon.
		Color fillColor = WeaponColors[Mathf.Clamp(WeaponIndex, 0, WeaponColors.Length - 1)];
		_ringMaterial?.SetShaderParameter("fill_color", fillColor);

		if (_respawnRoot != null)
			_respawnRoot.Visible = false;

		_baseY = Position.Y;
		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
		if (_available)
		{
			_bobTime += (float)delta * BobFrequency;
			Position = new Vector3(Position.X, _baseY + Mathf.Sin(_bobTime) * BobAmplitude, Position.Z);
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

		// Refill the currently active weapon, regardless of type.
		var controller = player.GetNodeOrNull<WeaponController>("WeaponController");
		if (controller == null) return;

		WeaponBase activeWeapon = controller.ActiveWeapon;
		if (activeWeapon == null) return;

		if (activeWeapon.RefillAmmo(AmmoAmount))
			StartRespawn();
	}

	private void ApplyWeaponTint()
	{
		if (_mesh == null) return;
		var mat = _mesh.GetActiveMaterial(0);
		if (mat is StandardMaterial3D std)
		{
			Color c = WeaponColors[Mathf.Clamp(WeaponIndex, 0, WeaponColors.Length - 1)];
			std.AlbedoColor         = c;
			std.EmissionEnabled     = true;
			std.Emission            = c * 0.4f;
			std.EmissionEnergyMultiplier = 1.5f;
		}
	}

	private void StartRespawn()
	{
		_available    = false;
		_respawnTimer = RespawnTime;
		if (_mesh        != null) _mesh.Visible       = false;
		if (_respawnRoot != null) _respawnRoot.Visible = false;  // fully hidden during countdown
		_ringMaterial?.SetShaderParameter("progress", 0f);
	}

	private void Respawn()
	{
		_available = true;
		_bobTime   = 0f;
		if (_mesh        != null) _mesh.Visible       = true;
		if (_respawnRoot != null) _respawnRoot.Visible = false;
	}
}
