using Godot;
using System;

public partial class SettingsMenu : Control
{
	private HSlider _sensSlider;
	private Label _sensLabel;
	private AudioStreamPlayer _hoverSound;

	public override void _Ready()
	{
		_sensSlider = GetNode<HSlider>("CenterContainer/VBoxContainer/SensSlider");
		_sensLabel = GetNode<Label>("CenterContainer/VBoxContainer/SensLabel");
		
		_hoverSound = GetNode<AudioStreamPlayer>("HoverSound");
		_hoverSound.Stream = GD.Load<AudioStream>("res://Sounds/UI/hover-sound.mp3");

		if (GlobalSettings.Instance != null)
		{
			_sensSlider.Value = GlobalSettings.Instance.MouseSensitivity;
		}
		UpdateLabel();
	}

	public void OnSensChanged(float value)
	{
		if (GlobalSettings.Instance != null)
		{
			GlobalSettings.Instance.MouseSensitivity = value;
		}
		UpdateLabel();
	}

	private void UpdateLabel()
	{
		_sensLabel.Text = $"Mouse Sensitivity: {_sensSlider.Value:F4}";
	}

	public void OnBackPressed()
	{
		Hide();
	}

	public void OnHover()
	{
		if (_hoverSound != null && _hoverSound.Stream != null)
		{
			_hoverSound.Play();
		}
	}
}
