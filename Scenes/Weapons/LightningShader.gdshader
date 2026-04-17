shader_type spatial;
render_mode unshaded, blend_add;

uniform sampler2D noise_texture : repeat_enable;
uniform vec4 lightning_color : source_color = vec4(0.2, 0.7, 1.0, 1.0);
uniform float speed = 5.0;
uniform float thickness = 1.0;

void fragment() {
	vec2 uv = UV;
	
	// Scroll noise
	uv.x += TIME * speed;
	
	// Read noise value
	float noise_val = texture(noise_texture, uv).r;
	
	// Create arc effect based on Y center
	float mask = abs(UV.y - 0.5) * 5.0; // Distance from center
	
	// Distort mask with noise
	float arc = abs(noise_val - mask);
	
	// Sharpen
	float power = smoothstep(0.1 / thickness, 0.0, arc);
	
	ALBEDO = lightning_color.rgb * power * 3.0;
	ALPHA = power * lightning_color.a;
}
