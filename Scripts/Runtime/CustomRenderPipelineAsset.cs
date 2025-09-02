using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
	[SerializeField]
	CustomRenderPipelineSettings settings;

	public override Type pipelineType => typeof(CustomRenderPipeline);

	public override string renderPipelineShaderTag => string.Empty;

	protected override RenderPipeline CreatePipeline() =>
		new CustomRenderPipeline(settings);
}
