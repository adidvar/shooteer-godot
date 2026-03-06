using Godot;
using System;

public partial class HUD : CanvasLayer
{
	private ProgressBar _healthBar;
	private Label _healthLabel;
	
	private int _maxHealth = 100;
	private int _currentHealth = 100;

	public override void _Ready()
	{
		_healthBar = GetNode<ProgressBar>("Control/HealthBar");
		_healthLabel = GetNode<Label>("Control/HealthBar/HealthLabel");
		UpdateHealthDisplay();
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
}
