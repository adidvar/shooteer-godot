using Godot;

/// <summary>
/// SettingsMenu — overlaid on the escape panel (HUD) or the main menu.
/// Controls mouse sensitivity and master audio toggle.
/// </summary>
public partial class SettingsMenu : Control
{
	private HSlider _sensSlider;
	private Label _sensLabel;
	private CheckButton _soundToggle;
	private AudioStreamPlayer _hoverSound;

	public override void _Ready()
	{
		_sensSlider  = GetNodeOrNull<HSlider>("CenterContainer/VBoxContainer/SensSlider");
		_sensLabel   = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SensLabel");
		_soundToggle = GetNodeOrNull<CheckButton>("CenterContainer/VBoxContainer/SoundToggle");
		_hoverSound  = GetNodeOrNull<AudioStreamPlayer>("HoverSound");

		if (_hoverSound != null)
			_hoverSound.Stream = GD.Load<AudioStream>("res://Sounds/UI/hover-sound.mp3");

		if (GlobalSettings.Instance != null)
		{
			if (_sensSlider != null)
				_sensSlider.Value = GlobalSettings.Instance.MouseSensitivity;
			if (_soundToggle != null)
				_soundToggle.ButtonPressed = GlobalSettings.Instance.IsSoundEnabled;
		}

		UpdateSensLabel();
	}

	// ── Callbacks (wired in SettingsMenu.tscn) ────────────────────────────────

	public void OnSensChanged(float value)
	{
		if (GlobalSettings.Instance != null)
			GlobalSettings.Instance.MouseSensitivity = value;
		UpdateSensLabel();
	}

	public void OnSoundToggled(bool enabled)
	{
		if (GlobalSettings.Instance == null) return;
		GlobalSettings.Instance.IsSoundEnabled = enabled;
		GlobalSettings.Instance.ApplySoundSettings();
	}

	public void OnBackPressed() => Hide();

	public void OnHover()
	{
		if (_hoverSound?.Stream != null)
			_hoverSound.Play();
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private void UpdateSensLabel()
	{
		if (_sensLabel != null && _sensSlider != null)
			_sensLabel.Text = $"Mouse Sensitivity: {_sensSlider.Value:F4}";
	}
}
