using Godot;
using System;

public partial class Main : Node
{
	private MainMenu _mainMenu;
	private HUD _hud;
	
	private const int Port = 8910;
	private const int MaxClients = 4;
	private ENetMultiplayerPeer _peer;
	private Node _levelContainer;

	public override void _Ready()
	{
		_mainMenu = GetNode<MainMenu>("MainMenu");
		_hud = GetNode<HUD>("HUD");
		_levelContainer = GetNode<Node>("LevelContainer");

		// Hide HUD initially
		_hud.Hide();

		// Connect MainMenu signals instead of MainMenu reaching up into Main.
		_mainMenu.HostRequested += HostGame;
		_mainMenu.JoinRequested += JoinGame;

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	public void HostGame()
	{
		_peer = new ENetMultiplayerPeer();
		Error error = _peer.CreateServer(Port, MaxClients);
		if (error != Error.Ok)
		{
			GD.PrintErr("Failed to start server: " + error);
			return;
		}

		Multiplayer.MultiplayerPeer = _peer;
		GD.Print("Server started on port " + Port);
		
		StartGame();
	}

	public void JoinGame(string ip)
	{
		_peer = new ENetMultiplayerPeer();
		Error error = _peer.CreateClient(ip, Port);
		if (error != Error.Ok)
		{
			GD.PrintErr("Failed to join server: " + error);
			return;
		}

		Multiplayer.MultiplayerPeer = _peer;
		GD.Print("Connecting to " + ip + "...");
	}

	private void StartGame()
	{
		_mainMenu.Hide();
		_hud.Show();
		LoadMap();
	}

	private void LoadMap()
	{
		if (_levelContainer.GetChildCount() == 0)
		{
			var mapScene = GD.Load<PackedScene>("res://Scenes/Map.tscn");
			var map = mapScene.Instantiate();
			_levelContainer.AddChild(map);
		}
	}

	private void OnPeerConnected(long id)
	{
		GD.Print("Peer connected: " + id);
		// Player spawning is handled by Map.cs on the server side.
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print("Peer disconnected: " + id);
		// Player despawning is handled by Map.cs on the server side.
	}

	private void OnConnectedToServer()
	{
		GD.Print("Connected to server!");
		StartGame();
	}

	private void OnConnectionFailed()
	{
		GD.PrintErr("Connection failed.");
		_mainMenu.Show();
	}

	private void OnServerDisconnected()
	{
		GD.Print("Server disconnected.");
		ReturnToMainMenu();
	}

	public void ReturnToMainMenu()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
		
		_mainMenu.Show();
		_hud.Hide();
		
		foreach (Node child in _levelContainer.GetChildren())
		{
			child.QueueFree();
		}
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}
}
