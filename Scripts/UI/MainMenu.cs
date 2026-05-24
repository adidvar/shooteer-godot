using Godot;

public partial class MainMenu : Control
{
	private LineEdit _ipLineEdit;
	private Button _hostButton;
	private Button _joinButton;
	private AudioStreamPlayer _hoverSound;

	private PackedScene _settingsScene;
	private Control _settingsInstance;

	[Signal] public delegate void HostRequestedEventHandler();
	[Signal] public delegate void JoinRequestedEventHandler(string ip);

	public override void _Ready()
	{
		_settingsScene = GD.Load<PackedScene>("res://Scenes/UI/SettingsMenu.tscn");
		_ipLineEdit = GetNode<LineEdit>("VBoxContainer/IPLineEdit");
		_hostButton = GetNode<Button>("VBoxContainer/HostButton");
		_joinButton = GetNode<Button>("VBoxContainer/JoinButton");
		_hoverSound = GetNode<AudioStreamPlayer>("HoverSound");
	}

	public void OnHostButtonPressed()
	{
		EmitSignal(SignalName.HostRequested);
	}

	public void OnJoinButtonPressed()
	{
		string ip = _ipLineEdit.Text;
		if (string.IsNullOrEmpty(ip))
			ip = "127.0.0.1";
		EmitSignal(SignalName.JoinRequested, ip);
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
			_hoverSound.Play();
	}
}
