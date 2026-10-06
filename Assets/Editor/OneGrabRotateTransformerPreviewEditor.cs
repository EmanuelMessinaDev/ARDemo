using System.Reflection;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector + Scene view preview for <see cref="OneGrabRotateTransformer"/>.
/// Shows where the grabbable target would end up when the Min / Max rotation
/// constraints are reached, using the same math as the runtime transformer:
/// the target is rotated around the pivot axis by an angle relative to its rest pose.
/// </summary>
[CustomEditor(typeof(OneGrabRotateTransformer)), CanEditMultipleObjects]
public class OneGrabRotateTransformerPreviewEditor : Editor
{
    private const string ShowInScenePref = "OneGrabRotatePreview.ShowInScene";
    private const string FoldoutPref = "OneGrabRotatePreview.Foldout";

    private static readonly Color MinColor = new Color(1f, 0.4f, 0.25f);
    private static readonly Color MaxColor = new Color(0.25f, 0.65f, 1f);
    private static readonly Color PreviewColor = new Color(1f, 0.85f, 0.2f);

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo PivotField =
        typeof(OneGrabRotateTransformer).GetField("_pivotTransform", PrivateInstance);
    private static readonly FieldInfo ConstrainedAngleField =
        typeof(OneGrabRotateTransformer).GetField("_constrainedRelativeAngle", PrivateInstance);
    private static readonly FieldInfo GrabbableTransformerField =
        typeof(Grabbable).GetField("_oneGrabTransformer", PrivateInstance);
    private static readonly FieldInfo GrabbableTargetField =
        typeof(Grabbable).GetField("_targetTransform", PrivateInstance);

    private static Material _ghostMaterial;

    private bool _showPreviewAngle;
    private float _previewAngle;

    private static bool ShowInScene
    {
        get => EditorPrefs.GetBool(ShowInScenePref, true);
        set => EditorPrefs.SetBool(ShowInScenePref, value);
    }

    private static bool Foldout
    {
        get => EditorPrefs.GetBool(FoldoutPref, true);
        set => EditorPrefs.SetBool(FoldoutPref, value);
    }

    private struct Frame
    {
        public Transform Target;
        public Vector3 Pivot;
        public Vector3 Axis;
        public Vector3 RestDirection;
        public float Radius;
        public float CurrentAngle;

        public Quaternion DeltaFor(float angle) => Quaternion.AngleAxis(angle - CurrentAngle, Axis);

        public Matrix4x4 MatrixFor(float angle) =>
            Matrix4x4.Translate(Pivot) * Matrix4x4.Rotate(DeltaFor(angle)) * Matrix4x4.Translate(-Pivot);

        public Pose WorldPoseFor(float angle)
        {
            Quaternion delta = DeltaFor(angle);
            return new Pose(Pivot + delta * (Target.position - Pivot), delta * Target.rotation);
        }

        public Pose LocalPoseFor(float angle)
        {
            Pose world = WorldPoseFor(angle);
            Transform parent = Target.parent;
            if (parent == null)
            {
                return world;
            }
            return new Pose(parent.InverseTransformPoint(world.position),
                Quaternion.Inverse(parent.rotation) * world.rotation);
        }
    }

    #region Inspector

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (targets.Length != 1)
        {
            return;
        }

        EditorGUILayout.Space();
        Foldout = EditorGUILayout.Foldout(Foldout, "Rotation Constraint Preview", true, EditorStyles.foldoutHeader);
        if (!Foldout)
        {
            return;
        }

