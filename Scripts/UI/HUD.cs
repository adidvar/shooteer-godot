using Godot;
using System;

public partial class HUD : CanvasLayer
{
	private ProgressBar _healthBar;
	private Label _healthLabel;
	
	private int _maxHealth = 100;
	private int _currentHealth = 100;

	private Panel _escapePanel;
	private AudioStreamPlayer _hoverSound;

	private PackedScene _settingsScene = GD.Load<PackedScene>("res://Scenes/UI/SettingsMenu.tscn");
	private Control _settingsInstance;

	public override void _Ready()
	{
		_healthBar = GetNode<ProgressBar>("Control/HealthBar");
		_healthLabel = GetNode<Label>("Control/HealthBar/HealthLabel");
		
		_escapePanel = GetNode<Panel>("Control/EscapePanel");
		_hoverSound = GetNode<AudioStreamPlayer>("Control/HoverSound");

		UpdateHealthDisplay();
	}

	public override void _Input(InputEvent @event)
	{
		if ((@event.IsActionPressed("ui_cancel") && !@event.IsEcho()) || (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape))
		{
			if (_escapePanel != null)
			{
				_escapePanel.Visible = !_escapePanel.Visible;
				
				if (_escapePanel.Visible)
				{
					Input.MouseMode = Input.MouseModeEnum.Visible;
				}
				else
				{
					Input.MouseMode = Input.MouseModeEnum.Captured;
				}
				
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public void SetMaxHealth(int maxHealth)
	{
		_maxHealth = maxHealth;
		_healthBar.MaxValue = _maxHealth;

		UpdateHealthDisplay();
	}

	public void UpdateHealth(int newHealth)
	{
		_currentHealth = Mathf.Clamp(newHealth, 0, _maxHealth);
		UpdateHealthDisplay();
	}

	public void TakeDamage(int damage)
	{
		UpdateHealth(_currentHealth - damage);
	}

	public void Heal(int amount)
	{
		UpdateHealth(_currentHealth + amount);
	}

	private void UpdateHealthDisplay()
	{
		if (_healthBar != null)
		{
			_healthBar.Value = _currentHealth;
		}
		if (_healthLabel != null)
		{
			_healthLabel.Text = $"HP: {_currentHealth}/{_maxHealth}";
		}
	}

	public void OnHover()
	{
		if (_hoverSound != null && _hoverSound.Stream != null)
		{
			_hoverSound.Play();
		}
	}

	public void OnReturnPressed()
	{
		if (_escapePanel != null)
		{
			_escapePanel.Visible = false;
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	public void OnSettingsPressed()
	{
		if (_settingsInstance == null)
		{
			_settingsInstance = _settingsScene.Instantiate<Control>();
			_escapePanel.AddChild(_settingsInstance);
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
		GetTree().Quit();
	}
}
