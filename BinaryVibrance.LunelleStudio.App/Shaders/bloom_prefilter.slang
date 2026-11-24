// The glow's first step (Glow.cs): the light brighter than white, at half the window's size. A soft knee round the
// threshold, so a lamp eases into its glow rather than snapping on; four taps averaged as it halves, each weighted by
// 1 / (1 + its brightness) (Karis's average), so a lit pixel or two neither blooms nor flickers as it moves, while a lamp
// or a lit line still glows. Anything not finite counts as dark.

#include "kaldra_platform.module.slang"

Sampler2D source;

struct VsOut
{
	float4 position : SV_Position;
	float2 uv : TEXCOORD0;
};

[shader("vertex")]
VsOut vertexMain(uint vertexId : SV_VertexID)
{
	float2 uv = float2((vertexId << 1) & 2, vertexId & 2);
	VsOut output;
	output.position = KALDRA_FULLSCREEN_POS(uv);
	output.uv = uv;
	return output;
}

struct Prefilter { float4 knee; };   // x: threshold; y: the knee's half width
KALDRA_PUSH_CONSTANT Prefilter pc;

float3 Bright(float3 c)
{
	float brightness = max(c.r, max(c.g, c.b));
	float soft = clamp(brightness - pc.knee.x + pc.knee.y, 0.0, 2.0 * pc.knee.y);
	soft = soft * soft / (4.0 * pc.knee.y + 1e-4);
	float over = max(soft, brightness - pc.knee.x);
	return c * (over / max(brightness, 1e-4));
}

// One tap, bright past the knee, with its Karis weight
float4 Tap(float2 uv)
{
	float3 c = source.Sample(uv).rgb;
	if (any(isnan(c)) || any(isinf(c))) c = float3(0.0, 0.0, 0.0);
	float3 b = Bright(c);
	float w = 1.0 / (1.0 + max(b.r, max(b.g, b.b)));
	return float4(b * w, w);
}

[shader("fragment")]
float4 fragmentMain(VsOut input) : SV_Target
{
	float2 texel = 0.5 / float2(pc.knee.zw);
	float4 sum = Tap(input.uv + float2(-texel.x, -texel.y)) + Tap(input.uv + float2(texel.x, -texel.y))
	           + Tap(input.uv + float2(-texel.x, texel.y)) + Tap(input.uv + float2(texel.x, texel.y));
	return float4(sum.rgb / max(sum.a, 1e-4), 1.0);
}
