// Additive unshaded shader for ImmediateMesh line segments.
// Used by the LightningGun bolt-network effect.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4  line_color : source_color = vec4(0.25, 0.70, 1.0, 0.85);
uniform float brightness = 4.5;

void fragment() {
	ALBEDO = line_color.rgb * brightness;
	ALPHA  = line_color.a;
}
