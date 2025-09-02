using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class CopyAttachmentsPass
{
	static readonly ProfilingSampler sampler = new("Copy Attachments");

	static readonly int
		colorCopyID = Shader.PropertyToID("_CameraColorTexture"),
		depthCopyID = Shader.PropertyToID("_CameraDepthTexture");

	bool copyColor, copyDepth;

	CameraRendererCopier copier;

	TextureHandle colorAttachment, depthAttachment, colorCopy, depthCopy;

	void Render(UnsafeGraphContext context)
	{
		UnsafeCommandBuffer buffer = context.cmd;
		if (copyColor)
		{
			copier.Copy(buffer, colorAttachment, colorCopy, false);
			buffer.SetGlobalTexture(colorCopyID, colorCopy);
		}
		if (copyDepth)
		{
			copier.Copy(buffer, depthAttachment, depthCopy, true);
			buffer.SetGlobalTexture(depthCopyID, depthCopy);
		}
		buffer.SetRenderTarget(
			colorAttachment,
			RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
			depthAttachment,
			RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
		);
	}

	public static void Record(
		RenderGraph renderGraph,
		bool copyColor,
		bool copyDepth,
		CameraRendererCopier copier,
		in CameraRendererTextures textures)
	{
		if (copyColor || copyDepth)
		{
			using var builder = renderGraph.AddUnsafePass(
				sampler.name, out CopyAttachmentsPass pass, sampler);

			pass.copyColor = copyColor;
			pass.copyDepth = copyDepth;
			pass.copier = copier;
			
			pass.colorAttachment = textures.colorAttachment;
			builder.UseTexture(pass.colorAttachment);
			pass.depthAttachment = textures.depthAttachment;
			builder.UseTexture(pass.depthAttachment);
			if (copyColor)
			{
				pass.colorCopy = textures.colorCopy;
				builder.UseTexture(pass.colorCopy, AccessFlags.WriteAll);
			}
			if (copyDepth)
			{
				pass.depthCopy = textures.depthCopy;
				builder.UseTexture(pass.depthCopy, AccessFlags.WriteAll);
			}
			builder.AllowPassCulling(true);
			builder.SetRenderFunc<CopyAttachmentsPass>(
				static (pass, context) => pass.Render(context));
		}
	}
}
