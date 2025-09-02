using UnityEngine.Rendering.RenderGraphModule;

public readonly ref struct LightResources
{
	public readonly BufferHandle
		directionalLightDataBuffer, otherLightDataBuffer, tilesBuffer;
	
	public readonly ShadowResources shadowResources;

	public LightResources(
		BufferHandle directionalLightDataBuffer,
		BufferHandle otherLightDataBuffer,
		BufferHandle tilesBuffer,
		ShadowResources shadowResources)
	{
		this.directionalLightDataBuffer = directionalLightDataBuffer;
		this.otherLightDataBuffer = otherLightDataBuffer;
		this.tilesBuffer = tilesBuffer;
		this.shadowResources = shadowResources;
	}
}
