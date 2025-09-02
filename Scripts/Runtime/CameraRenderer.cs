using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class CameraRenderer
{
    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;
    static readonly CameraSettings defaultCameraSettings = new();
    readonly PostFXStack postFXStack = new();
    readonly Material material;

    public CameraRenderer(Shader shader, Shader cameraDebuggerShader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        CameraDebugger.Initialize(cameraDebuggerShader);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CameraDebugger.Cleanup();
        LightingPass.Cleanup();
    }

    public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera camera,
                       CustomRenderPipelineSettings settings)
    {
        CameraBufferSettings bufferSettings = settings.cameraBuffer;
        PostFXSettings postFXSettings = settings.postFXSettings;
        ShadowSettings shadowSettings = settings.shadows;

        ProfilingSampler cameraSampler;
        CameraSettings cameraSettings;

        if (camera.TryGetComponent(out CustomRenderPipelineCamera crpCamera))
        {
            cameraSampler = crpCamera.Sampler;
            cameraSettings = crpCamera.Settings;
        }
        else
        {
            cameraSampler = GetDefaultProfileSampler(camera);
            cameraSettings = defaultCameraSettings;
        }

        bool useColorTexture, useDepthTexture;
        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = bufferSettings.copyColorReflection;
            useDepthTexture = bufferSettings.copyDepthReflection;
        }
        else
        {
            useColorTexture =
                bufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture =
                bufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        bool hasActivePostFX =
            postFXSettings != null && PostFXSettings.AreApplicableTo(camera);

        float renderScale = cameraSettings.GetRenderScale(
            bufferSettings.renderScale);
        bool useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            useScaledRendering = false;
        }
#endif

        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
        {
            return;
        }

        scriptableCullingParameters.shadowDistance =
            Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
        CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);

        bufferSettings.allowHDR &= camera.allowHDR;
        Vector2Int bufferSize = default;
        if (useScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }

        bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;

        var renderGraphParameters = new RenderGraphParameters
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = cameraSampler.name,
            rendererListCulling = true,
            scriptableRenderContext = context
        };
        renderGraph.BeginRecording(renderGraphParameters);
        using (new RenderGraphProfilingScope(renderGraph, cameraSampler))
        {
            LightResources lightResources = LightingPass.Record(
                renderGraph, cullingResults, bufferSize,
                settings.forwardPlus, shadowSettings,
                cameraSettings.maskLights ? cameraSettings.newRenderingLayerMask : -1,
                context);

            CameraRendererTextures textures = SetupPass.Record(
                renderGraph, useColorTexture, useDepthTexture,
                bufferSettings.allowHDR, bufferSize, camera);

            GeometryPass.Record(
                renderGraph, camera, cullingResults,
                cameraSettings.newRenderingLayerMask, true,
                textures, lightResources);

            SkyboxPass.Record(renderGraph, camera, textures);

            var copier = new CameraRendererCopier(
                material, camera, cameraSettings.finalBlendMode);
            CopyAttachmentsPass.Record(
                renderGraph, useColorTexture, useDepthTexture,
                copier, textures);

            GeometryPass.Record(
                renderGraph, camera, cullingResults,
                cameraSettings.newRenderingLayerMask, false,
                textures, lightResources);

            //Editor Only
            UnsupportedShadersPass.Record(
                renderGraph, camera, cullingResults, textures);

            if (hasActivePostFX)
            {
                postFXStack.BufferSettings = bufferSettings;
                postFXStack.BufferSize = bufferSize;
                postFXStack.Camera = camera;
                postFXStack.FinalBlendMode = cameraSettings.finalBlendMode;
                postFXStack.Settings = postFXSettings;
                PostFXPass.Record(
                    renderGraph, postFXStack, (int)settings.colorLUTResolution,
                    cameraSettings.keepAlpha, textures);
            }
            else
            {
                FinalPass.Record(renderGraph, copier, textures);
            }

            DebugPass.Record(renderGraph, settings, camera, lightResources);
            GizmosPass.Record(renderGraph, copier, textures);
        }

        renderGraph.EndRecordingAndExecute();
        context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
        context.Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);
    }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD)
    static ProfilingSampler GetDefaultProfileSampler(Camera camera) =>
        ProfilingSampler.Get(camera.cameraType);
#else
	static ProfilingSampler defaultReleaseBuildProfilingSampler =
		new("Other Camera");

	static ProfilingSampler GetDefaultProfileSampler(Camera camera) =>
		defaultReleaseBuildProfilingSampler;
#endif
}