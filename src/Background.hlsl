cbuffer Settings : register(b0) {
    float4 screen; // width, height, motion time, style (0 diffuse, 1 stars, 2 glass)
    float4 options; // softness, brightness, glass opacity, star size
    float4 color1; // rgb: first color; w: active diffuse/glass color count
    float4 color2;
    float4 color3;
    float4 starOptions; // shape, motion direction (+1 outward / -1 inward), length at 1080p, particle count
    float4 color4;
    float4 starDistribution; // three cumulative color counts, coprime permutation stride
    float4 endColor1; // rgb: secondary color; w: gradient enabled
    float4 endColor2;
    float4 endColor3;
    float4 endColor4;
};
struct Vertex { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 tint : COLOR0; float2 trail : TEXCOORD1; float4 endTint : COLOR1; };
Vertex Fullscreen(uint id : SV_VertexID) {
    Vertex o;
    o.uv = float2((id << 1) & 2, id & 2);
    o.position = float4(o.uv * float2(2,-2) + float2(-1,1), 0, 1);
    o.tint = 1; o.trail = 0; o.endTint = 0; return o;
}
float hash(float n) { return frac(sin(n * 12.9898 + 78.233) * 43758.5453); }
float4 Field(Vertex input) : SV_TARGET {
    float2 uv = input.uv;
    float t = screen.z * .19;
    float3 colors[4] = {color1.rgb, color2.rgb, color3.rgb, color4.rgb};
    int count = clamp((int)color1.w, 1, 4);
    // Preserve the original six fields for existing palettes; a fourth color
    // gets two fields as well, without increasing overall mixing strength.
    int fields = count == 4 ? 8 : 6;
    float3 sum = 0;
    float weight = 0;
    [unroll] for (int i = 0; i < 8; i++) {
        if (i >= fields) break;
        float phase = i * 2.39996;
        float2 center = .5 + float2(.46 * sin(t * (.62 + i * .067) + phase),
                                    .48 * cos(t * (.48 + i * .081) + phase * 1.7));
        float2 delta = uv - center;
        delta.x *= 1.3;
        float angle = t * .17 + phase;
        delta = float2(delta.x * cos(angle) - delta.y * sin(angle), delta.x * sin(angle) + delta.y * cos(angle));
        delta.y *= 1.3 + .3 * sin(t * .4 + phase);
        float spread = lerp(22, 5, options.x);
        float w = exp2(-dot(delta, delta) * spread) * (.7 + .3 * sin(t * .31 + phase));
        sum += colors[i % count] * w; weight += w;
    }
    sum *= 6.0 / fields; weight *= 6.0 / fields;
    float3 c;
    if (screen.w > 1.5) {
        c = lerp(float3(.80,.84,.91), sum / max(.001, weight), saturate(weight * .75));
        c = lerp(c, float3(.96,.98,1), options.z);
    } else {
        c = lerp(float3(.024,.029,.038), sum / max(.001, weight), saturate(weight * .58));
    }
    // Spatial dither avoids visible bands without animated grain/flicker.
    float noise = frac(52.9829189 * frac(dot(input.position.xy, float2(.06711056,.00583715)))) - .5;
    return float4(saturate(c * options.y + noise / 255), 1);
}
Vertex StarVertex(uint vertexId : SV_VertexID, uint instance : SV_InstanceID) {
    Vertex o;
    float seed = instance + 1;
    float angle = hash(seed) * 6.2831853;
    float radius = .08 + sqrt(hash(seed + 123)) * 1.12;
    float z = 1 - frac(hash(seed + 345) + screen.z * (.11 + .1 * hash(seed + 432)));
    float2 ray = float2(cos(angle), sin(angle));
    // Dot and trail heads share this exact trajectory, lifetime and depth.
    // Shape changes only the geometry behind the head, never the star field.
    float2 center = ray * radius / (.12 + z * 2.7);
    center.x *= screen.y / screen.x;
    float2 corners[6] = {float2(-1,-1), float2(1,-1), float2(-1,1),float2(-1,1),float2(1,-1),float2(1,1)};
    float2 corner = corners[vertexId];
    float pixelRadius = options.w * (.35 + 2.5 * pow(1-z, 3)) * (.7 + .8 * hash(seed + 625)) * screen.y / 1080;
    pixelRadius = max(.6, pixelRadius);
    o.position = float4(center + corner * (pixelRadius * 2) / screen.xy, 0, 1);
    o.uv = corner;
    o.trail = 0;
    if (starOptions.x > .5) {
        // Work in physical pixel coordinates so direction, caps and width stay
        // circular on wide, portrait and high-DPI outputs. The head retains the
        // original star position; the tail always points opposite the motion.
        // Streaks need less perspective widening than dots. Keep the user's
        // width multiplier, but prevent near stars from becoming solid rods.
        pixelRadius = max(.45, options.w * (.35 + 1.6 * pow(1-z, 3))
            * (.75 + .5 * hash(seed + 625)) * screen.y / 1080);
        float2 head = center * screen.xy * .5;
        // Trace this particle's recent displacement. Clamp its earlier depth
        // to the current lifetime so a wrap cannot draw a screen-length flash.
        float rate = .11 + .1 * hash(seed + 432);
        float previousZ = clamp(z + (screen.z - color4.w) * rate, 0, 1);
        float2 previous = ray * radius / (.12 + previousZ * 2.7) * screen.y * .5;
        float2 displacement = head - previous;
        float traveled = length(displacement);
        float2 direction = traveled > .0001 ? displacement / traveled : ray;
        float2 normal = float2(-direction.y, direction.x);
        float trailLength = min(traveled, starOptions.z * screen.y / 1080);
        float halfLength = trailLength * .5;
        // Local halo fits inside the same quad; no blur surface/history frames.
        float support = pixelRadius * 2.25;
        float2 position = head + direction * (corner.x * (halfLength + support) - halfLength)
            + normal * corner.y * support;
        o.position.xy = position * 2 / screen.xy;
        o.uv = corner * float2((halfLength + support) / pixelRadius, 2.25);
        o.trail.x = halfLength / pixelRadius;
    }
    float fade = smoothstep(1,.85,z) * smoothstep(0,.08,z);
    float light = lerp(.55, 1, 1-z) * fade * options.y;
    if (starOptions.x > .5)
        light *= lerp(.65, 1, sqrt(1-z)) * lerp(.8, 1, hash(seed + 2031));
    uint count = max(1u, (uint)starOptions.w);
    uint rank = (instance * (uint)starDistribution.w + 37u) % count;
    float3 tint = rank < (uint)starDistribution.x ? color1.rgb
        : rank < (uint)starDistribution.y ? color2.rgb
        : rank < (uint)starDistribution.z ? color3.rgb : color4.rgb;
    o.tint = float4(tint * light, 1);
    float4 end = rank < (uint)starDistribution.x ? endColor1
        : rank < (uint)starDistribution.y ? endColor2
        : rank < (uint)starDistribution.z ? endColor3 : endColor4;
    o.endTint = float4(end.rgb * light, end.w);
    return o;
}
float4 StarPixel(Vertex input) : SV_TARGET {
    if (starOptions.x > .5) {
        // The zero-length case becomes a symmetric star instead of a half-lit
        // sliver. This also keeps slow movement and direction changes continuous.
        float streak = smoothstep(0, 2, input.trail.x);
        float along = saturate((input.uv.x + input.trail.x) / max(.001, 2 * input.trail.x));
        // Most of the body stays cylindrical; only the tail gently narrows.
        // All layers fade to zero, so a dim round end never remains behind.
        float width = lerp(1, lerp(.28, 1, smoothstep(0, .8, along)), streak);
        float cap = max(abs(input.uv.x) - input.trail.x, 0);
        float radiusSquared = (cap * cap + input.uv.y * input.uv.y) / (width * width);
        float longitudinal = lerp(1, pow(smoothstep(0, 1, along), .75), streak);
        // Approximate pixel coverage for subpixel cores. Compensating the
        // amplitude preserves energy instead of thickening distant stars.
        float footprint = length(float2(ddx(input.uv.y), ddy(input.uv.y))) / width;
        float coreVariance = 1 + 2 * footprint * footprint;
        float core = exp2(-10 * radiusSquared / coreVariance) / sqrt(coreVariance);
        float body = exp2(-2.7 * radiusSquared);
        float halo = exp2(-.9 * radiusSquared) * (1 - smoothstep(1.5, 2.25, sqrt(radiusSquared)));
        float3 tint = input.tint.rgb;
        // Start color at the bright head, end color toward the fading tail.
        if (input.endTint.w > .5)
            tint = lerp(tint, input.endTint.rgb, (1 - along) * streak);
        float peak = max(tint.r, max(tint.g, tint.b));
        float3 coreTint = lerp(tint, float3(peak, peak, peak), .65);
        float3 emission = (coreTint * (.85 * core) + tint * (.4 * body + .06 * halo)) * longitudinal;
        return float4(emission, 0);
    }
    float r = length(input.uv);
    float alpha = 1 - smoothstep(.4, 1, r);
    float3 dotTint = input.tint.rgb;
    // Dots use the same pair across their face, independent of lifetime/speed.
    if (input.endTint.w > .5)
        dotTint = lerp(dotTint, input.endTint.rgb, saturate(input.uv.x * .5 + .5));
    return float4(dotTint * alpha, alpha);
}
