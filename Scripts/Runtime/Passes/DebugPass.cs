using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class DebugPass
{
	static readonly ProfilingSampler sampler = new("Debug");

	[Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
	public static void Record(
		RenderGraph renderGraph,
		CustomRenderPipelineSettings settings,
		Camera camera,
		in LightResources lightData)
	{
		if (CameraDebugger.IsActive &&
			camera.cameraType <= CameraType.SceneView)
		{
			using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
				sampler.name, out DebugPass pass, sampler);
			builder.UseBuffer(lightData.tilesBuffer);
			builder.SetRenderFunc<DebugPass>(
				static (pass, context) => CameraDebugger.Render(context));
		}
	}
}
