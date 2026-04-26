// LightningBeam — advanced electric-web shader for the LightningGun beam cylinder.
//
// Technique stack:
//   1. Voronoi cell-edge web   (two animated layers)
//   2. FBM arc displacement     (zigzag "bolt path" running along the beam UV.y axis)
//   3. White hot core           (thin additive strip along the beam length, displaced by FBM)
//   4. Temporal flicker         (two incommensurate frequencies → feels random)
//   5. Color gradient           (white → cyan → deep blue from bright → dim)
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec4  arc_color   : source_color = vec4(0.10, 0.55, 1.0, 1.0);
uniform vec4  core_color  : source_color = vec4(0.80, 0.95, 1.0, 1.0);
uniform float speed           = 12.0;
uniform float cell_density    = 5.0;
uniform float arc_sharpness   = 0.13;
uniform float brightness      = 4.0;
uniform float core_width      = 0.045;   // half-width of the hot white core strip
uniform float fbm_strength    = 0.18;    // how much the core arc zigzags

// ---- Math helpers ---------------------------------------------------
float hash1(float n) { return fract(sin(n) * 43758.5453); }

vec2 hash2(vec2 p) {
	p = vec2(dot(p, vec2(127.1, 311.7)),
	         dot(p, vec2(269.5, 183.3)));
	return fract(sin(p) * 43758.5453);
}

// 2-D value noise (no sampler — pure math)
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash1(dot(i,          vec2(1.0, 157.0)));
	float b = hash1(dot(i + vec2(1,0), vec2(1.0, 157.0)));
	float c = hash1(dot(i + vec2(0,1), vec2(1.0, 157.0)));
	float d = hash1(dot(i + vec2(1,1), vec2(1.0, 157.0)));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// FBM — 4 octaves
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * vnoise(p);
		p = p * 2.13 + vec2(1.7, 0.9);
		a *= 0.48;
	}
	return v;
}

// Voronoi — returns vec2(F1, F2); F2-F1 is small at cell edges
vec2 voronoi(vec2 uv) {
	vec2 i = floor(uv), f = fract(uv);
	float f1 = 8.0, f2 = 8.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 n  = vec2(float(x), float(y));
			vec2 pt = hash2(i + n);
			pt = 0.5 + 0.5 * sin(TIME * speed + TAU * pt);
			float d = length(n + pt - f);
			if (d < f1) { f2 = f1; f1 = d; }
			else if (d < f2) { f2 = d; }
		}
	}
	return vec2(f1, f2);
}

void fragment() {
	// ---- Voronoi web (two layers) -----------------------------------
	vec2 uv1 = vec2(UV.x * cell_density * 0.55, UV.y * cell_density)
	         + vec2(TIME * 0.06,  TIME * 0.45);
	vec2 uv2 = vec2(UV.x * cell_density * 0.90, UV.y * cell_density * 1.7)
	         + vec2(-TIME * 0.04, TIME * 0.68);

	vec2 v1 = voronoi(uv1);
	vec2 v2 = voronoi(uv2);

	float edge1 = pow(1.0 - smoothstep(0.0, arc_sharpness,        v1.y - v1.x), 1.9);
	float edge2 = pow(1.0 - smoothstep(0.0, arc_sharpness * 0.68, v2.y - v2.x), 2.1);
	float node1 = pow(max(1.0 - smoothstep(0.0, 0.18, v1.x), 0.0), 3.0);

	float web  = edge1 * 0.85 + edge2 * 0.40;
	float node = node1 * 1.8;

	// ---- FBM arc displacement: a zig-zag "bolt" down UV.y ----------
	// Animate two independent FBM layers for the X displacement of the core arc.
	float disp1 = fbm(vec2(UV.y * 2.8 + TIME * 0.55, TIME * 0.28)) - 0.5;
	float disp2 = fbm(vec2(UV.y * 5.5 - TIME * 0.70, TIME * 0.43 + 3.14)) - 0.5;
	float disp  = disp1 * 0.7 + disp2 * 0.3;

	// Distance from the displaced centerline (UV.x = 0.5 is the seam/front)
	float cx     = abs(UV.x - 0.5 + disp * fbm_strength);
	// Wrap: if cx > 0.5 the arc came around to the "back" — clamp it
	cx = min(cx, 1.0 - cx);

	float coreGlow = smoothstep(core_width * 2.5, 0.0, cx);          // wide soft glow
	float coreHot  = smoothstep(core_width * 0.6, 0.0, cx);          // narrow white core

	// ---- Temporal flicker -------------------------------------------
	float flicker = 0.78 + 0.12 * sin(TIME * 53.1) + 0.10 * sin(TIME * 37.7);

	// ---- Combine ----------------------------------------------------
	// Colour: web uses arc_color, bright nodes/core use core_color (white-ish)
	vec3 webCol  = arc_color.rgb  * (web + node);
	vec3 coreCol = core_color.rgb * (coreHot * 2.5 + coreGlow * 0.6);

	vec3  col   = webCol + coreCol;
	float alpha = clamp(web * 0.75 + node * 0.3 + coreGlow * 0.7 + coreHot * 0.9, 0.0, 1.0);

	ALBEDO = col * brightness * flicker;
	ALPHA  = alpha * flicker;
}
