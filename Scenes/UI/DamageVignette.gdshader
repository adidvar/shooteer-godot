// Full-screen damage vignette: red at screen edges, transparent in centre.
// Set `intensity` to 0..1 via ShaderMaterial.SetShaderParameter.
shader_type canvas_item;

uniform vec4  vignette_color : source_color = vec4(1.0, 0.04, 0.04, 1.0);
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform float radius    : hint_range(0.0, 1.0) = 0.55;  // centre dead-zone radius
uniform float softness  : hint_range(0.01, 1.0) = 0.40;

void fragment() {
	vec2 uv = UV - 0.5;
	// Slightly elliptical — wider than tall like a real TV vignette
	uv.x *= 0.85;
	float dist = length(uv) * 2.0;
	float ring = smoothstep(radius, radius + softness, dist);
	float alpha = ring * intensity;
	// Subtle pulse to sell the "damage hit" feel
	alpha *= 0.88 + 0.12 * sin(TIME * 12.0) * intensity;
	COLOR = vec4(vignette_color.rgb, clamp(alpha, 0.0, 0.9));
}
