using Godot;
using System.Collections.Generic;

/// <summary>
/// SaveLoadMenu — full-screen overlay for browsing and managing save slots.
/// Built entirely in C# so no separate .tscn scene is required.
///
/// Layout:
///   ┌─────────────────────────────────────────┐
///   │  SAVE / LOAD GAME              [✕ Close] │
///   │  ┌──────────────────────────────────┐   │
///   │  │ Slot 1 — "My Game"  2025-01-01   │   │
///   │  │ [Save]  [Load]  [Delete]          │   │
///   │  ├──────────────────────────────────┤   │
///   │  │ Slot 2 — EMPTY                   │   │
///   │  │ [Save]                            │   │
///   │  └──────────────────────────────────┘   │
///   └─────────────────────────────────────────┘
/// </summary>
public partial class SaveLoadMenu : Control
{
    private VBoxContainer _slotsContainer;
    private Label         _statusLabel;

    public override void _Ready()
    {
        BuildUI();
        Hide(); // hidden by default; shown explicitly by HUD
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // This node is the full-screen backdrop — absorbs all pointer events.
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Dimmed background.
        var bg = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // CenterContainer reliably centres the panel regardless of viewport size.
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { CustomMinimumSize = new Vector2(580, 500) };
        center.AddChild(panel);

        // Single MarginContainer fills the panel — no sibling containers to block clicks.
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        // ── Header ────────────────────────────────────────────────────────────
        var header = new HBoxContainer();
        vbox.AddChild(header);

        var title = new Label { Text = "SAVE / LOAD GAME" };
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(title);

        var closeBtn = new Button { Text = "✕ Close" };
        closeBtn.Pressed += () => Hide();
        header.AddChild(closeBtn);

        vbox.AddChild(new HSeparator());

        // ── Slot list in scroll container ─────────────────────────────────────
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        _slotsContainer = new VBoxContainer();
        _slotsContainer.AddThemeConstantOverride("separation", 6);
        _slotsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_slotsContainer);

