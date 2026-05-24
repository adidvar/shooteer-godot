using Godot;
using System;

/// <summary>
/// WeaponController – lives as a child of Player (CharacterBody3D).
/// Scans res://Scenes/Weapons/ for *.tscn files, instantiates them and attaches
/// them to a dynamically created "Weapons" Node3D child.
/// Forwards shoot input to the currently active weapon.
/// </summary>
public partial class WeaponController : Node
{
	[Export]
	public Godot.Collections.Array<PackedScene> WeaponScenes { get; set; } = new();

	[Signal]
	public delegate void WeaponSwitchedEventHandler(int newIndex, string weaponName);

	[Signal]
	public delegate void AmmoChangedEventHandler(int current, int max);

	private Node3D _weaponsHolder;
	private WeaponBase _activeWeapon;
	public WeaponBase ActiveWeapon => _activeWeapon;
	public int CurrentWeaponIndex { get; private set; } = 0;
	public string CurrentWeaponName => _activeWeapon?.Stats?.WeaponName ?? _activeWeapon?.Name.ToString() ?? "None";
	private Player _player;

	public override void _Ready()
	{
		_player = GetParent<Player>();
		if (_player == null)
		{
			GD.PushWarning("WeaponController: parent is not a Player!");
			return;
		}

		// Attach weapons to camera so AimDetector automatically aligns with camera look direction
		var camera = _player.GetNodeOrNull<Camera3D>("Camera3D");
		Node3D weaponParent = camera ?? (Node3D)_player;

		_weaponsHolder = new Node3D { Name = "Weapons" };
		weaponParent.AddChild(_weaponsHolder);

		Callable.From(LoadWeapons).CallDeferred();
	}

	private void LoadWeapons()
	{
		if (WeaponScenes == null || WeaponScenes.Count == 0)
		{
			GD.PushWarning("WeaponController: No weapons assigned in WeaponScenes array!");
			return;
		}

		foreach (var scene in WeaponScenes)
		{
			if (scene == null) continue;

			var weapon = scene.Instantiate<WeaponBase>();
			if (weapon == null)
			{
				GD.PushWarning($"WeaponController: scene root is not a WeaponBase for {scene.ResourcePath}");
				continue;
			}

			_weaponsHolder.AddChild(weapon);
			weapon.Initialize(_player);
			weapon.Visible = false; // Hide by default

			GD.Print($"WeaponController: loaded weapon '{scene.ResourcePath}'");
		}

		if (_weaponsHolder.GetChildCount() > 0)
			SwitchWeapon(0);
	}

	public override void _Process(double delta)
	{
		if (_player == null || !_player.IsMultiplayerAuthority()) return;
		if (_player.IsDead) return;
		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

		if (Input.IsActionPressed("shoot"))
			_activeWeapon?.Fire();
	}

	public override void _Input(InputEvent @event)
	{
		if (_player == null || !_player.IsMultiplayerAuthority()) return;
		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

		// Number keys
		if (@event.IsActionPressed("weapon_1")) { SwitchWeapon(0); GetViewport().SetInputAsHandled(); }
		else if (@event.IsActionPressed("weapon_2")) { SwitchWeapon(1); GetViewport().SetInputAsHandled(); }
		else if (@event.IsActionPressed("weapon_3")) { SwitchWeapon(2); GetViewport().SetInputAsHandled(); }
		else if (@event.IsActionPressed("weapon_4")) { SwitchWeapon(3); GetViewport().SetInputAsHandled(); }
		// Scroll wheel
		else if (@event.IsActionPressed("weapon_next")) { SwitchWeapon(CurrentWeaponIndex + 1); GetViewport().SetInputAsHandled(); }
		else if (@event.IsActionPressed("weapon_prev")) { SwitchWeapon(CurrentWeaponIndex - 1); GetViewport().SetInputAsHandled(); }
	}

	/// <summary>Switch to the weapon at the given index in the Weapons holder.</summary>
	public void SwitchWeapon(int index)
	{
		if (_weaponsHolder == null) return;
		var children = _weaponsHolder.GetChildren();
		if (children.Count == 0) return;

		// Wrap around
		if (index < 0) index = children.Count - 1;
		if (index >= children.Count) index = 0;

		if (_activeWeapon != null)
			_activeWeapon.Visible = false;

		CurrentWeaponIndex = index;
		_activeWeapon = children[index] as WeaponBase;
		
		if (_activeWeapon != null)
		{
			_activeWeapon.Visible = true;
			string wName = _activeWeapon.Stats != null ? _activeWeapon.Stats.WeaponName : _activeWeapon.Name.ToString();
			EmitSignal(SignalName.WeaponSwitched, index, wName);
			// Forward the new weapon's current ammo to HUD immediately
			EmitSignal(SignalName.AmmoChanged, _activeWeapon.CurrentAmmo, _activeWeapon.MaxAmmo);
		}
	}
}
