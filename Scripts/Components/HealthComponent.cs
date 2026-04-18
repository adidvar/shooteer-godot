using Godot;

public partial class HealthComponent : Node
{
	[Export] public int MaxHealth { get; set; } = 100;

	private int _currentHealth;

	public int CurrentHealth
	{
		get => _currentHealth;
		set
		{
			int clamped = Mathf.Clamp(value, 0, MaxHealth);
			if (_currentHealth == clamped) return;

			_currentHealth = clamped;
			EmitSignal(SignalName.HealthChanged, _currentHealth);

			if (_currentHealth == 0)
				EmitSignal(SignalName.Died);
		}
	}

	[Signal] public delegate void HealthChangedEventHandler(int newHealth);
	[Signal] public delegate void DiedEventHandler();

	public override void _Ready()
	{
		_currentHealth = MaxHealth;
	}

	public void TakeDamage(int damage)
	{
		if (damage <= 0) return;
		CurrentHealth -= damage;
	}

	public void Heal(int amount)
	{
		if (amount <= 0) return;
		CurrentHealth += amount;
	}

	public void Respawn()
	{
		CurrentHealth = MaxHealth;
	}
}
