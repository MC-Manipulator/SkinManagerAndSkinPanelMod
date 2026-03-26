shader_type canvas_item;

uniform vec4 outline_color : source_color = vec4(1.0, 0.8, 0.2, 1.0); // 金色
uniform float outline_width : hint_range(0.0, 10.0) = 2.0;

void fragment() {
    vec4 col = texture(TEXTURE, UV);
    vec2 size = TEXTURE_PIXEL_SIZE * outline_width;
    
    // 只对外轮廓采样，忽略内部像素
    float alpha = 0.0;
    alpha += texture(TEXTURE, UV + vec2(size.x, 0.0)).a;
    alpha += texture(TEXTURE, UV + vec2(-size.x, 0.0)).a;
    alpha += texture(TEXTURE, UV + vec2(0.0, size.y)).a;
    alpha += texture(TEXTURE, UV + vec2(0.0, -size.y)).a;
    
    alpha += texture(TEXTURE, UV + vec2(size.x, size.y)).a;
    alpha += texture(TEXTURE, UV + vec2(-size.x, size.y)).a;
    alpha += texture(TEXTURE, UV + vec2(-size.x, -size.y)).a;
    alpha += texture(TEXTURE, UV + vec2(size.x, -size.y)).a;
    
    // 如果当前像素透明，且周围有不透明像素，则绘制描边色
    // 如果当前像素不透明，则绘制原色 (维持 Spine 原本的色彩)
    if (col.a == 0.0 && alpha > 0.0) {
        COLOR = outline_color;
    } else {
        COLOR = col;
    }
}