// The workbench's backdrop: one triangle covering the screen, shaded from a deep blue at the top to violet at the bottom.

#include "kaldra_platform.module.slang"

struct VsOut
{
	float4 position : SV_Position;
	float2 uv : TEXCOORD0;
};

[shader("vertex")]
VsOut vertexMain(uint vertexId : SV_VertexID)
{
	// (0,0), (2,0), (0,2): a triangle whose middle covers the unit square, so the screen is covered by one draw.
	float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

	VsOut output;
	output.uv = uv;
	// uv.y runs down the screen: KALDRA_CLIP_POS takes the target's NDC-Y convention into account.
	output.position = KALDRA_CLIP_POS(uv * 2.0 - 1.0);
	return output;
}

[shader("fragment")]
float4 fragmentMain(VsOut input) : SV_Target
{
	float3 top = float3(0.02, 0.04, 0.10);
	float3 bottom = float3(0.12, 0.05, 0.18);
	return float4(lerp(top, bottom, saturate(input.uv.y)), 1.0);
}