        var transformer = (OneGrabRotateTransformer)target;
        if (!TryGetFrame(transformer, out Frame frame))
        {
            EditorGUILayout.HelpBox("No target transform found.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.IndentLevelScope())
        {
            ShowInScene = EditorGUILayout.Toggle("Show In Scene", ShowInScene);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Rotated Target", frame.Target, typeof(Transform), true);
                if (Application.isPlaying)
                {
                    EditorGUILayout.FloatField("Current Angle", frame.CurrentAngle);
                }
            }

            var constraints = transformer.Constraints;
            bool minOn = constraints.MinAngle.Constrain;
            bool maxOn = constraints.MaxAngle.Constrain;
            float min = constraints.MinAngle.Value;
            float max = constraints.MaxAngle.Value;

            if (!minOn && !maxOn)
            {
                EditorGUILayout.HelpBox("No rotation constraint enabled: rotation is unbounded.", MessageType.Info);
            }
            if (minOn && maxOn && min > max)
            {
                EditorGUILayout.HelpBox("Min Angle is greater than Max Angle: the runtime will always clamp to Min.",
                    MessageType.Warning);
            }

            if (minOn)
            {
                DrawPoseInfo($"Min ({min:0.##}°)", frame, min, MinColor);
            }
            if (maxOn)
            {
                DrawPoseInfo($"Max ({max:0.##}°)", frame, max, MaxColor);
            }

            EditorGUILayout.Space();
            _showPreviewAngle = EditorGUILayout.Toggle("Preview Custom Angle", _showPreviewAngle);
            if (_showPreviewAngle)
            {
                float lo = minOn ? min : -180f;
                float hi = maxOn ? max : 180f;
                if (lo > hi)
                {
                    (lo, hi) = (hi, lo);
                }
                _previewAngle = EditorGUILayout.Slider("Angle", _previewAngle, lo, hi);
                DrawPoseInfo($"Preview ({_previewAngle:0.##}°)", frame, _previewAngle, PreviewColor);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
    }

    private static void DrawPoseInfo(string title, Frame frame, float angle, Color color)
    {
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = color;
        EditorGUILayout.LabelField(title, style);

        Pose world = frame.WorldPoseFor(angle);
        Pose local = frame.LocalPoseFor(angle);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector3Field("World Position", world.position);
            EditorGUILayout.Vector3Field("Local Position", local.position);
            EditorGUILayout.Vector3Field("Local Rotation", local.rotation.eulerAngles);
        }
    }

    #endregion

    #region Scene view

    private void OnSceneGUI()
    {
        if (!ShowInScene)
        {
            return;
        }

        var transformer = (OneGrabRotateTransformer)target;
        if (!TryGetFrame(transformer, out Frame frame))
        {
            return;
        }

        var constraints = transformer.Constraints;
        bool minOn = constraints.MinAngle.Constrain;
        bool maxOn = constraints.MaxAngle.Constrain;
        float min = constraints.MinAngle.Value;
        float max = constraints.MaxAngle.Value;

        DrawArc(frame, minOn, min, maxOn, max);

        if (minOn)
        {
            DrawAngleMarker(frame, min, MinColor, $"Min {min:0.##}°");
        }
        if (maxOn)
        {
            DrawAngleMarker(frame, max, MaxColor, $"Max {max:0.##}°");
        }
        if (_showPreviewAngle && targets.Length == 1)
        {
            DrawAngleMarker(frame, _previewAngle, PreviewColor, $"{_previewAngle:0.##}°");
        }
    }

    private static void DrawArc(Frame frame, bool minOn, float min, bool maxOn, float max)
    {
        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.DrawWireDisc(frame.Pivot, frame.Axis, frame.Radius);

        Handles.color = Color.white;
        Handles.DrawLine(frame.Pivot, frame.Pivot + frame.Axis * frame.Radius * 0.5f);
        Handles.DrawLine(frame.Pivot, frame.Pivot + frame.RestDirection * frame.Radius);

        if (minOn && maxOn && max > min)
        {
            Vector3 from = Quaternion.AngleAxis(min, frame.Axis) * frame.RestDirection;
            Handles.color = new Color(0.3f, 1f, 0.4f, 0.15f);
            Handles.DrawSolidArc(frame.Pivot, frame.Axis, from, max - min, frame.Radius);
            Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Handles.DrawWireArc(frame.Pivot, frame.Axis, from, max - min, frame.Radius, 3f);
        }
    }

    private static void DrawAngleMarker(Frame frame, float angle, Color color, string label)
    {
        Vector3 dir = Quaternion.AngleAxis(angle, frame.Axis) * frame.RestDirection;
        Vector3 edge = frame.Pivot + dir * frame.Radius;

        Handles.color = color;
        Handles.DrawLine(frame.Pivot, edge, 2f);

        Pose pose = frame.WorldPoseFor(angle);
        float size = HandleUtility.GetHandleSize(pose.position) * 0.08f;
        Handles.SphereHandleCap(0, pose.position, Quaternion.identity, size, EventType.Repaint);
        Handles.DrawDottedLine(edge, pose.position, 3f);

        var style = new GUIStyle(EditorStyles.whiteBoldLabel);
        style.normal.textColor = color;
        Handles.Label(edge, label, style);

        DrawGhost(frame, angle, color);
    }

    private static void DrawGhost(Frame frame, float angle, Color color)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        Material material = GhostMaterial;
        Matrix4x4 delta = frame.MatrixFor(angle);
        MeshFilter[] filters = frame.Target.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinned = frame.Target.GetComponentsInChildren<SkinnedMeshRenderer>();

        bool wireframe = GL.wireframe;
        for (int pass = 0; pass < 2; pass++)
        {
            GL.wireframe = pass == 1;
            material.SetColor("_Color", new Color(color.r, color.g, color.b, pass == 0 ? 0.2f : 0.5f));
            material.SetPass(0);

            foreach (MeshFilter filter in filters)
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer != null && renderer.enabled)
                {
                    DrawMesh(filter.sharedMesh, delta * filter.transform.localToWorldMatrix);
                }
            }
            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                if (renderer.enabled)
                {
                    // Bind pose approximation: good enough to visualize the extent.
                    DrawMesh(renderer.sharedMesh, delta * renderer.transform.localToWorldMatrix);
                }
            }
        }
        GL.wireframe = wireframe;
    }

    private static void DrawMesh(Mesh mesh, Matrix4x4 matrix)
    {
        if (mesh == null)
        {
            return;
        }
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            Graphics.DrawMeshNow(mesh, matrix, i);
        }
    }

    private static Material GhostMaterial
    {
        get
        {
            if (_ghostMaterial == null)
            {
                _ghostMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _ghostMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _ghostMaterial.SetInt("_ZWrite", 0);
                _ghostMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            }
            return _ghostMaterial;
        }
    }

    #endregion

    #region Frame computation

    private static bool TryGetFrame(OneGrabRotateTransformer transformer, out Frame frame)
    {
        frame = default;
        Transform target = FindTarget(transformer);
        if (target == null)
        {
            return false;
        }

        // Mirrors OneGrabRotateTransformer.ComputeWorldPivotPose: without an explicit pivot
        // the target itself is the pivot (its rotation stays on-axis while rotating).
        var pivotTransform = PivotField?.GetValue(transformer) as Transform;
        Vector3 pivotPosition = pivotTransform != null ? pivotTransform.position : target.position;
        Quaternion pivotRotation = pivotTransform != null ? pivotTransform.rotation : target.rotation;

        int axisIndex = (int)transformer.RotationAxis;
        Vector3 localAxis = Vector3.zero;
        localAxis[axisIndex] = 1f;
        Vector3 axis = pivotRotation * localAxis;

        // At runtime the angle is relative to the pose the target had when the scene started.
        float currentAngle = Application.isPlaying && ConstrainedAngleField != null
            ? (float)ConstrainedAngleField.GetValue(transformer)
            : 0f;

        Bounds? bounds = ComputeBounds(target);
        Vector3 center = bounds?.center ?? target.position;
        Vector3 offset = Vector3.ProjectOnPlane(center - pivotPosition, axis);

        Vector3 direction;
        if (offset.sqrMagnitude > 1e-6f)
        {
            direction = Quaternion.AngleAxis(-currentAngle, axis) * offset.normalized;
        }
        else
        {
            Vector3 localNext = Vector3.zero;
            localNext[(axisIndex + 1) % 3] = 1f;
            direction = Quaternion.AngleAxis(-currentAngle, axis) * (pivotRotation * localNext);
        }

        float radius = offset.magnitude;
        if (bounds.HasValue)
        {
            radius = Mathf.Max(radius, bounds.Value.extents.magnitude);
        }
        radius = Mathf.Max(radius, HandleUtility.GetHandleSize(pivotPosition) * 0.5f);

        frame = new Frame
        {
            Target = target,
            Pivot = pivotPosition,
            Axis = axis,
            RestDirection = direction,
            Radius = radius,
            CurrentAngle = currentAngle
        };
        return true;
    }

    private static Transform FindTarget(OneGrabRotateTransformer transformer)
    {
        foreach (Grabbable grabbable in transformer.GetComponentsInParent<Grabbable>(true))
        {
            if (TryGetGrabbableTarget(grabbable, transformer, out Transform target))
            {
                return target;
            }
        }
        foreach (Grabbable grabbable in transformer.GetComponentsInChildren<Grabbable>(true))
        {
            if (TryGetGrabbableTarget(grabbable, transformer, out Transform target))
            {
                return target;
            }
        }
        return transformer.transform;
    }

    private static bool TryGetGrabbableTarget(Grabbable grabbable, OneGrabRotateTransformer transformer,
        out Transform target)
    {
        target = null;
        if (GrabbableTransformerField?.GetValue(grabbable) as Object != transformer)
        {
            return false;
        }
        target = GrabbableTargetField?.GetValue(grabbable) as Transform;
        if (target == null)
        {
            target = grabbable.transform;
        }
        return true;
    }

    private static Bounds? ComputeBounds(Transform target)
    {
        Bounds? bounds = null;
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
            {
                continue;
            }
            if (bounds.HasValue)
            {
                Bounds b = bounds.Value;
                b.Encapsulate(renderer.bounds);
                bounds = b;
            }
            else
            {
                bounds = renderer.bounds;
            }
        }
        return bounds;
    }

    #endregion
}
