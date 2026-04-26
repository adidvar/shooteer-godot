// RespawnCircle.gdshader
// Draws a clock-wipe progress ring on a QuadMesh.
// Occluded by world geometry (standard depth test — NOT no_depth_test).
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_opaque;

/// Fill fraction 0..1 (0 = empty, 1 = full ring visible).
uniform float progress : hint_range(0.0, 1.0) = 1.0;
/// Colour of the filled arc.
uniform vec4 fill_color   : source_color = vec4(0.0, 0.78, 1.0, 1.0);
/// Colour of the unfilled arc background.
uniform vec4 bg_color     : source_color = vec4(0.15, 0.15, 0.15, 0.5);
/// Inner radius (0..1 in UV space, 0.5 = full circle centre).
uniform float inner_r : hint_range(0.0, 0.5) = 0.30;
/// Outer radius.
uniform float outer_r : hint_range(0.0, 0.5) = 0.48;

void fragment() {
	// Map UV 0..1 → -1..+1
	vec2 uv = UV * 2.0 - 1.0;
	float r = length(uv);

	// Discard outside the ring band completely.
	if (r < inner_r || r > outer_r) {
		ALPHA = 0.0;
		return;
	}

	// Angle: 0 at top, clockwise (matching a typical loading indicator).
	// atan(x, -y) gives angle from +Y axis.
	float angle = atan(uv.x, -uv.y) / (2.0 * 3.14159265358979) + 0.5;

	if (angle <= progress) {
		ALBEDO = fill_color.rgb;
		ALPHA  = fill_color.a;
	} else {
		ALBEDO = bg_color.rgb;
		ALPHA  = bg_color.a;
	}
}
