using Godot;
using System;

public partial class MainMenu : Control
{
    private LineEdit _ipLineEdit;
    private Button _hostButton;
    private Button _joinButton;

    public override void _Ready()
    {
        _ipLineEdit = GetNode<LineEdit>("VBoxContainer/IPLineEdit");
        _hostButton = GetNode<Button>("VBoxContainer/HostButton");
        _joinButton = GetNode<Button>("VBoxContainer/JoinButton");
    }

    public void OnHostButtonPressed()
    {
        GD.Print("Host button pressed");
    }

    public void OnJoinButtonPressed()
    {
        string ip = _ipLineEdit.Text;
        if (string.IsNullOrEmpty(ip))
        {
            ip = "127.0.0.1";
        }
        GD.Print($"Join button pressed, IP: {ip}");
    }
}
