// Draws grid lines from world position, so the lines stay put while the sheet moves with the camera.
shader_type spatial;
render_mode unshaded;

uniform vec3 base_color : source_color = vec3(0.015, 0.02, 0.045);
uniform vec3 line_color : source_color = vec3(0.07, 0.11, 0.2);
uniform vec3 major_color : source_color = vec3(0.13, 0.22, 0.4);
uniform float cell_size = 5.0;
uniform float major_every = 5.0;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// 1.0 on a grid line, fading to 0.0 about one pixel away from it.
float grid_line(vec2 p, float width_px) {
	vec2 distance_px = abs(fract(p - 0.5) - 0.5) / fwidth(p);
	return 1.0 - clamp(min(distance_px.x, distance_px.y) - width_px * 0.5 + 0.5, 0.0, 1.0);
}

void fragment() {
	vec2 p = world_position.xz / cell_size;
	vec3 color = mix(base_color, line_color, grid_line(p, 1.0));
	color = mix(color, major_color, grid_line(p / major_every, 2.0));
	ALBEDO = color;
}
