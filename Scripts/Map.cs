using Godot;
using System;

public partial class Map : Node3D
{
    private Node _playersContainer;
    private PackedScene _playerScene;

    public override void _Ready()
    {
        _playersContainer = GetNode<Node>("Players");
        _playerScene = GD.Load<PackedScene>("res://Scenes/Player.tscn");

        if (Multiplayer.IsServer())
        {
            SpawnPlayer(Multiplayer.GetUniqueId());

            foreach (var peer in Multiplayer.GetPeers())
            {
                SpawnPlayer(peer);
            }

            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
        }
    }

    private void SpawnPlayer(long id)
    {
        var player = _playerScene.Instantiate<CharacterBody3D>();
        player.Name = id.ToString();
        // Spawning position can be randomized or set to a specific spawn point.
        player.Position = new Vector3(GD.Randf() * 4 - 2, 2, GD.Randf() * 4 - 2);
        _playersContainer.AddChild(player, true); // true for readable names, but mainly for consistent path structure
    }

    private void OnPeerConnected(long id)
    {
        SpawnPlayer(id);
    }

    private void OnPeerDisconnected(long id)
    {
        if (_playersContainer.HasNode(id.ToString()))
        {
            _playersContainer.GetNode(id.ToString()).QueueFree();
        }
    }
}
