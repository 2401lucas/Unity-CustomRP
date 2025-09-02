using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField]
    CameraSettings settings = default;

    [System.NonSerialized]
    ProfilingSampler sampler;

    public ProfilingSampler Sampler => sampler ??= new(GetComponent<Camera>().name);

    public CameraSettings Settings => settings ??= new();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void OnEnable() => sampler = null;
#endif
}