        // Status bar at the bottom — shows feedback for save/load/delete actions.
        vbox.AddChild(new HSeparator());
        _statusLabel = new Label { Text = "" };
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.5f));
        vbox.AddChild(_statusLabel);
    }

    // ── Slot rendering ────────────────────────────────────────────────────────

    /// <summary>Rebuild the slot list from current SaveManager state.</summary>
    public void RefreshSlots()
    {
        foreach (Node child in _slotsContainer.GetChildren())
            child.QueueFree();

        if (SaveManager.Instance == null)
        {
            var lbl = new Label { Text = "SaveManager not available." };
            _slotsContainer.AddChild(lbl);
            return;
        }

        var slots = SaveManager.Instance.GetAllSlotsMeta();
        foreach (var (slotIdx, name, date, playtime, isEmpty) in slots)
            _slotsContainer.AddChild(BuildSlotRow(slotIdx, name, date, playtime, isEmpty));
    }

    private Control BuildSlotRow(int slotIdx, string name, string date, int playtime, bool isEmpty)
    {
        var panel = new PanelContainer();

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(hbox);

        // Slot info.
        var info = new VBoxContainer();
        info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(info);

        string slotTitle = isEmpty
            ? $"Slot {slotIdx}  —  EMPTY"
            : $"Slot {slotIdx}  —  {name}";
        var nameLabel = new Label { Text = slotTitle };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        info.AddChild(nameLabel);

        if (!isEmpty)
        {
            string timeStr = FormatPlayTime(playtime);
            var metaLabel  = new Label { Text = $"{date}   ⏱ {timeStr}" };
            metaLabel.AddThemeFontSizeOverride("font_size", 11);
            metaLabel.Modulate = new Color(0.75f, 0.75f, 0.75f);
            info.AddChild(metaLabel);
        }

        // Buttons.
        int capturedIdx = slotIdx;

        var saveBtn = new Button { Text = isEmpty ? "💾 Save Here" : "💾 Overwrite" };
        saveBtn.Pressed += () =>
        {
            if (SaveManager.Instance == null)
            {
                SetStatus("SaveManager unavailable.", error: true);
                return;
            }
            bool ok = SaveManager.Instance.SaveGame(capturedIdx);
            SetStatus(ok ? $"Saved to Slot {capturedIdx}." : $"Save failed — check console.", !ok);
            RefreshSlots();
        };
        hbox.AddChild(saveBtn);

        if (!isEmpty)
        {
            var loadBtn = new Button { Text = "📂 Load" };
            loadBtn.Pressed += () => OnLoadPressed(capturedIdx);
            hbox.AddChild(loadBtn);

            var delBtn = new Button { Text = "🗑 Delete" };
            delBtn.Modulate = new Color(1f, 0.5f, 0.5f);
            delBtn.Pressed += () =>
            {
                SaveManager.Instance?.DeleteSlot(capturedIdx);
                SetStatus($"Slot {capturedIdx} deleted.");
                RefreshSlots();
            };
            hbox.AddChild(delBtn);
        }

        return panel;
    }

    // ── Load callback ─────────────────────────────────────────────────────────

    private void OnLoadPressed(int slotIdx)
    {
        var data = SaveManager.Instance?.LoadGame(slotIdx);
        if (data == null)
        {
            SetStatus($"Could not load Slot {slotIdx}.", error: true);
            return;
        }

        // Restore match state if a match is active.
        var matchMgr = GetNodeOrNull<MatchManager>("/root/Main/MatchManager");
        matchMgr?.RestoreFromSave(data);

        // Restore local player.
        RestoreLocalPlayer(data);

        SetStatus($"Slot {slotIdx} loaded.");
        Hide();
    }

    private static void RestoreLocalPlayer(SaveData data)
    {
        // Players are manual children of Map/Players (see Map.cs — no MultiplayerSpawner).
        var container = (Engine.GetMainLoop() as SceneTree)
            ?.Root.GetNodeOrNull("/root/Main/LevelContainer/Map/Players");
        if (container == null) return;

        foreach (Node child in container.GetChildren())
        {
            if (child is not Player p || !p.IsMultiplayerAuthority()) continue;

            p.HealthComp.CurrentHealth = data.PlayerHealth;

            // Reset velocity so residual gravity from the previous frame doesn't
            // push CharacterBody3D into geometry on the first post-teleport tick.
            p.Velocity = Vector3.Zero;

            // Restore look direction.
            // Body yaw — only Y axis rotates on the player node.
            var bodyRot = p.Rotation;
            bodyRot.Y = data.PlayerRotationY;
            p.Rotation = bodyRot;
            // Camera pitch — child Camera3D stores X-axis tilt.
            var camera = p.GetNodeOrNull<Camera3D>("Camera3D");
            if (camera != null)
            {
                var camRot = camera.Rotation;
                camRot.X = data.CameraRotationX;
                camera.Rotation = camRot;
            }

            // Compute a Y spawn offset from the actual capsule shape.
            // On the first physics tick after teleport, IsOnFloor() returns false,
            // so MovementComponent calls AirMove and immediately applies one frame
            // of gravity before MoveAndSlide re-establishes floor contact.
            // Elevating by one scaled capsule radius gives the physics engine enough
            // clearance to resolve that single gravity step without clipping.
            float yOffset = ComputeCapsuleSpawnOffset(p);
            p.GlobalPosition = new Vector3(data.PlayerX, data.PlayerY + yOffset, data.PlayerZ);

            p.WeaponCtrl?.SwitchWeapon(data.WeaponIndex);
            p.WeaponCtrl?.RestoreAmmoData(data.WeaponAmmo);
            break;
        }
    }

    /// <summary>
    /// Returns the Y offset to add when teleporting a CharacterBody3D so the
    /// capsule bottom starts one scaled radius above the saved floor surface.
    ///
    /// Derivation for Player.tscn defaults (CapsuleShape3D height=2, radius=0.5,
    /// CollisionShape3D scale=0.8, local Y=1.0):
    ///   scaledRadius    = 0.5 * 0.8 = 0.4
    ///   capsuleHalfH    = (2.0 / 2) * 0.8 = 0.8
    ///   capsuleBottomΔY = 1.0 − 0.8 = +0.2   (bottom is 0.2 above player origin)
    ///
    /// At save time the player stood on the floor:
    ///   floorY = savedOriginY + 0.2
    /// After restore with offset:
    ///   newOriginY = savedOriginY + 0.4
    ///   capsuleBottomY = newOriginY + 0.2 = floorY + 0.4  → safely above the floor.
    /// </summary>
    private static float ComputeCapsuleSpawnOffset(Player p)
    {
        var cs = p.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (cs?.Shape is not CapsuleShape3D capsule)
            return 0.4f; // fallback matching default capsule radius * default scale

        // The CollisionShape3D Transform.Basis encodes non-uniform scale.
        float scaleXZ = cs.Transform.Basis.Scale.X;

        // One scaled radius above the save position is enough clearance.
        return capsule.Radius * scaleXZ;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string FormatPlayTime(int seconds)
    {
        int h = seconds / 3600;
        int m = (seconds % 3600) / 60;
        int s = seconds % 60;
        return h > 0 ? $"{h}h {m:D2}m {s:D2}s" : $"{m:D2}m {s:D2}s";
    }

    private void SetStatus(string message, bool error = false)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color",
            error ? new Color(1f, 0.35f, 0.35f) : new Color(0.4f, 1f, 0.5f));
    }
}
