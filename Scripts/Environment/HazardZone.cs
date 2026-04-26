using Godot;
using System.Collections.Generic;

/// <summary>
/// HazardZone — a dangerous area that drains HP from every player inside it
/// every <see cref="TickInterval"/> seconds.
/// The mesh pulses between two colours to signal danger.
/// </summary>
public partial class HazardZone : Area3D
{
	[Export] public int   DamagePerTick  = 5;
	[Export] public float TickInterval   = 0.5f;
	[Export] public Color ColorA         = new Color(0.85f, 0.05f, 0.05f, 0.6f);
	[Export] public Color ColorB         = new Color(0.25f, 0.0f,  0.0f,  0.4f);
	[Export] public float PulseDuration  = 0.45f;   // half-period of pulse

	private readonly List<Node3D> _inside = new();
	private float  _tickTimer;
	private Tween  _pulseTween;
	private StandardMaterial3D _material;

	public override void _Ready()
	{
		BodyEntered += b => { if (!_inside.Contains(b)) _inside.Add(b); };
		BodyExited  += b => _inside.Remove(b);

		// Grab (or duplicate) the material so we can tween its colour
		// without affecting other zone instances.
		var mi = GetNodeOrNull<MeshInstance3D>("Mesh");
		if (mi != null)
		{
			// Duplicate the material so each instance has its own copy.
			if (mi.GetActiveMaterial(0) is StandardMaterial3D src)
			{
				_material = (StandardMaterial3D)src.Duplicate();
				mi.MaterialOverride = _material;
				_material.AlbedoColor = ColorA;
			}
		}

		StartPulse();
	}

	private void StartPulse()
	{
		_pulseTween?.Kill();
		_pulseTween = CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine);
		_pulseTween.TweenProperty(_material, "albedo_color", ColorB, PulseDuration);
		_pulseTween.TweenProperty(_material, "albedo_color", ColorA, PulseDuration);
	}

	public override void _Process(double delta)
	{
		// Damage is server-authoritative only.
		if (!Multiplayer.IsServer()) return;
		if (_inside.Count == 0) return;

		_tickTimer -= (float)delta;
		if (_tickTimer > 0f) return;

		_tickTimer = TickInterval;

		foreach (var body in _inside)
		{
			if (body == null || !IsInstanceValid(body)) continue;

			if (body is Player player)
			{
				// Player damage goes through the server-authoritative path
				// so death triggers ClientDie RPC and frag penalty.
				player.ServerEnvironmentDamage(DamagePerTick);
			}
			else
			{
				body.GetNodeOrNull<HealthComponent>("HealthComponent")?.TakeDamage(DamagePerTick);
			}
		}
	}
}
