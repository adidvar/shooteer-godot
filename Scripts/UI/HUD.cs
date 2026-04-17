using Godot;

/// <summary>
/// HUD — canvas layer displayed during gameplay.
/// Manages: health bar, weapon panel, hit marker, escape/pause menu.
/// </summary>
public partial class HUD : CanvasLayer
{
	// ── Node references ───────────────────────────────────────────────────────
	private ProgressBar _healthBar;
	private Label _healthLabel;
	private Panel _escapePanel;
	private AudioStreamPlayer _hoverSound;

	// Weapon panel (optional — only present if WeaponPanel node exists in scene).
	private Label _weaponNameLabel;
	private HBoxContainer _weaponSlots;

	// Hit marker
	private ColorRect _hitMarker;
	private double _hitMarkerTimer = 0.0;
	private const double HitMarkerDuration = 0.15;

	// Settings overlay (instantiated lazily).
	private PackedScene _settingsScene = GD.Load<PackedScene>("res://Scenes/UI/SettingsMenu.tscn");
	private Control _settingsInstance;

	// ── State ─────────────────────────────────────────────────────────────────
	private int _maxHealth = 100;
	private int _currentHealth = 100;

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		_healthBar  = GetNode<ProgressBar>("Control/HealthBar");
		_healthLabel = GetNode<Label>("Control/HealthBar/HealthLabel");
		_escapePanel = GetNode<Panel>("Control/EscapePanel");
		_hoverSound  = GetNode<AudioStreamPlayer>("Control/HoverSound");

		_weaponNameLabel = GetNodeOrNull<Label>("Control/WeaponPanel/VBox/WeaponName");
		_weaponSlots     = GetNodeOrNull<HBoxContainer>("Control/WeaponPanel/VBox/Slots");
		_hitMarker       = GetNodeOrNull<ColorRect>("Control/HitMarker");

		UpdateHealthDisplay();
	}

	public override void _Process(double delta)
	{
		// Fade out hit marker.
		if (_hitMarkerTimer > 0 && _hitMarker != null)
		{
			_hitMarkerTimer -= delta;
			float alpha = (float)(_hitMarkerTimer / HitMarkerDuration);
			var c = _hitMarker.Color;
			_hitMarker.Color = new Color(c.R, c.G, c.B, Mathf.Max(alpha * 0.8f, 0f));
			if (_hitMarkerTimer <= 0)
				_hitMarker.Visible = false;
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Toggle pause/escape menu via the "pause" action (Escape key by default).
		if (@event.IsActionPressed("pause") && !@event.IsEcho())
		{
			ToggleEscapeMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	// ── Health ────────────────────────────────────────────────────────────────

	public void SetMaxHealth(int maxHealth)
	{
		_maxHealth = maxHealth;
		if (_healthBar != null) _healthBar.MaxValue = _maxHealth;
		UpdateHealthDisplay();
	}

	public void UpdateHealth(int newHealth)
	{
		_currentHealth = Mathf.Clamp(newHealth, 0, _maxHealth);
		UpdateHealthDisplay();
	}

	private void UpdateHealthDisplay()
	{
		if (_healthBar != null)  _healthBar.Value = _currentHealth;
		if (_healthLabel != null) _healthLabel.Text = $"HP: {_currentHealth}/{_maxHealth}";
	}

	// ── Weapon panel ──────────────────────────────────────────────────────────

	/// <summary>Called by WeaponController.WeaponSwitched signal.</summary>
	public void OnWeaponSwitched(int index, string weaponName)
	{
		if (_weaponNameLabel != null)
			_weaponNameLabel.Text = weaponName;

		if (_weaponSlots == null) return;

		for (int i = 0; i < _weaponSlots.GetChildCount(); i++)
		{
			if (_weaponSlots.GetChild(i) is Panel slot)
			{
				slot.SelfModulate = (i == index)
					? new Color(1f, 0.8f, 0.2f)   // active — golden
					: new Color(0.4f, 0.4f, 0.4f); // inactive — grey
			}
		}
	}

	// ── Hit marker ────────────────────────────────────────────────────────────

	/// <summary>Called by WeaponBase.HitMarker signal.</summary>
	public void ShowHitMarker(bool isKill = false)
	{
		if (_hitMarker == null) return;
		_hitMarker.Color = isKill
			? new Color(1f, 0.8f, 0f, 0.8f)    // gold for kill
			: new Color(1f, 0.2f, 0.2f, 0.8f);  // red for hit
		_hitMarker.Visible = true;
		_hitMarkerTimer = HitMarkerDuration;
	}

	// ── Escape menu ───────────────────────────────────────────────────────────

	private void ToggleEscapeMenu()
	{
		if (_escapePanel == null) return;

		bool opening = !_escapePanel.Visible;
		_escapePanel.Visible = opening;
		Input.MouseMode = opening ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		GetTree().Paused = opening;
		// HUD must keep processing while paused so it can unpause.
		ProcessMode = opening ? ProcessModeEnum.Always : ProcessModeEnum.Inherit;
	}

	// ── Button callbacks (wired in HUD.tscn) ─────────────────────────────────

	public void OnHover()
	{
		if (_hoverSound?.Stream != null)
			_hoverSound.Play();
	}

	public void OnReturnPressed()
	{
		if (_escapePanel == null) return;
		_escapePanel.Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		GetTree().Paused = false;
		ProcessMode = ProcessModeEnum.Inherit;
	}

	public void OnSettingsPressed()
	{
		if (_settingsScene == null)
		{
			GD.PushWarning("HUD: SettingsMenu scene not found!");
			return;
		}
		if (_settingsInstance == null)
		{
			_settingsInstance = _settingsScene.Instantiate<Control>();
			_escapePanel?.AddChild(_settingsInstance);
		}
		_settingsInstance.Show();
	}

	public void OnMainMenuPressed()
	{
		OnReturnPressed();
		GetNode<Main>("/root/Main").ReturnToMainMenu();
	}

	public void OnExitPressed()
	{
		GetTree().Paused = false;
		GetTree().Quit();
	}
}
