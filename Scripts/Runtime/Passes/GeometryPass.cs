using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
	static readonly ProfilingSampler
		samplerOpaque = new("Opaque Geometry"),
		samplerTransparent = new("Transparent Geometry");

	static readonly ShaderTagId[] shaderTagIDs = {
		new("SRPDefaultUnlit"),
		new("CustomLit")
	};

	RendererListHandle list;

	void Render(UnsafeGraphContext context)
	{
		context.cmd.DrawRendererList(list);
	}

	public static void Record(
		RenderGraph renderGraph,
		Camera camera,
		CullingResults cullingResults,
		uint renderingLayerMask,
		bool opaque,
		in CameraRendererTextures textures,
		in LightResources lightData)
	{
		ProfilingSampler sampler = opaque ? samplerOpaque : samplerTransparent;

		using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
			sampler.name, out GeometryPass pass, sampler);

		pass.list = renderGraph.CreateRendererList(
			new RendererListDesc(shaderTagIDs, cullingResults, camera)
			{
				sortingCriteria = opaque ?
					SortingCriteria.CommonOpaque :
					SortingCriteria.CommonTransparent,
				rendererConfiguration =
					PerObjectData.ReflectionProbes |
					PerObjectData.Lightmaps |
					PerObjectData.ShadowMask |
					PerObjectData.LightProbe |
					PerObjectData.OcclusionProbe |
					PerObjectData.LightProbeProxyVolume |
					PerObjectData.OcclusionProbeProxyVolume,
				renderQueueRange = opaque ?
					RenderQueueRange.opaque : RenderQueueRange.transparent,
				renderingLayerMask = renderingLayerMask
			});
		
		builder.UseRendererList(pass.list);
		builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
		builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);

		if (!opaque)
		{
			if (textures.colorCopy.IsValid())
			{
				builder.UseTexture(textures.colorCopy);
			}
			if (textures.depthCopy.IsValid())
			{
				builder.UseTexture(textures.depthCopy);
			}
		}
		
		builder.UseBuffer(lightData.directionalLightDataBuffer);
		builder.UseBuffer(lightData.otherLightDataBuffer);
		builder.UseBuffer(lightData.tilesBuffer);
		builder.UseTexture(lightData.shadowResources.directionalAtlas);
		builder.UseTexture(lightData.shadowResources.otherAtlas);
		builder.UseBuffer(
			lightData.shadowResources.directionalShadowCascadesBuffer);
		builder.UseBuffer(
			lightData.shadowResources.directionalShadowMatricesBuffer);
		builder.UseBuffer(lightData.shadowResources.otherShadowDataBuffer);

		builder.SetRenderFunc<GeometryPass>(
			static (pass, context) => pass.Render(context));
	}
}
