using System.Diagnostics;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class GizmosPass
{
#if UNITY_EDITOR
	static readonly ProfilingSampler sampler = new("Gizmos");

	CameraRendererCopier copier;

	TextureHandle depthAttachment, cameraTarget;

	RendererListHandle preList, postList;

	void Render(UnsafeGraphContext context)
	{
		UnsafeCommandBuffer buffer = context.cmd;
		copier.Copy(buffer, depthAttachment, cameraTarget, true);
		buffer.DrawRendererList(preList);
		buffer.DrawRendererList(postList);
	}
#endif

	[Conditional("UNITY_EDITOR")]
	public static void Record(
		RenderGraph renderGraph,
		CameraRendererCopier copier,
		in CameraRendererTextures textures)
	{
#if UNITY_EDITOR
		if (Handles.ShouldRenderGizmos())
		{
			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
				sampler.name, out GizmosPass pass, sampler);
			pass.copier = copier;
			pass.depthAttachment = textures.depthAttachment;
			builder.UseTexture(pass.depthAttachment);
			pass.preList = renderGraph.CreateGizmoRendererList(
				copier.Camera, GizmoSubset.PreImageEffects);
			builder.UseRendererList(pass.preList);
			pass.postList = renderGraph.CreateGizmoRendererList(
				copier.Camera, GizmoSubset.PostImageEffects);
			builder.UseRendererList(pass.postList);
			pass.cameraTarget = renderGraph.ImportBackbuffer(
				BuiltinRenderTextureType.CameraTarget);
			builder.UseTexture(pass.cameraTarget, AccessFlags.WriteAll);
			builder.AllowPassCulling(false);
			builder.SetRenderFunc<GizmosPass>(
				static (pass, context) => pass.Render(context));
		}
#endif
	}
}
