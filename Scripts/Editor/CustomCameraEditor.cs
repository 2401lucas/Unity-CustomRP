using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Downgraded camera inspector to generic editor to avoid warnings.
/// </summary>
[CanEditMultipleObjects]
[CustomEditor(typeof(Camera))]
[SupportedOnRenderPipeline(typeof(CustomRenderPipelineAsset))]
public class CustomCameraEditor : Editor {}
