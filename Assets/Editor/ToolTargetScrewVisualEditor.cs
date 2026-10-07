using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Scene view tools for <see cref="ToolTargetScrewVisual"/> (edit mode only): a ghost of the bolt at the
/// preview progress (1 = end of the screwing action), the path travelled along the axis, and a handle to
/// drag Travel.
/// </summary>
[CustomEditor(typeof(ToolTargetScrewVisual))]
public class ToolTargetScrewVisualEditor : Editor
{
    private static readonly Color GhostColor = new Color(1f, 0.6f, 0f, 0.35f);
    private static readonly Color PathColor = new Color(1f, 0.6f, 0f, 1f);

    // Static so the preview progress is kept when selecting another bolt.
    private static float _previewProgress = 1f;

    private SerializedProperty _bolt;
    private SerializedProperty _localAxis;
    private SerializedProperty _turns;
    private SerializedProperty _travel;

    private Material _ghostMaterial;

    protected virtual void OnEnable()
    {
        _bolt = serializedObject.FindProperty("_bolt");
        _localAxis = serializedObject.FindProperty("_localAxis");
        _turns = serializedObject.FindProperty("_turns");
        _travel = serializedObject.FindProperty("_travel");

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

        if (Application.isPlaying)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        _previewProgress = EditorGUILayout.Slider("Preview Progress", _previewProgress, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox("The orange ghost shows the bolt at Preview Progress (1 = end of the screwing " +
                                "action). Drag the orange arrow in the Scene view to change Travel.",
            MessageType.None);
    }

    protected virtual void OnSceneGUI()
    {
        if (Application.isPlaying)
        {
            return;
        }

        serializedObject.Update();

        Transform bolt = GetBolt();
        Vector3 localAxis = _localAxis.vector3Value;
        if (bolt == null || localAxis == Vector3.zero)
        {
            return;
        }

        // In edit mode the bolt's current pose is its start pose. Travel is in the parent's space.
        Vector3 travelDirection = bolt.localRotation * localAxis.normalized;
        Vector3 start = bolt.position;
        Vector3 end = ParentToWorld(bolt, bolt.localPosition + travelDirection * _travel.floatValue);
        Vector3 worldDirection = (ParentToWorld(bolt, bolt.localPosition + travelDirection) - start).normalized;
        float size = HandleUtility.GetHandleSize(end);

        Handles.color = PathColor;
        Handles.DrawDottedLine(start, end, 4f);
        Handles.DrawWireDisc(end, worldDirection, size * 0.15f);

        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.Slider(end, worldDirection, size * 0.6f, Handles.ArrowHandleCap,
            EditorSnapSettings.move.z);
        moved = Handles.Slider(moved, -worldDirection, size * 0.6f, Handles.ArrowHandleCap,
            EditorSnapSettings.move.z);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 localMoved = bolt.parent != null ? bolt.parent.InverseTransformPoint(moved) : moved;
            _travel.floatValue = Vector3.Dot(localMoved - bolt.localPosition, travelDirection);
            serializedObject.ApplyModifiedProperties();
        }

        Handles.Label(end + Vector3.up * size * 0.2f,
            $"End  {_travel.floatValue * 1000f:0.#} mm · {_turns.floatValue:0.##} turns");
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.SceneView || target == null || Application.isPlaying)
        {
            return;
        }

        Transform bolt = GetBolt();
        Vector3 localAxis = _localAxis.vector3Value;
        if (bolt == null || localAxis == Vector3.zero)
        {
            return;
        }

        // Same math as ToolTargetScrewVisual.Apply, with the current local pose as the start pose.
        Vector3 axis = localAxis.normalized;
        Quaternion rotation = bolt.localRotation
                              * Quaternion.AngleAxis(_turns.floatValue * 360f * _previewProgress, axis);
        Vector3 position = bolt.localPosition + bolt.localRotation * axis * (_travel.floatValue * _previewProgress);

        Matrix4x4 parent = bolt.parent != null ? bolt.parent.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 previewBolt = parent * Matrix4x4.TRS(position, rotation, bolt.localScale);
        Matrix4x4 offset = previewBolt * bolt.worldToLocalMatrix;

        foreach (MeshFilter meshFilter in bolt.GetComponentsInChildren<MeshFilter>())
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
    /// The bolt transform, defaulting to the component's own transform as at runtime.
    /// </summary>
    private Transform GetBolt()
    {
        var bolt = _bolt.objectReferenceValue as Transform;
        return bolt != null ? bolt : ((Component)target).transform;
    }

    private static Vector3 ParentToWorld(Transform transform, Vector3 localPosition)
    {
        return transform.parent != null ? transform.parent.TransformPoint(localPosition) : localPosition;
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
