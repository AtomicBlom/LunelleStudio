// Bloom's way back up (Glow.cs): each smaller step spread by a 3×3 tent and added onto the larger one
// (the pass blends additively), so the glow gathers every size from tight round a highlight to wide across the frame.

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

struct Up { float4 size; };   // xy: the source's size in pixels
KALDRA_PUSH_CONSTANT Up pc;

[shader("fragment")]
float4 fragmentMain(VsOut input) : SV_Target
{
	float2 t = 1.0 / pc.size.xy;
	float3 c = source.Sample(input.uv).rgb * 4.0;
	c += (source.Sample(input.uv + float2(t.x, 0)).rgb + source.Sample(input.uv - float2(t.x, 0)).rgb
	    + source.Sample(input.uv + float2(0, t.y)).rgb + source.Sample(input.uv - float2(0, t.y)).rgb) * 2.0;
	c += source.Sample(input.uv + t).rgb + source.Sample(input.uv - t).rgb
	   + source.Sample(input.uv + float2(t.x, -t.y)).rgb + source.Sample(input.uv + float2(-t.x, t.y)).rgb;
	return float4(c / 16.0, 1.0);
}
