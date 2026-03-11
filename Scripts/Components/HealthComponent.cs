using Godot;
using System;

public partial class HealthComponent : Node
{
    [Export] public int MaxHealth { get; set; } = 100;

    private int _currentHealth;

    public int CurrentHealth
    {
        get => _currentHealth;
        set
        {
            if (_currentHealth == value) return;
            
            _currentHealth = Mathf.Clamp(value, 0, MaxHealth);
            EmitSignal(SignalName.HealthChanged, _currentHealth);

            if (_currentHealth == 0)
            {
                EmitSignal(SignalName.Died);
            }
        }
    }

    [Signal]
    public delegate void HealthChangedEventHandler(int newHealth);

    [Signal]
    public delegate void DiedEventHandler();

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
    }

    public void TakeDamage(int damage)
    {
        CurrentHealth -= damage;
    }

    public void Heal(int amount)
    {
        CurrentHealth += amount;
    }

    public void Respawn()
    {
        CurrentHealth = MaxHealth;
    }
}
