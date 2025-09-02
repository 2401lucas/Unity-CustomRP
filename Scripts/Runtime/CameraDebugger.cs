using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class CameraDebugger
{
    const string panelName = "Forward+";

    static readonly int opacityID = Shader.PropertyToID("_DebugOpacity");

    static Material material;

    static bool showTiles;

    static float opacity = 0.5f;

    public static bool IsActive => showTiles && opacityID > 0f;

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Initialize(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        DebugManager.instance.GetPanel(panelName, true).children.Add(new DebugUI.FloatField
        {
            displayName = "Opacity",
            tooltip = "Opacity of the debug overlay.",
            min = static () => 0f,
            max = static () => 1f,
            getter = static () => opacity,
            setter = static value => opacity = value
        },
            new DebugUI.BoolField
            {
                displayName = "Show Tiles",
                tooltip = "Whether the debug overlay is shown.",
                getter = static () => showTiles,
                setter = static value => showTiles = value
            });
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Cleanup()
    {
        CoreUtils.Destroy(material);
        DebugManager.instance.RemovePanel(panelName);
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Render(UnsafeGraphContext context)
    {
        UnsafeCommandBuffer buffer = context.cmd;
        buffer.SetGlobalFloat(opacityID, opacity);
        buffer.DrawProcedural(
            Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
    }
}
