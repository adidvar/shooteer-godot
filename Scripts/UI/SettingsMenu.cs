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

	// Save-system selector (added programmatically so no .tscn change is needed).
	private OptionButton _saveSystemOption;

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
		BuildSaveSystemSelector();
	}

	// ── Save-system selector ──────────────────────────────────────────────────

	private void BuildSaveSystemSelector()
	{
		var vbox = GetNodeOrNull<VBoxContainer>("CenterContainer/VBoxContainer");
		if (vbox == null) return;

		// Label
		var label = new Label { Text = "Save System:" };
		vbox.AddChild(label);

		// OptionButton
		_saveSystemOption = new OptionButton();
		foreach (var sys in SaveManager.AllSystems)
			_saveSystemOption.AddItem($"{sys.SystemName}  —  {sys.SystemDescription}");
		_saveSystemOption.Selected = GlobalSettings.Instance?.SaveSystemIndex ?? 0;
		_saveSystemOption.ItemSelected += OnSaveSystemSelected;
		vbox.AddChild(_saveSystemOption);
	}

	private void OnSaveSystemSelected(long index)
	{
		SaveManager.Instance?.SetSaveSystem((int)index);
	}

	// ── Callbacks (wired in SettingsMenu.tscn) ────────────────────────────────

	public void OnSensChanged(float value)
	{
		if (GlobalSettings.Instance != null)
			GlobalSettings.Instance.MouseSensitivity = value;
		UpdateSensLabel();
		SaveManager.Instance?.SaveSettings();
	}

	public void OnSoundToggled(bool enabled)
	{
		if (GlobalSettings.Instance == null) return;
		GlobalSettings.Instance.IsSoundEnabled = enabled;
		GlobalSettings.Instance.ApplySoundSettings();
		SaveManager.Instance?.SaveSettings();
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
