using Godot;
using System;

public static class WeaponHelpers
{
	/// <summary>
	/// Spawns a glowing cylinder tracer from muzzle to hit target and fades it out quickly.
	/// </summary>
	public static void SpawnTracerLine(Node parent, Vector3 from, Vector3 to)
	{
		float dist = from.DistanceTo(to);
		if (dist < 0.01f) return;

		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(1f, 0.92f, 0.45f, 1f),
			EmissionEnabled = true,
			Emission = new Color(1f, 0.85f, 0.2f),
			EmissionEnergyMultiplier = 6f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};

		var cyl = new CylinderMesh
		{
			TopRadius    = 0.012f,
			BottomRadius = 0.012f,
			Height       = dist,
			RadialSegments = 4,
			Rings        = 1,
			Material     = mat,
		};

		var mesh = new MeshInstance3D { Mesh = cyl };
		parent.GetTree().Root.AddChild(mesh);

		mesh.GlobalPosition = from.Lerp(to, 0.5f);
		Vector3 dir = (to - from).Normalized();

		if (dir.Length() > 0.001f && dir != Vector3.Up && dir != Vector3.Down)
			mesh.LookAt(to, Vector3.Up);
		else
			mesh.LookAt(to, Vector3.Right);
			
		mesh.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2f);

		var tween = mesh.CreateTween();
		tween.TweenProperty(mat, "albedo_color:a", 0f, 0.05f).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(mesh)) mesh.QueueFree();
		}));
	}

	public static void SpawnImpact(Node parent, PackedScene effectScene, Vector3 position)
	{
		if (effectScene == null) return;
		var impact = effectScene.Instantiate<Node3D>();
		if (impact == null) return;
		parent.GetTree().Root.AddChild(impact);
		impact.GlobalPosition = position;
	}
}
