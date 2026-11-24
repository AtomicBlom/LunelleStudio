// Bloom's way down (Glow.cs): each step half the size of the one above, its four bilinear taps covering a
// 4×4 block of it, so the glow widens without aliasing.

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

struct Down { float4 size; };   // xy: the source's size in pixels
KALDRA_PUSH_CONSTANT Down pc;

[shader("fragment")]
float4 fragmentMain(VsOut input) : SV_Target
{
	float2 t = 1.0 / pc.size.xy;
	float3 c = source.Sample(input.uv + float2(-t.x, -t.y)).rgb + source.Sample(input.uv + float2(t.x, -t.y)).rgb
	         + source.Sample(input.uv + float2(-t.x, t.y)).rgb + source.Sample(input.uv + float2(t.x, t.y)).rgb;
	return float4(c * 0.25, 1.0);
}
