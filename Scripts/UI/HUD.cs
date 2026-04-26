using Godot;

/// <summary>
/// HUD — canvas layer displayed during gameplay.
/// Manages: health bar (with ghost/drain effect), weapon panel, ammo counter,
///          hit marker, and escape/pause menu.
/// </summary>
public partial class HUD : CanvasLayer
{
	// ── Node references ───────────────────────────────────────────────────────
	private ProgressBar _healthBar;
	private Label       _healthLabel;
	private Panel       _escapePanel;
	private AudioStreamPlayer _hoverSound;

	// Ghost HP bar — created programmatically, positioned behind _healthBar.
	private ProgressBar _ghostBar;
	private Tween       _ghostTween;

	// Gradient HP-bar — fill colour updated dynamically via StyleBoxFlat.
	private StyleBoxFlat   _hpFillStyle;
	private ShaderMaterial _hpBarShader; // kept for possible future use (unused at runtime)

	// Damage vignette — full-screen ColorRect with shader, fades in on damage.
	private ColorRect      _vignetteRect;
	private ShaderMaterial _vignetteShader;
	private float          _vignetteIntensity = 0f;
	private float          _vignetteFade      = 0f;  // fade-out timer
	private const float    VignetteDuration   = 0.55f;

	// Weapon panel (optional — only present if WeaponPanel node exists in scene).
	private Label          _weaponNameLabel;
	private HBoxContainer  _weaponSlots;

	// Ammo counter — created programmatically below the weapon panel.
	private Label _ammoLabel;

	// Hit marker
	private ColorRect _hitMarker;
	private double    _hitMarkerTimer  = 0.0;
	private const double HitMarkerDuration = 0.15;

	// Settings overlay (instantiated lazily).
	private PackedScene _settingsScene  = GD.Load<PackedScene>("res://Scenes/UI/SettingsMenu.tscn");
	private Control     _settingsInstance;

	// ── State ─────────────────────────────────────────────────────────────────
	private int _maxHealth     = 100;
	private int _currentHealth = 100;

	// ── Match UI (built programmatically) ─────────────────────────────────────
	private Label  _timerLabel;
	private VBoxContainer _scorePanel;
	private Panel  _matchEndPanel;
	private Label  _matchEndLabel;
	private Label  _restartLabel;
	// peerID → score label
	private readonly System.Collections.Generic.Dictionary<int, Label> _scoreLabels = new();

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		_healthBar   = GetNode<ProgressBar>("Control/HealthBar");
		_healthLabel = GetNode<Label>("Control/HealthBar/HealthLabel");
		_escapePanel = GetNode<Panel>("Control/EscapePanel");
		_hoverSound  = GetNode<AudioStreamPlayer>("Control/HoverSound");

		_weaponNameLabel = GetNodeOrNull<Label>("Control/WeaponPanel/VBox/WeaponName");
		_weaponSlots     = GetNodeOrNull<HBoxContainer>("Control/WeaponPanel/VBox/Slots");
		_hitMarker       = GetNodeOrNull<ColorRect>("Control/HitMarker");

