using UnityEngine.Rendering.RenderGraphModule;

public readonly ref struct ShadowResources
{
	public readonly TextureHandle directionalAtlas, otherAtlas;

	public readonly BufferHandle
		directionalShadowCascadesBuffer,
		directionalShadowMatricesBuffer,
		otherShadowDataBuffer;

	public ShadowResources(
		TextureHandle directionalAtlas,
		TextureHandle otherAtlas,
		BufferHandle directionalShadowCascadesBuffer,
		BufferHandle directionalShadowMatricesBuffer,
		BufferHandle otherShadowDataBuffer)
	{
		this.directionalAtlas = directionalAtlas;
		this.otherAtlas = otherAtlas;
		this.directionalShadowCascadesBuffer = directionalShadowCascadesBuffer;
		this.directionalShadowMatricesBuffer = directionalShadowMatricesBuffer;
		this.otherShadowDataBuffer = otherShadowDataBuffer;
	}
}
