#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight(Surface surface, Light light)
{
    return
        saturate(dot(surface.normal, light.direction) * light.attenuation) *
        light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

bool RenderingLayersOverlap(Surface surface, Light light)
{
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

float3 GetLighting(Fragment fragment, Surface surfaceWS, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;

    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        if (RenderingLayersOverlap(surfaceWS, light))
        {
            color += GetLighting(surfaceWS, brdf, light);
        }
    }

    ForwardPlusTile tile = GetForwardPlusTile(fragment.screenUV);
    int lastLightIndex = tile.GetLastLightIndexInTile();
    for (int j = tile.GetFirstLightIndexInTile(); j <= lastLightIndex; j++)
    {
        Light light = GetOtherLight(
            tile.GetLightIndex(j), surfaceWS, shadowData);
        if (RenderingLayersOverlap(surfaceWS, light))
        {
            color += GetLighting(surfaceWS, brdf, light);
        }
    }
    return color;
}

float3 ToonCutoff(float3 lighting, float threshold, float3 shadowColor, float3 lightColor)
{
    // Compute perceived brightness of the lighting
    float intensity = dot(lighting, float3(0.299, 0.587, 0.114));

    if (intensity < threshold)
        return shadowColor; // darkest band
    else
        return lightColor; // brightest band
}

float3 GetToonLighting(Fragment fragment, Surface surfaceWS, BRDF brdf, GI gi, float threshold,
                       float shadowDarknessScale)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;

    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        if (RenderingLayersOverlap(surfaceWS, light))
        {
            float3 light_Col = GetLighting(surfaceWS, brdf, light);
            // color += ToonCutoff(light_Col, threshold, light_Col * shadowDarknessScale, light_Col);
            color += light_Col;
        }
    }

    ForwardPlusTile tile = GetForwardPlusTile(fragment.screenUV);
    int lastLightIndex = tile.GetLastLightIndexInTile();
    for (int j = tile.GetFirstLightIndexInTile(); j <= lastLightIndex; j++)
    {
        Light light = GetOtherLight(
            tile.GetLightIndex(j), surfaceWS, shadowData);
        if (RenderingLayersOverlap(surfaceWS, light))
        {
            color += GetLighting(surfaceWS, brdf, light);
        }
    }
    return color;
}

#endif