		BuildGhostBar();
		BuildAmmoLabel();
		BuildDamageVignette();
		BuildMatchUI();
		ApplyGradientHealthBar();
		ApplyWeaponPanelStyle();
		UpdateHealthDisplay();
	}

	// ── Ghost HP bar (created in code, no .tscn edit needed) ─────────────────

	private void BuildGhostBar()
	{
		if (_healthBar == null) return;

		// Insert ghost bar as a sibling directly behind the main bar.
		_ghostBar                    = new ProgressBar();
		_ghostBar.Name               = "GhostHealthBar";
		_ghostBar.ShowPercentage     = false;
		_ghostBar.MaxValue           = _maxHealth;
		_ghostBar.Value              = _maxHealth;
		_ghostBar.MouseFilter        = Control.MouseFilterEnum.Ignore;

		// Inherit the same anchors/offsets so it overlaps perfectly.
		_ghostBar.AnchorLeft   = _healthBar.AnchorLeft;
		_ghostBar.AnchorTop    = _healthBar.AnchorTop;
		_ghostBar.AnchorRight  = _healthBar.AnchorRight;
		_ghostBar.AnchorBottom = _healthBar.AnchorBottom;
		_ghostBar.OffsetLeft   = _healthBar.OffsetLeft;
		_ghostBar.OffsetTop    = _healthBar.OffsetTop;
		_ghostBar.OffsetRight  = _healthBar.OffsetRight;
		_ghostBar.OffsetBottom = _healthBar.OffsetBottom;

		// White tint so the "drain" segment stands out clearly.
		_ghostBar.Modulate = new Color(1f, 1f, 1f, 0.85f);

		// Add before the main bar so main bar paints on top.
		_healthBar.GetParent().AddChild(_ghostBar);
		_healthBar.GetParent().MoveChild(_ghostBar, _healthBar.GetIndex());
	}

	// ── Gradient health-bar shader ────────────────────────────────────────────

	private void ApplyGradientHealthBar()
	{
		if (_healthBar == null) return;

		// Background style: dark with a subtle cyan border
		var bgStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.05f, 0.06f, 0.10f, 0.92f),
			BorderColor = new Color(0.0f, 0.65f, 0.90f, 0.70f),
		};
		bgStyle.SetBorderWidthAll(2);
		bgStyle.CornerRadiusTopLeft = bgStyle.CornerRadiusTopRight =
		bgStyle.CornerRadiusBottomLeft = bgStyle.CornerRadiusBottomRight = 4;
		_healthBar.AddThemeStyleboxOverride("background", bgStyle);

		// Fill style: starts at full-health colour, updated in UpdateHealthDisplay()
		_hpFillStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.85f, 0.40f),
		};
		_hpFillStyle.CornerRadiusTopLeft = _hpFillStyle.CornerRadiusTopRight =
		_hpFillStyle.CornerRadiusBottomLeft = _hpFillStyle.CornerRadiusBottomRight = 3;
		_healthBar.AddThemeStyleboxOverride("fill", _hpFillStyle);
	}

	// ── Gradient border / style on WeaponPanel ────────────────────────────────

	private void ApplyWeaponPanelStyle()
	{
		var panel = GetNodeOrNull<PanelContainer>("Control/WeaponPanel");
		if (panel == null) return;

		// Dark translucent background with a cyan gradient border.
		var sb = new StyleBoxFlat();
		sb.BgColor            = new Color(0.03f, 0.06f, 0.10f, 0.82f);
		sb.BorderColor        = new Color(0.0f, 0.78f, 1.0f, 0.85f);
		sb.SetBorderWidthAll(2);
		sb.CornerRadiusTopLeft    = 6;
		sb.CornerRadiusTopRight   = 6;
		sb.CornerRadiusBottomLeft = 6;
		sb.CornerRadiusBottomRight= 6;
		sb.ContentMarginLeft   = 10f;
		sb.ContentMarginRight  = 10f;
		sb.ContentMarginTop    = 6f;
		sb.ContentMarginBottom = 6f;
		panel.AddThemeStyleboxOverride("panel", sb);

		// Also style the HP bar container for consistency.
		var hpPanel = _healthBar?.GetParent() as Control;
		if (hpPanel != null)
		{
			// Apply the same border look to the bar background via StyleBoxFlat on ProgressBar itself.
			var barBg = new StyleBoxFlat();
			barBg.BgColor     = new Color(0.05f, 0.05f, 0.08f, 0.90f);
			barBg.BorderColor = new Color(0.0f, 0.65f, 0.90f, 0.70f);
			barBg.SetBorderWidthAll(2);
			barBg.CornerRadiusTopLeft = barBg.CornerRadiusTopRight =
			barBg.CornerRadiusBottomLeft = barBg.CornerRadiusBottomRight = 4;
			_healthBar.AddThemeStyleboxOverride("background", barBg);
		}
	}

	// ── Damage vignette ───────────────────────────────────────────────────────

	private void BuildDamageVignette()
	{
		var control = GetNodeOrNull<Control>("Control");
		if (control == null) return;

		var shader = ResourceLoader.Load<Shader>("res://Scenes/UI/DamageVignette.gdshader");
		if (shader == null) return;

		_vignetteShader = new ShaderMaterial { Shader = shader };
		_vignetteShader.SetShaderParameter("intensity", 0f);

		_vignetteRect = new ColorRect
		{
			Name        = "DamageVignette",
			AnchorLeft  = 0f, AnchorTop    = 0f,
			AnchorRight = 1f, AnchorBottom = 1f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Color       = new Color(1f, 1f, 1f, 1f), // colour handled by shader
		};
		_vignetteRect.Material = _vignetteShader;

		// Must be on top of everything — add as last child of Control.
		control.AddChild(_vignetteRect);
		control.MoveChild(_vignetteRect, control.GetChildCount() - 1);
	}

	private void FlashVignette()
	{
		if (_vignetteShader == null) return;
		_vignetteFade      = VignetteDuration;
		_vignetteIntensity = 1f;
		_vignetteShader.SetShaderParameter("intensity", 1f);
	}

	// ── Match UI (timer + scoreboard) ────────────────────────────────────────

	private void BuildMatchUI()
	{
		var control = GetNodeOrNull<Control>("Control");
		if (control == null) return;

		// ── Timer label — top-center ─────────────────────────────────────────
		_timerLabel = new Label
		{
			Name              = "MatchTimer",
			Text              = "15:00",
			AnchorLeft        = 0.5f, AnchorRight  = 0.5f,
			AnchorTop         = 0f,   AnchorBottom = 0f,
			OffsetLeft        = -70f, OffsetRight  = 70f,
			OffsetTop         = 10f,  OffsetBottom = 46f,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter       = Control.MouseFilterEnum.Ignore,
		};
		_timerLabel.AddThemeFontSizeOverride("font_size", 28);
		_timerLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		_timerLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
		_timerLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		_timerLabel.AddThemeConstantOverride("shadow_offset_y", 2);

		// Background pill behind the timer
		var timerBg = new StyleBoxFlat
		{
			BgColor     = new Color(0.02f, 0.04f, 0.08f, 0.78f),
			BorderColor = new Color(0f, 0.75f, 1f, 0.70f),
		};
		timerBg.SetBorderWidthAll(2);
		timerBg.CornerRadiusTopLeft = timerBg.CornerRadiusTopRight =
		timerBg.CornerRadiusBottomLeft = timerBg.CornerRadiusBottomRight = 6;
		var timerPanel = new PanelContainer
		{
			AnchorLeft = 0.5f, AnchorRight  = 0.5f,
			AnchorTop  = 0f,   AnchorBottom = 0f,
			OffsetLeft = -80f, OffsetRight  = 80f,
			OffsetTop  = 6f,   OffsetBottom = 52f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		timerPanel.AddThemeStyleboxOverride("panel", timerBg);
		timerPanel.AddChild(_timerLabel);
		control.AddChild(timerPanel);

		// ── Score panel — top-right ──────────────────────────────────────────
		var scoreBg = new StyleBoxFlat
		{
			BgColor     = new Color(0.02f, 0.04f, 0.08f, 0.78f),
			BorderColor = new Color(0f, 0.75f, 1f, 0.65f),
		};
		scoreBg.SetBorderWidthAll(2);
		scoreBg.CornerRadiusTopLeft = scoreBg.CornerRadiusTopRight =
		scoreBg.CornerRadiusBottomLeft = scoreBg.CornerRadiusBottomRight = 6;
		scoreBg.ContentMarginLeft = scoreBg.ContentMarginRight =
		scoreBg.ContentMarginTop  = scoreBg.ContentMarginBottom = 8f;

		var scoreContainer = new PanelContainer
		{
			AnchorLeft  = 1f, AnchorRight  = 1f,
			AnchorTop   = 0f, AnchorBottom = 0f,
			OffsetLeft  = -200f, OffsetRight = -10f,
			OffsetTop   = 6f,   OffsetBottom = 200f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		scoreContainer.AddThemeStyleboxOverride("panel", scoreBg);

		var scoreVBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		scoreVBox.AddThemeConstantOverride("separation", 4);

		var scoreTitle = new Label { Text = "FRAGS" };
		scoreTitle.AddThemeFontSizeOverride("font_size", 14);
		scoreTitle.AddThemeColorOverride("font_color", new Color(0f, 0.8f, 1f));
		scoreTitle.HorizontalAlignment = HorizontalAlignment.Center;
		scoreVBox.AddChild(scoreTitle);

		_scorePanel = scoreVBox;
		scoreContainer.AddChild(scoreVBox);
		control.AddChild(scoreContainer);

		// ── Match-end overlay — centered ─────────────────────────────────────
		_matchEndPanel = new Panel { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_matchEndPanel.AnchorLeft   = 0f; _matchEndPanel.AnchorRight  = 1f;
		_matchEndPanel.AnchorTop    = 0f; _matchEndPanel.AnchorBottom = 1f;

		var endBg = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.55f) };
		_matchEndPanel.AddThemeStyleboxOverride("panel", endBg);

		var endVBox = new VBoxContainer
		{
			AnchorLeft = 0.5f, AnchorRight  = 0.5f,
			AnchorTop  = 0.5f, AnchorBottom = 0.5f,
			OffsetLeft = -200f, OffsetRight = 200f, OffsetTop = -80f, OffsetBottom = 80f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		endVBox.AddThemeConstantOverride("separation", 14);

		_matchEndLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_matchEndLabel.AddThemeFontSizeOverride("font_size", 32);
		_matchEndLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.1f));

		_restartLabel = new Label
		{
			Text = "", HorizontalAlignment = HorizontalAlignment.Center,
		};
		_restartLabel.AddThemeFontSizeOverride("font_size", 18);
		_restartLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));

		endVBox.AddChild(_matchEndLabel);
		endVBox.AddChild(_restartLabel);
		_matchEndPanel.AddChild(endVBox);
		control.AddChild(_matchEndPanel);
	}

	// ── Match API (called by MatchManager RPCs) ───────────────────────────────

	/// <summary>Called when the match starts — clears old rows.</summary>
	public void MatchStarted(float duration)
	{
		_scoreLabels.Clear();
		if (_scorePanel != null)
		{
			// Remove old score rows (keep header at index 0)
			for (int i = _scorePanel.GetChildCount() - 1; i > 0; i--)
				_scorePanel.GetChild(i).QueueFree();
		}
		if (_matchEndPanel != null) _matchEndPanel.Visible = false;
		UpdateMatchTimer(duration);
	}

	/// <summary>Ensures a player row exists with the given frag count. Idempotent.</summary>
	public void RegisterPlayer(int peerId, int frags)
	{
		if (_scorePanel == null) return;

		if (!_scoreLabels.TryGetValue(peerId, out var lbl))
		{
			lbl = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
			lbl.AddThemeFontSizeOverride("font_size", 13);
			bool isLocal = (Multiplayer.GetUniqueId() == peerId);
			lbl.AddThemeColorOverride("font_color",
				isLocal ? new Color(0f, 0.85f, 1f) : new Color(0.85f, 0.85f, 0.85f));
			_scorePanel.AddChild(lbl);
			_scoreLabels[peerId] = lbl;
		}

		string prefix = (Multiplayer.GetUniqueId() == peerId) ? "▶ " : "  ";
		lbl.Text = $"{prefix}P{peerId}: {frags} frags";
	}

	/// <summary>Removes a player row when they disconnect.</summary>
	public void RemovePlayer(int peerId)
	{
		if (_scoreLabels.TryGetValue(peerId, out var lbl))
		{
			lbl.QueueFree();
			_scoreLabels.Remove(peerId);
		}
	}

	/// <summary>Called ~every second with remaining seconds.</summary>
	public void UpdateMatchTimer(float remaining)
	{
		if (_timerLabel == null) return;
		int secs  = Mathf.Max(0, (int)remaining);
		int mm    = secs / 60;
		int ss    = secs % 60;
		_timerLabel.Text = $"{mm:D2}:{ss:D2}";

		// Colour shifts: white → yellow → red when under 60 s
		_timerLabel.AddThemeColorOverride("font_color",
			secs > 60  ? new Color(0.95f, 0.95f, 0.95f) :
			secs > 20  ? new Color(1f, 0.8f, 0.1f)      :
						 new Color(1f, 0.2f, 0.2f));
	}

	/// <summary>Called whenever a player's frag count changes.</summary>
	public void UpdateScore(int peerId, int frags)
	{
		// RegisterPlayer creates the row if missing, then updates the label.
		RegisterPlayer(peerId, frags);
	}

	/// <summary>Called when the match ends.</summary>
	public void ShowMatchEnd(int winnerPeerId, int winnerFrags, int restartInSec)
	{
		if (_matchEndLabel == null || _matchEndPanel == null) return;

		bool isWinner = (Multiplayer.GetUniqueId() == winnerPeerId);
		_matchEndLabel.Text = isWinner
			? $"YOU WIN!\n{winnerFrags} frags"
			: $"PLAYER {winnerPeerId} WINS\n{winnerFrags} frags";
		_matchEndLabel.AddThemeColorOverride("font_color",
			isWinner ? new Color(1f, 0.85f, 0.1f) : new Color(0.8f, 0.8f, 0.8f));

		_restartLabel.Text = $"New match in {restartInSec}s…";
		_matchEndPanel.Visible = true;
	}

	// ── Ammo label ────────────────────────────────────────────────────────────

	private void BuildAmmoLabel()
	{
		// Place the ammo counter just below the weapon name label if it exists,
		// otherwise below the weapon panel area.
		var parent = _weaponNameLabel?.GetParent() as Control
					 ?? GetNodeOrNull<Control>("Control/WeaponPanel/VBox")
					 ?? GetNodeOrNull<Control>("Control");
		if (parent == null) return;

		_ammoLabel            = new Label();
		_ammoLabel.Name       = "AmmoLabel";
		_ammoLabel.Text       = "-- / --";
		_ammoLabel.Modulate   = new Color(0.0f, 0.78f, 1.0f, 1.0f); // cyan
		_ammoLabel.MouseFilter= Control.MouseFilterEnum.Ignore;
		parent.AddChild(_ammoLabel);
	}

	// ── Process ───────────────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		// Fade out hit marker.
		if (_hitMarkerTimer > 0 && _hitMarker != null)
		{
			_hitMarkerTimer -= delta;
			float alpha = (float)(_hitMarkerTimer / HitMarkerDuration);
			var c = _hitMarker.Color;
			_hitMarker.Color = new Color(c.R, c.G, c.B, Mathf.Max(alpha * 0.8f, 0f));
			if (_hitMarkerTimer <= 0)
				_hitMarker.Visible = false;
		}

		// Fade out damage vignette.
		if (_vignetteFade > 0f && _vignetteShader != null)
		{
			_vignetteFade -= (float)delta;
			_vignetteIntensity = Mathf.Clamp(_vignetteFade / VignetteDuration, 0f, 1f);
			_vignetteShader.SetShaderParameter("intensity", _vignetteIntensity);
			if (_vignetteFade <= 0f)
				_vignetteShader.SetShaderParameter("intensity", 0f);
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Toggle pause/escape menu via the "pause" action (Escape key by default).
		if (@event.IsActionPressed("pause") && !@event.IsEcho())
		{
			ToggleEscapeMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	// ── Health ────────────────────────────────────────────────────────────────

	public void SetMaxHealth(int maxHealth)
	{
		_maxHealth = maxHealth;
		if (_healthBar  != null) _healthBar.MaxValue  = _maxHealth;
		if (_ghostBar   != null) _ghostBar.MaxValue   = _maxHealth;
		UpdateHealthDisplay();
	}

	public void UpdateHealth(int newHealth)
	{
		int previous = _currentHealth;
		_currentHealth = Mathf.Clamp(newHealth, 0, _maxHealth);

		// Ghost drain effect: only triggers when health decreases.
		if (_ghostBar != null && newHealth < previous)
		{
			// Keep ghost at old value — kill any running tween and start a new one.
			_ghostTween?.Kill();
			_ghostBar.MaxValue = _maxHealth;
			_ghostBar.Value    = previous;
			_ghostBar.Modulate = new Color(1f, 1f, 1f, 0.85f); // bright white

			_ghostTween = CreateTween().SetParallel();

			// 1) After 0.25s hold, slide the ghost bar down to the new value over 0.6s
			_ghostTween.TweenProperty(_ghostBar, "value", (double)_currentHealth, 0.6)
					   .SetDelay(0.25)
					   .SetTrans(Tween.TransitionType.Cubic)
					   .SetEase(Tween.EaseType.Out);

			// 2) Fade from white → transparent over 0.85s total
			_ghostTween.TweenProperty(_ghostBar, "modulate:a", 0.0f, 0.85)
					   .SetTrans(Tween.TransitionType.Quad)
					   .SetEase(Tween.EaseType.In);

			// 3) Flash damage vignette
			FlashVignette();
		}

		UpdateHealthDisplay();
	}

	private void UpdateHealthDisplay()
	{
		if (_healthBar  != null) _healthBar.Value  = _currentHealth;
		if (_healthLabel!= null) _healthLabel.Text = $"HP: {_currentHealth}/{_maxHealth}";

		// Shift fill colour: green (full) → yellow (mid) → red (low)
		if (_hpFillStyle != null)
		{
			float ratio = _maxHealth > 0 ? (float)_currentHealth / _maxHealth : 0f;
			Color fillColor;
			if (ratio > 0.5f)
			{
				// green-cyan → yellow
				fillColor = new Color(0.05f, 0.85f, 0.40f).Lerp(
					new Color(0.95f, 0.75f, 0.05f), (1f - ratio) * 2f);
			}
			else
			{
				// yellow → red
				fillColor = new Color(0.95f, 0.75f, 0.05f).Lerp(
					new Color(0.90f, 0.10f, 0.10f), (0.5f - ratio) * 2f);
			}
			_hpFillStyle.BgColor = fillColor;
		}
	}

	// ── Ammo ──────────────────────────────────────────────────────────────────

	/// <summary>Called by WeaponController.AmmoChanged signal and per-weapon AmmoChanged.</summary>
	public void UpdateAmmo(int current, int max)
	{
		if (_ammoLabel == null) return;
		_ammoLabel.Text = $"{current} / {max}";

		// Colour cue: red when low (≤25% of max), orange when medium, cyan when full.
		float ratio = max > 0 ? (float)current / max : 0f;
		_ammoLabel.Modulate = ratio <= 0.25f
			? new Color(1f, 0.2f, 0.2f, 1f)   // red
			: ratio <= 0.55f
				? new Color(1f, 0.65f, 0f, 1f)  // orange
				: new Color(0f, 0.78f, 1f, 1f); // cyan
	}

	// ── Weapon panel ──────────────────────────────────────────────────────────

	/// <summary>Called by WeaponController.WeaponSwitched signal.</summary>
	public void OnWeaponSwitched(int index, string weaponName)
	{
		if (_weaponNameLabel != null)
			_weaponNameLabel.Text = weaponName;

		if (_weaponSlots == null) return;

		for (int i = 0; i < _weaponSlots.GetChildCount(); i++)
		{
			if (_weaponSlots.GetChild(i) is Panel slot)
			{
				slot.SelfModulate = (i == index)
					? new Color(1f, 0.8f, 0.2f)   // active — golden
					: new Color(0.4f, 0.4f, 0.4f); // inactive — grey
			}
		}
	}

	// ── Hit marker ────────────────────────────────────────────────────────────

	/// <summary>Called by WeaponBase.HitMarker signal.</summary>
	public void ShowHitMarker(bool isKill = false)
	{
		if (_hitMarker == null) return;
		_hitMarker.Color = isKill
			? new Color(1f, 0.8f, 0f, 0.8f)    // gold for kill
			: new Color(1f, 0.2f, 0.2f, 0.8f);  // red for hit
		_hitMarker.Visible  = true;
		_hitMarkerTimer = HitMarkerDuration;
	}

	// ── Escape menu ───────────────────────────────────────────────────────────

	private void ToggleEscapeMenu()
	{
		if (_escapePanel == null) return;

		bool opening = !_escapePanel.Visible;
		_escapePanel.Visible = opening;
		Input.MouseMode = opening ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		GetTree().Paused = opening;
		// HUD must keep processing while paused so it can unpause.
		ProcessMode = opening ? ProcessModeEnum.Always : ProcessModeEnum.Inherit;
	}

	// ── Button callbacks (wired in HUD.tscn) ─────────────────────────────────

	public void OnHover()
	{
		if (_hoverSound?.Stream != null)
			_hoverSound.Play();
	}

	public void OnReturnPressed()
	{
		if (_escapePanel == null) return;
		_escapePanel.Visible = false;
		Input.MouseMode      = Input.MouseModeEnum.Captured;
		GetTree().Paused     = false;
		ProcessMode          = ProcessModeEnum.Inherit;
	}

	public void OnSettingsPressed()
	{
		if (_settingsScene == null)
		{
			GD.PushWarning("HUD: SettingsMenu scene not found!");
			return;
		}
		if (_settingsInstance == null)
		{
			_settingsInstance = _settingsScene.Instantiate<Control>();
			_escapePanel?.AddChild(_settingsInstance);
		}
		_settingsInstance.Show();
	}

	public void OnMainMenuPressed()
	{
		OnReturnPressed();
		GetNode<Main>("/root/Main").ReturnToMainMenu();
	}

	public void OnExitPressed()
	{
		GetTree().Paused = false;
		GetTree().Quit();
	}
}
