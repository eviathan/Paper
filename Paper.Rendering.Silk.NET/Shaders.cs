namespace Paper.Rendering.Silk.NET
{
    /// <summary>Embedded GLSL shader sources for Paper's OpenGL renderer.</summary>
    internal static class Shaders
    {
        // ── Rect (coloured box with rounded corners + border) ─────────────────

        public const string RectVert = @"
#version 330 core

// Base quad vertex (0..1 range)
layout (location = 0) in vec2 aPos;

// Per-instance attributes
layout (location = 1) in vec2  iPos;          // top-left corner in screen pixels
layout (location = 2) in vec2  iSize;         // width / height in pixels
layout (location = 3) in vec4  iBgColor;
layout (location = 4) in vec4  iBorderColor;
layout (location = 5) in float iBorderWidth;
layout (location = 6) in vec4  iRadii;        // (topLeft, topRight, bottomRight, bottomLeft)
layout (location = 7) in float iRotation;     // rotation in radians, around rect center

uniform vec2 uResolution;   // (screen_w, screen_h)

out vec2  vFragCoord;       // pixel coord relative to element top-left
out vec2  vSize;
out vec4  vBgColor;
out vec4  vBorderColor;
out float vBorderWidth;
out vec4  vRadii;

void main() {
    vFragCoord   = aPos * iSize;
    vSize        = iSize;
    vBgColor     = iBgColor;
    vBorderColor = iBorderColor;
    vBorderWidth = iBorderWidth;
    vRadii       = iRadii;

    vec2 pixelPos = iPos + aPos * iSize;
    if (abs(iRotation) > 0.0001) {
        vec2 center = iPos + iSize * 0.5;
        vec2 rel    = pixelPos - center;
        float s     = sin(iRotation);
        float c_    = cos(iRotation);
        pixelPos    = center + vec2(c_ * rel.x - s * rel.y, s * rel.x + c_ * rel.y);
    }
    vec2 clipPos   = (pixelPos / uResolution) * 2.0 - 1.0;
    clipPos.y      = -clipPos.y;          // flip Y (screen space → clip space)
    gl_Position    = vec4(clipPos, 0.0, 1.0);
}
";

        public const string RectFrag = @"
#version 330 core

in vec2  vFragCoord;
in vec2  vSize;
in vec4  vBgColor;
in vec4  vBorderColor;
in float vBorderWidth;
in vec4  vRadii;           // (topLeft, topRight, bottomRight, bottomLeft)

out vec4 FragColor;

// Signed distance to a rounded rectangle centered at the origin with per-corner radii.
// b = half-extents, r = corner radius for this fragment's corner.
float sdRoundRect(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

void main() {
    vec2  center     = vSize * 0.5;
    vec2  halfExtent = vSize * 0.5;

    vec2  p = vFragCoord - center;
    // Select corner radius based on which quadrant the fragment is in.
    // vRadii: (topLeft, topRight, bottomRight, bottomLeft)
    float r;
    if (p.x <= 0.0 && p.y <= 0.0)      r = vRadii.x; // top-left
    else if (p.x > 0.0 && p.y <= 0.0)  r = vRadii.y; // top-right
    else if (p.x > 0.0 && p.y > 0.0)   r = vRadii.z; // bottom-right
    else                                r = vRadii.w; // bottom-left
    float radius     = clamp(r, 0.0, min(halfExtent.x, halfExtent.y));

    float outerDist  = sdRoundRect(p, halfExtent, radius);
    float innerDist  = sdRoundRect(p, halfExtent - vBorderWidth, max(0.0, radius - vBorderWidth));

    // Anti-alias: 1px smoothstep across the SDF boundary
    float outerAlpha = 1.0 - smoothstep(-0.5, 0.5, outerDist);
    float innerAlpha = 1.0 - smoothstep(-0.5, 0.5, innerDist);

    // Border zone
    float borderFactor = outerAlpha - innerAlpha;

    // Blend: background inside, border on edge
    vec4 color = mix(vBgColor, vBorderColor, clamp(borderFactor, 0.0, 1.0));
    color.a   *= outerAlpha;

    if (color.a < 0.001) discard;
    FragColor = color;
}
";

        // ── Text ──────────────────────────────────────────────────────────────
        // Simple textured quad renderer for font atlas glyphs.

        public const string TextVert = @"
#version 330 core

layout (location = 0) in vec2 aPos;
layout (location = 1) in vec2 aUV;

// Per-instance
layout (location = 2) in vec2  iPos;
layout (location = 3) in vec2  iSize;
layout (location = 4) in vec4  iUVRect;    // (u0, v0, u1, v1)
layout (location = 5) in vec4  iColor;

uniform vec2  uResolution;
uniform float uItalicSkew;   // synthetic italic: horizontal shear per pixel of height (0 = off)

out vec2 vUV;
out vec4 vColor;

void main() {
    vColor = iColor;
    vUV    = iUVRect.xy + aUV * (iUVRect.zw - iUVRect.xy);

    vec2 pixelPos = iPos + aPos * iSize;
    // Italic shear: shift top of glyph right, keep bottom fixed.
    // aPos.y=0 → top of glyph (full shift), aPos.y=1 → bottom (no shift).
    pixelPos.x += (1.0 - aPos.y) * iSize.y * uItalicSkew;
    vec2 clipPos  = (pixelPos / uResolution) * 2.0 - 1.0;
    clipPos.y     = -clipPos.y;
    gl_Position   = vec4(clipPos, 0.0, 1.0);
}
";

        public const string TextFrag = @"
#version 330 core

in vec2 vUV;
in vec4 vColor;

uniform sampler2D uFontAtlas;

out vec4 FragColor;

void main() {
    float alpha  = texture(uFontAtlas, vUV).r;
    FragColor    = vec4(vColor.rgb, vColor.a * alpha);
    if (FragColor.a < 0.001) discard;
}
";

        // ── Viewport ─────────────────────────────────────────────────────────
        // Renders a single textured quad mapped to a screen-space rect.
        // Used to display the embedded engine's game-view framebuffer texture.

        public const string ViewportVert = @"
#version 330 core

layout (location = 0) in vec2 aPos;     // [0,1] base quad
layout (location = 1) in vec2 aUV;      // [0,1] base UV

uniform vec4 uRect;         // x, y, w, h in screen pixels (top-left origin)
uniform vec4 uUV;           // UV rect (u0, v0, u1, v1); default (0,0,1,1) for full texture
uniform vec2 uResolution;   // screen width, height

out vec2 vUV;

void main() {
    vec2 pixelPos = vec2(uRect.x, uRect.y) + aPos * vec2(uRect.z, uRect.w);
    vec2 clipPos  = (pixelPos / uResolution) * 2.0 - 1.0;
    clipPos.y     = -clipPos.y;
    gl_Position   = vec4(clipPos, 0.0, 1.0);
    vec2 baseUV   = vec2(aUV.x, 1.0 - aUV.y);
    vUV           = uUV.xy + baseUV * (uUV.zw - uUV.xy);
}
";

        public const string ViewportFrag = @"
#version 330 core

in vec2 vUV;

uniform sampler2D uTexture;

out vec4 FragColor;

void main() {
    FragColor = texture(uTexture, vUV);
}
";
    }
}
