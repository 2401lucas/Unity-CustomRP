using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

using static Unity.Mathematics.math;

public partial class LightingPass
{
	static readonly ProfilingSampler sampler = new("Lighting");

	const int
		maxDirectionalLightCount = 4,
		maxOtherLightCount = 128;

	static readonly int
		directionalLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
		directionalLightDataId = Shader.PropertyToID("_DirectionalLightData"),
		otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
		otherLightDataId = Shader.PropertyToID("_OtherLightData"),
		tilesId = Shader.PropertyToID("_ForwardPlusTiles"),
		tileSettingsId = Shader.PropertyToID("_ForwardPlusTileSettings");

	static readonly DirectionalLightData[] directionalLightData =
		new DirectionalLightData[maxDirectionalLightCount];

	static readonly OtherLightData[] otherLightData =
		new OtherLightData[maxOtherLightCount];

	BufferHandle
		directionalLightDataBuffer, otherLightDataBuffer, tilesBuffer;

	CullingResults cullingResults;

	readonly Shadows shadows = new();

	int directionalLightCount, otherLightCount;

	NativeArray<float4> lightBounds;

	static NativeArray<int> tileData;

	JobHandle forwardPlusJobHandle;

	Vector2 screenUVToTileCoordinates;

	Vector2Int tileCount;

	int maxLightsPerTile, tileDataSize, maxTileDataSize;

	int TileCount => tileCount.x * tileCount.y;

	void Setup(
		CullingResults cullingResults,
		Vector2Int attachmentSize,
		ForwardPlusSettings forwardPlusSettings,
		ShadowSettings shadowSettings,
		int renderingLayerMask)
	{
		this.cullingResults = cullingResults;
		shadows.Setup(cullingResults, shadowSettings);

		maxLightsPerTile = forwardPlusSettings.maxLightsPerTile <= 0 ?
			31 : forwardPlusSettings.maxLightsPerTile;
		maxTileDataSize = maxLightsPerTile + 1;
		lightBounds = new NativeArray<float4>(
			maxOtherLightCount, Allocator.TempJob,
			NativeArrayOptions.UninitializedMemory);
		float tileScreenPixelSize = forwardPlusSettings.tileSize <= 0 ?
			64f : (float)forwardPlusSettings.tileSize;
		screenUVToTileCoordinates.x =
			attachmentSize.x / tileScreenPixelSize;
		screenUVToTileCoordinates.y =
			attachmentSize.y / tileScreenPixelSize;
		tileCount.x = Mathf.CeilToInt(screenUVToTileCoordinates.x);
		tileCount.y = Mathf.CeilToInt(screenUVToTileCoordinates.y);

		SetupLights(renderingLayerMask);
	}

	void SetupForwardPlus(int lightIndex, ref VisibleLight visibleLight)
	{
		Rect r = visibleLight.screenRect;
		lightBounds[lightIndex] = float4(r.xMin, r.yMin, r.xMax, r.yMax);
	}

	void SetupLights(int renderingLayerMask)
	{
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
		int requiredMaxLightsPerTile = Mathf.Min(
			maxLightsPerTile, visibleLights.Length);
		tileDataSize = requiredMaxLightsPerTile + 1;
		int i;
		directionalLightCount = otherLightCount = 0;
		for (i = 0; i < visibleLights.Length; i++)
		{
			VisibleLight visibleLight = visibleLights[i];
			Light light = visibleLight.light;
			if ((light.renderingLayerMask & renderingLayerMask) != 0)
			{
				switch (visibleLight.lightType)
				{
					case LightType.Directional:
						if (directionalLightCount < maxDirectionalLightCount)
						{
							directionalLightData[directionalLightCount++] =
								new DirectionalLightData(
									ref visibleLight, light,
									shadows.ReserveDirectionalShadows(
										light, i));
						}
						break;
					case LightType.Point:
						if (otherLightCount < maxOtherLightCount)
						{
							SetupForwardPlus(otherLightCount, ref visibleLight);
							otherLightData[otherLightCount++] =
								OtherLightData.CreatePointLight(
									ref visibleLight, light,
									shadows.ReserveOtherShadows(light, i));
						}
						break;
					case LightType.Spot:
						if (otherLightCount < maxOtherLightCount)
						{
							SetupForwardPlus(otherLightCount, ref visibleLight);
							otherLightData[otherLightCount++] =
								OtherLightData.CreateSpotLight(
									ref visibleLight, light,
									shadows.ReserveOtherShadows(light, i));
						}
						break;
				}
			}
		}

		int tileDataLength = TileCount * tileDataSize;
		if (!tileData.IsCreated || tileData.Length != tileDataLength)
		{
			if (tileData.IsCreated)
			{
				tileData.Dispose();
			}
			tileData = new(tileDataLength, Allocator.Persistent,
				NativeArrayOptions.UninitializedMemory);
		}
		forwardPlusJobHandle = new ForwardPlusTilesJob
		{
			lightBounds = lightBounds,
			tileData = tileData,
			otherLightCount = otherLightCount,
			tileScreenUVSize = float2(
				1f / screenUVToTileCoordinates.x,
				1f / screenUVToTileCoordinates.y),
			maxLightsPerTile = requiredMaxLightsPerTile,
			tilesPerRow = tileCount.x,
			tileDataSize = tileDataSize
		}.ScheduleParallel(TileCount, tileCount.x, default);
	}

