using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class FinalPass
{
	static readonly ProfilingSampler sampler = new("Final");

	CameraRendererCopier copier;

	TextureHandle colorAttachment;

	void Render(UnsafeGraphContext context)
	{
		UnsafeCommandBuffer buffer = context.cmd;
		copier.CopyToCameraTarget(buffer, colorAttachment);
	}

	public static void Record(
		RenderGraph renderGraph,
		CameraRendererCopier copier,
		in CameraRendererTextures textures)
	{
		using var builder = renderGraph.AddUnsafePass(
			sampler.name, out FinalPass pass, sampler);
		pass.copier = copier;
		pass.colorAttachment = textures.colorAttachment;
		builder.UseTexture(pass.colorAttachment);
		builder.SetRenderFunc<FinalPass>(
			static (pass, context) => pass.Render(context));
	}
}
