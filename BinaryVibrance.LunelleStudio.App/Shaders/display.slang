// The window as shown (Glow.cs): the workbench, drawn in linear light into a half-float target, with its glow added. The
// screen's sRGB format encodes it. What the workbench draws no brighter than white shows exactly as drawn; a lamp
// drawn brighter glows round itself, and past white it clips to its own colour's brightest.

#include "kaldra_platform.module.slang"

Sampler2D scene;
Sampler2D bloom;   // the glow's widest gathering, at half the window's size

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

struct Display { float4 glow; };   // x: how much of the glow is added (the theme's), 0 for none
KALDRA_PUSH_CONSTANT Display pc;

[shader("fragment")]
float4 fragmentMain(VsOut input) : SV_Target
{
	float3 c = scene.Sample(input.uv).rgb;
	if (pc.glow.x > 0.0)
		c += bloom.Sample(input.uv).rgb * pc.glow.x;
	return float4(c, 1.0);
}