	void Render(UnsafeGraphContext context)
	{
		UnsafeCommandBuffer buffer = context.cmd;
		buffer.SetGlobalInt(directionalLightCountId, directionalLightCount);
		buffer.SetBufferData(
			directionalLightDataBuffer, directionalLightData,
			0, 0, directionalLightCount);
		buffer.SetGlobalBuffer(
			directionalLightDataId, directionalLightDataBuffer);

		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		buffer.SetBufferData(
			otherLightDataBuffer, otherLightData, 0, 0, otherLightCount);
		buffer.SetGlobalBuffer(otherLightDataId, otherLightDataBuffer);

		shadows.Render(context.cmd);

		forwardPlusJobHandle.Complete();
		buffer.SetBufferData(
			tilesBuffer, tileData, 0, 0, tileData.Length);
		buffer.SetGlobalBuffer(tilesId, tilesBuffer);
		buffer.SetGlobalVector(tileSettingsId, new Vector4(
			screenUVToTileCoordinates.x, screenUVToTileCoordinates.y,
			tileCount.x.ReinterpretAsFloat(),
			tileDataSize.ReinterpretAsFloat()));
		lightBounds.Dispose();
	}

	public static LightResources Record(
		RenderGraph renderGraph,
		CullingResults cullingResults,
		Vector2Int attachmentSize,
		ForwardPlusSettings forwardPlusSettings,
		ShadowSettings shadowSettings,
		int renderingLayerMask,
		ScriptableRenderContext context)
	{
		using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
			sampler.name, out LightingPass pass, sampler);
		pass.Setup(cullingResults, attachmentSize,
			forwardPlusSettings, shadowSettings, renderingLayerMask);

		pass.directionalLightDataBuffer = renderGraph.CreateBuffer(
			new BufferDesc(
				maxDirectionalLightCount, DirectionalLightData.stride)
			{
				name = "Directional Light Data"
			});
		builder.UseBuffer(
			pass.directionalLightDataBuffer, AccessFlags.WriteAll);
		pass.otherLightDataBuffer = renderGraph.CreateBuffer(new BufferDesc(
			maxOtherLightCount, OtherLightData.stride)
		{
			name = "Other Light Data"
		});
		builder.UseBuffer(pass.otherLightDataBuffer, AccessFlags.WriteAll);
		pass.tilesBuffer = renderGraph.CreateBuffer(new BufferDesc(
			pass.TileCount * pass.maxTileDataSize, 4
		)
		{
			name = "Forward+ Tiles"
		});
		builder.UseBuffer(pass.tilesBuffer, AccessFlags.WriteAll);
		builder.SetRenderFunc<LightingPass>(
			static (pass, context) => pass.Render(context));
		builder.AllowPassCulling(false);
		return new LightResources(
			pass.directionalLightDataBuffer,
			pass.otherLightDataBuffer,
			pass.tilesBuffer,
			pass.shadows.GetResources(renderGraph, builder, context));
	}

	public static void Cleanup() => tileData.Dispose();
}
