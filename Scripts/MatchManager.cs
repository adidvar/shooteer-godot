using Godot;
using System.Collections.Generic;

/// <summary>
/// Server-authoritative match manager.
/// Tracks a 15-minute countdown and per-player frag counts.
/// Syncs state to all clients via RPCs.
/// After the timer expires the match resets automatically.
/// </summary>
public partial class MatchManager : Node
{
	public const float MatchDuration  = 900f; // 15 minutes
	public const float RestartDelay   = 6f;   // seconds after match end before restart

	// ── Server state ──────────────────────────────────────────────────────────
	private float _timeRemaining;
	private float _syncAccum;
	private const float SyncInterval = 1f;

	private bool  _matchActive;
	private bool  _endPending;
	private float _restartTimer;

	// peerID → frag count (server keeps the source of truth)
	private readonly Dictionary<int, int> _frags = new();
	private readonly System.Collections.Generic.HashSet<int> _players = new();

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		if (!Multiplayer.IsServer()) return;

		// Track peers joining/leaving so the scoreboard always shows everyone.
		Multiplayer.PeerConnected    += id => OnPeerConnected((int)id);
		Multiplayer.PeerDisconnected += id => OnPeerLeft((int)id);
	}

	private void OnPeerConnected(int peerId)
	{
		_players.Add(peerId);
		_frags.TryAdd(peerId, 0);
		// Tell every client to add this player row (0 frags).
		Rpc(MethodName.ClientPlayerJoined, peerId, 0);
	}

	private void OnPeerLeft(int peerId)
	{
		_players.Remove(peerId);
		_frags.Remove(peerId);
		Rpc(MethodName.ClientPlayerLeft, peerId);
	}

	// ── Server entry points ───────────────────────────────────────────────────

	/// <summary>Called by Main (server only) when gameplay begins.</summary>
	public void StartMatch()
	{
		if (!Multiplayer.IsServer()) return;

		_timeRemaining = MatchDuration;
		_syncAccum     = 0f;
		_frags.Clear();
		_players.Clear();
		_matchActive   = true;
		_endPending    = false;

		// Seed the player set: server itself + all connected peers.
		_players.Add(Multiplayer.GetUniqueId());
		foreach (int id in Multiplayer.GetPeers())
			_players.Add(id);
		foreach (int id in _players)
			_frags[id] = 0;

		Rpc(MethodName.ClientMatchStarted, MatchDuration);

		// Register every current player on all clients.
		foreach (int id in _players)
			Rpc(MethodName.ClientPlayerJoined, id, 0);
	}

	/// <summary>Called by Player.cs server-side on a confirmed kill.</summary>
	public void AddFrag(int killerPeerId)
	{
		if (!Multiplayer.IsServer() || !_matchActive) return;

		_frags.TryGetValue(killerPeerId, out int current);
		_frags[killerPeerId] = current + 1;
		Rpc(MethodName.ClientFragUpdate, killerPeerId, _frags[killerPeerId]);
	}

	/// <summary>Called when a player dies from a neutral/environment cause. Can go negative.</summary>
	public void SubFrag(int peerId)
	{
		if (!Multiplayer.IsServer() || !_matchActive) return;

		_frags.TryGetValue(peerId, out int current);
		_frags[peerId] = current - 1;  // allowed to go below 0
		Rpc(MethodName.ClientFragUpdate, peerId, _frags[peerId]);
	}

	// ── _Process ──────────────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		if (!Multiplayer.IsServer()) return;

		if (_endPending)
		{
			_restartTimer -= (float)delta;
			if (_restartTimer <= 0f)
			{
				_endPending = false;
				StartMatch();
			}
			return;
		}

		if (!_matchActive) return;

		_timeRemaining -= (float)delta;
		_syncAccum     += (float)delta;

		if (_syncAccum >= SyncInterval)
		{
			_syncAccum -= SyncInterval;
			Rpc(MethodName.ClientTimerSync, _timeRemaining);
		}

		if (_timeRemaining <= 0f)
		{
			_matchActive  = false;
			_endPending   = true;
			_restartTimer = RestartDelay;

			int winnerPeerId = 1;
			int maxFrags     = 0;
			foreach (var kv in _frags)
			{
				if (kv.Value > maxFrags)
				{
					maxFrags     = kv.Value;
					winnerPeerId = kv.Key;
				}
			}

			Rpc(MethodName.ClientMatchEnded, winnerPeerId, maxFrags, (int)RestartDelay);
		}
	}

	// ── RPCs ──────────────────────────────────────────────────────────────────

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMatchStarted(float duration)
	{
		GetHud()?.MatchStarted(duration);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ClientTimerSync(float remaining)
	{
		GetHud()?.UpdateMatchTimer(remaining);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientFragUpdate(int peerId, int frags)
	{
		GetHud()?.UpdateScore(peerId, frags);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayerJoined(int peerId, int frags)
	{
		GetHud()?.RegisterPlayer(peerId, frags);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPlayerLeft(int peerId)
	{
		GetHud()?.RemovePlayer(peerId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true,
		 TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMatchEnded(int winnerPeerId, int winnerFrags, int restartInSec)
	{
		GetHud()?.ShowMatchEnd(winnerPeerId, winnerFrags, restartInSec);
	}

	// ── Helper ────────────────────────────────────────────────────────────────

	private HUD GetHud() => GetNodeOrNull<HUD>("/root/Main/HUD");
}
