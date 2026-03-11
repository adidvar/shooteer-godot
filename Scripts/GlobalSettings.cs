using Godot;
using System;

public partial class GlobalSettings : Node
{
	public static GlobalSettings Instance { get; private set; }

	public float MouseSensitivity { get; set; } = 0.002f;

	public override void _Ready()
	{
		Instance = this;
	}
}
