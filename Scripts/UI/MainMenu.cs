using Godot;
using System;

public partial class MainMenu : Control
{
	private LineEdit _ipLineEdit;
	private Button _hostButton;
	private Button _joinButton;
	private AudioStreamPlayer _hoverSound;

	private PackedScene _settingsScene = GD.Load<PackedScene>("res://Scenes/UI/SettingsMenu.tscn");
	private Control _settingsInstance;

	public override void _Ready()
	{
		_ipLineEdit = GetNode<LineEdit>("VBoxContainer/IPLineEdit");
		_hostButton = GetNode<Button>("VBoxContainer/HostButton");
		_joinButton = GetNode<Button>("VBoxContainer/JoinButton");
		_hoverSound = GetNode<AudioStreamPlayer>("HoverSound");
	}

	public void OnHostButtonPressed()
	{
		GetParent<Main>().HostGame();

	}	

	public void OnJoinButtonPressed()
	{
		string ip = _ipLineEdit.Text;
		if (string.IsNullOrEmpty(ip))
		{
			ip = "127.0.0.1";
		}
		GetParent<Main>().JoinGame(ip);

	}

	public void OnSettingsButtonPressed()
	{
		if (_settingsInstance == null)
		{
			_settingsInstance = _settingsScene.Instantiate<Control>();
			AddChild(_settingsInstance);
		}
		_settingsInstance.Show();
	}

	public void OnHover()
	{
		if (_hoverSound != null && _hoverSound.Stream != null)
		{
			_hoverSound.Play();
		}
	}
}
