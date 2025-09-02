using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;

public class UnsupportedShadersPass
{
#if UNITY_EDITOR
	static readonly ProfilingSampler sampler = new("Unsupported Shaders");

	static readonly ShaderTagId[] shaderTagIDs = {
		new("Always"),
		new("ForwardBase"),
		new("PrepassBase"),
		new("Vertex"),
		new("VertexLMRGBM"),
		new("VertexLM")
	};

	static Material errorMaterial;

	RendererListHandle list;

	void Render(UnsafeGraphContext context)
	{
		context.cmd.DrawRendererList(list);
	}
#endif

	[Conditional("UNITY_EDITOR")]
	public static void Record(
		RenderGraph renderGraph,
		Camera camera,
		CullingResults cullingResults,
		in CameraRendererTextures textures)
	{
#if UNITY_EDITOR
		using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
			sampler.name, out UnsupportedShadersPass pass, sampler);

		if (errorMaterial == null)
		{
			errorMaterial = new(Shader.Find("Hidden/InternalErrorShader"));
		}

		pass.list = renderGraph.CreateRendererList(
			new RendererListDesc(shaderTagIDs, cullingResults, camera)
			{
				overrideMaterial = errorMaterial,
				renderQueueRange = RenderQueueRange.all
			});
		builder.UseRendererList(pass.list);
		builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
		builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);

		builder.SetRenderFunc<UnsupportedShadersPass>(
			static (pass, context) => pass.Render(context));
#endif
	}
}
