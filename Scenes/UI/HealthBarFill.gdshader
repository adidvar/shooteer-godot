// Gradient fill shader for the HealthBar ProgressBar.
// Apply as a canvas_item material on the ProgressBar node itself.
// The fill area UV.x runs 0→1 left to right across the ENTIRE bar width,
// but we colour only the pixels that are actually "filled" (the progress area).
// Works because Godot's ProgressBar clips child drawing automatically.
shader_type canvas_item;

uniform vec4 color_high : source_color = vec4(0.05, 0.85, 0.40, 1.0); // green-cyan
uniform vec4 color_mid  : source_color = vec4(0.95, 0.70, 0.05, 1.0); // orange
uniform vec4 color_low  : source_color = vec4(0.90, 0.10, 0.10, 1.0); // red
uniform float hp_ratio  : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	// Pick colour based on current HP level
	vec4 base_color;
	if (hp_ratio > 0.5) {
		base_color = mix(color_mid, color_high, (hp_ratio - 0.5) * 2.0);
	} else {
		base_color = mix(color_low,  color_mid,  hp_ratio * 2.0);
	}
	// Horizontal brightness gradient: brighter edge on the left → darker right
	float shine = 0.65 + 0.35 * (1.0 - UV.x);
	// Vertical highlight: slightly brighter on top third (glass-like sheen)
	float top_sheen = 1.0 + 0.20 * smoothstep(0.0, 0.35, 1.0 - UV.y);
	vec3 final_rgb = base_color.rgb * shine * top_sheen;
	// Keep original pixel alpha (transparent outside the fill)
	COLOR = vec4(final_rgb, COLOR.a);
}
