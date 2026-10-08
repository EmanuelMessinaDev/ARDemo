using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Scene view preview for <see cref="PokeInteractableVisual"/> (edit mode only): a ghost of the button at the
/// preview press (1 = fully pressed onto Button Base), the path it travels, and the Button Base plane.
/// Uses the same math as the runtime component: the button moves along Button Base's forward, keeping its
/// planar offset, from its current distance down to the base plane.
/// </summary>
[CustomEditor(typeof(PokeInteractableVisual))]
public class PokeInteractableVisualEditor : Editor
{
    private static readonly Color GhostColor = new Color(1f, 0.6f, 0f, 0.35f);
    private static readonly Color PathColor = new Color(1f, 0.6f, 0f, 1f);
    private static readonly Color PlaneColor = new Color(1f, 0.6f, 0f, 0.1f);

    // Static so the preview press is kept when selecting another button.
    private static float _previewPress = 1f;

    private SerializedProperty _buttonBaseTransform;

    private Material _ghostMaterial;

    protected virtual void OnEnable()
    {
        _buttonBaseTransform = serializedObject.FindProperty("_buttonBaseTransform");
        _ghostMaterial = CreateGhostMaterial(GhostColor);

        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    }

    protected virtual void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        DestroyImmediate(_ghostMaterial);
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (Application.isPlaying || !TryGetTravel(out _, out _, out float travel))
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        _previewPress = EditorGUILayout.Slider("Preview Press", _previewPress, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
        EditorGUILayout.LabelField("Travel", $"{travel * 1000f:0.#} mm");

        if (travel <= 0f)
        {
            EditorGUILayout.HelpBox("This transform is on or behind the Button Base plane (along its -forward), " +
                                    "so the button will not move when poked. Move it in front of the base, " +
                                    "towards -forward.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("The orange ghost shows the button at Preview Press (1 = fully pressed " +
                                    "onto Button Base).", MessageType.None);
        }
    }

    protected virtual void OnSceneGUI()
    {
        if (Application.isPlaying)
        {
            return;
        }

        serializedObject.Update();

        if (!TryGetTravel(out Transform buttonBase, out Vector3 pressedPosition, out float travel))
        {
            return;
        }

        var visual = (PokeInteractableVisual)target;
        Vector3 restPosition = visual.transform.position;
        float size = HandleUtility.GetHandleSize(pressedPosition) * 0.3f;

        // Button Base plane, around the point where the button stops.
        Vector3 right = buttonBase.right * size;
        Vector3 up = buttonBase.up * size;
        Vector3[] corners =
        {
            pressedPosition - right - up,
            pressedPosition - right + up,
            pressedPosition + right + up,
            pressedPosition + right - up
        };
        Handles.DrawSolidRectangleWithOutline(corners, PlaneColor, PathColor);

        Handles.color = PathColor;
        Handles.DrawDottedLine(restPosition, pressedPosition, 3f);
        Handles.Label(pressedPosition + up * 1.2f,
            travel > 0f ? $"Pressed  {travel * 1000f:0.#} mm" : "No travel");
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.SceneView || Application.isPlaying || target == null
            || !TryGetTravel(out _, out Vector3 pressedPosition, out float travel) || travel <= 0f)
        {
            return;
        }

        var visual = (PokeInteractableVisual)target;
        Matrix4x4 offset = Matrix4x4.Translate((pressedPosition - visual.transform.position) * _previewPress);

        foreach (MeshFilter meshFilter in visual.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Matrix4x4 matrix = offset * meshFilter.transform.localToWorldMatrix;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Graphics.DrawMesh(mesh, matrix, _ghostMaterial, 0, camera, i, null, ShadowCastingMode.Off, false);
            }
        }
    }

    /// <summary>
    /// Fully pressed position and travel distance, as computed by PokeInteractableVisual.Start.
    /// </summary>
    private bool TryGetTravel(out Transform buttonBase, out Vector3 pressedPosition, out float travel)
    {
        buttonBase = _buttonBaseTransform.objectReferenceValue as Transform;
        if (buttonBase == null)
        {
            pressedPosition = Vector3.zero;
            travel = 0f;
            return false;
        }

        Vector3 position = ((PokeInteractableVisual)target).transform.position;
        travel = Vector3.Dot(position - buttonBase.position, -buttonBase.forward);
        pressedPosition = position + buttonBase.forward * travel;
        return true;
    }

    /// <summary>
    /// Transparent, unlit, double-sided material that works with any render pipeline.
    /// </summary>
    private static Material CreateGhostMaterial(Color color)
    {
        var material = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave,
            color = color
        };
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZWrite", 0);
        return material;
    }
}
