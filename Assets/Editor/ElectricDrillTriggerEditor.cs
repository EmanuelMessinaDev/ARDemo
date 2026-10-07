using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Scene view tools for <see cref="ElectricDrillTrigger"/> (edit mode only): a ghost of the trigger at the
/// preview pressure, the arc swept up to Max Angle, and a handle to drag Max Angle around the rotation axis.
/// </summary>
[CustomEditor(typeof(ElectricDrillTrigger))]
public class ElectricDrillTriggerEditor : Editor
{
    private static readonly Color GhostColor = new Color(1f, 0.6f, 0f, 0.35f);
    private static readonly Color ArcColor = new Color(1f, 0.6f, 0f, 0.15f);

    // Static so the preview pressure is kept when selecting another drill.
    private static float _previewStrength = 1f;

    private SerializedProperty _trigger;
    private SerializedProperty _rotationAxis;
    private SerializedProperty _maxAngle;

    private Material _ghostMaterial;

    protected virtual void OnEnable()
    {
        _trigger = serializedObject.FindProperty("_trigger");
        _rotationAxis = serializedObject.FindProperty("_rotationAxis");
        _maxAngle = serializedObject.FindProperty("_maxAngle");

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

        if (Application.isPlaying || _trigger.objectReferenceValue == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        _previewStrength = EditorGUILayout.Slider("Preview Strength", _previewStrength, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox("The orange ghost shows the trigger at Preview Strength (1 = Max Angle). " +
                                "Drag the orange disc in the Scene view to change Max Angle.", MessageType.None);
    }

    protected virtual void OnSceneGUI()
    {
        if (Application.isPlaying)
        {
            return;
        }

        serializedObject.Update();

        var trigger = _trigger.objectReferenceValue as Transform;
        Vector3 localAxis = _rotationAxis.vector3Value;
        if (trigger == null || localAxis == Vector3.zero)
        {
            return;
        }

        localAxis.Normalize();
        float maxAngle = _maxAngle.floatValue;
        Vector3 position = trigger.position;
        Vector3 worldAxis = trigger.rotation * localAxis;
        Vector3 localReference = Perpendicular(localAxis);
        Vector3 worldReference = trigger.rotation * localReference;
        float radius = HandleUtility.GetHandleSize(position) * 0.6f;

        Handles.color = ArcColor;
        Handles.DrawSolidArc(position, worldAxis, worldReference, maxAngle, radius);

        Handles.color = GhostColor;
        Handles.DrawLine(position, position + worldReference * radius);
        Vector3 maxDirection = Quaternion.AngleAxis(maxAngle, worldAxis) * worldReference;
        Handles.DrawLine(position, position + maxDirection * radius);
        Handles.Label(position + maxDirection * radius * 1.1f, $"Max {maxAngle:0.#}°");

        Quaternion restRotation = trigger.rotation;
        Quaternion maxRotation = restRotation * Quaternion.AngleAxis(maxAngle, localAxis);

        EditorGUI.BeginChangeCheck();
        Quaternion newRotation = Handles.Disc(maxRotation, position, worldAxis, radius, false,
            EditorSnapSettings.rotate);
        if (EditorGUI.EndChangeCheck())
        {
            Quaternion localDelta = Quaternion.Inverse(restRotation) * newRotation;
            _maxAngle.floatValue = Vector3.SignedAngle(localReference, localDelta * localReference, localAxis);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        var drillTrigger = target as ElectricDrillTrigger;
        if (camera.cameraType != CameraType.SceneView || drillTrigger == null || Application.isPlaying)
        {
            return;
        }

        var trigger = _trigger.objectReferenceValue as Transform;
        Vector3 localAxis = _rotationAxis.vector3Value;
        if (trigger == null || localAxis == Vector3.zero)
        {
            return;
        }

        // The trigger's current pose is its rest pose: pivot its meshes around it by the preview angle.
        float angle = _maxAngle.floatValue * _previewStrength;
        Matrix4x4 pivot = Matrix4x4.TRS(trigger.position, trigger.rotation, Vector3.one);
        Matrix4x4 offset = pivot * Matrix4x4.Rotate(Quaternion.AngleAxis(angle, localAxis.normalized))
                           * pivot.inverse;

        foreach (MeshFilter meshFilter in trigger.GetComponentsInChildren<MeshFilter>())
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

    private static Vector3 Perpendicular(Vector3 axis)
    {
        Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.99f ? Vector3.up : Vector3.forward;
        return Vector3.Cross(axis, reference).normalized;
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
