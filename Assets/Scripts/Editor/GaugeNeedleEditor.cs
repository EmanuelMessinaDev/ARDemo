using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view helper for <see cref="GaugeNeedle"/>: draws the gauge arc and lets the min/max angles
/// and the start value be set by dragging handles. Hold Ctrl while dragging to snap to 5 degrees.
/// </summary>
[CustomEditor(typeof(GaugeNeedle))]
public class GaugeNeedleEditor : Editor
{
    private const int TickCount = 10;
    private const float SnapAngle = 5f;

    private static readonly Color ArcColor = new Color(1f, 1f, 1f, 0.8f);
    private static readonly Color FillColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color MinColor = new Color(0.3f, 0.6f, 1f);
    private static readonly Color MaxColor = new Color(1f, 0.35f, 0.3f);
    private static readonly Color StartColor = new Color(1f, 0.85f, 0.2f);
    private static readonly Color TargetColor = new Color(0.3f, 1f, 0.4f);

    private SerializedProperty _needle;
    private SerializedProperty _axis;
    private SerializedProperty _minValue;
    private SerializedProperty _maxValue;
    private SerializedProperty _minAngle;
    private SerializedProperty _maxAngle;
    private SerializedProperty _startValue;
    private SerializedProperty _needleDirection;
    private SerializedProperty _gizmoRadius;

    private void OnEnable()
    {
        _needle = serializedObject.FindProperty("_needle");
        _axis = serializedObject.FindProperty("_axis");
        _minValue = serializedObject.FindProperty("_minValue");
        _maxValue = serializedObject.FindProperty("_maxValue");
        _minAngle = serializedObject.FindProperty("_minAngle");
        _maxAngle = serializedObject.FindProperty("_maxAngle");
        _startValue = serializedObject.FindProperty("_startValue");
        _needleDirection = serializedObject.FindProperty("_needleDirection");
        _gizmoRadius = serializedObject.FindProperty("_gizmoRadius");
    }

    private void OnSceneGUI()
    {
        var gauge = (GaugeNeedle)target;
        serializedObject.Update();

        Transform needle = _needle.objectReferenceValue as Transform;
        if (needle == null)
        {
            needle = gauge.transform;
        }

        // In play mode the needle is rotated, so its rest rotation is recovered from the shown value.
        Quaternion rest = needle.rotation;
        if (Application.isPlaying)
        {
            rest *= Quaternion.Inverse(Quaternion.AngleAxis(gauge.AngleOf(gauge.ShownValue), _axis.vector3Value));
        }

        Vector3 center = needle.position;
        Vector3 axis = (rest * _axis.vector3Value).normalized;
        Vector3 zero = Vector3.ProjectOnPlane(rest * _needleDirection.vector3Value, axis).normalized;
        if (axis == Vector3.zero || zero == Vector3.zero)
        {
            return;
        }

        float radius = _gizmoRadius.floatValue;
        float minAngle = _minAngle.floatValue;
        float maxAngle = _maxAngle.floatValue;

        Vector3 Direction(float angle) => Quaternion.AngleAxis(angle, axis) * zero;

        // Arc and ticks.
        Handles.color = FillColor;
        Handles.DrawSolidArc(center, axis, Direction(minAngle), maxAngle - minAngle, radius);
        Handles.color = ArcColor;
        Handles.DrawWireArc(center, axis, Direction(minAngle), maxAngle - minAngle, radius);
        for (int i = 0; i <= TickCount; i++)
        {
            Vector3 dir = Direction(Mathf.Lerp(minAngle, maxAngle, (float)i / TickCount));
            float inner = i % 5 == 0 ? 0.85f : 0.92f;
            Handles.DrawLine(center + dir * radius * inner, center + dir * radius);
        }

        Handles.Label(center + Direction(minAngle) * radius * 1.15f, $"{_minValue.floatValue:0.##}");
        Handles.Label(center + Direction(maxAngle) * radius * 1.15f, $"{_maxValue.floatValue:0.##}");

        if (Application.isPlaying)
        {
            Handles.color = TargetColor;
            Handles.DrawLine(center, center + Direction(gauge.AngleOf(gauge.Target)) * radius, 2f);
            return;
        }

        // Min / max angle handles.
        float size = HandleUtility.GetHandleSize(center) * 0.08f;

        float newMin = AngleHandle(center, axis, zero, radius, minAngle, size, MinColor);
        float newMax = AngleHandle(center, axis, zero, radius, maxAngle, size, MaxColor);

        // Start value handle, dragged along the arc.
        float startAngle = AngleOf(_startValue.floatValue);
        Handles.color = StartColor;
        Handles.DrawLine(center, center + Direction(startAngle) * radius * 0.9f, 2f);
        float newStartAngle = AngleHandle(center, axis, zero, radius * 0.9f, startAngle, size, StartColor);
        Handles.Label(center + Direction(startAngle) * radius * 0.6f, $"Start {_startValue.floatValue:0.##}");

        if (!Mathf.Approximately(newMin, minAngle))
        {
            _minAngle.floatValue = newMin;
        }

        if (!Mathf.Approximately(newMax, maxAngle))
        {
            _maxAngle.floatValue = newMax;
        }

        if (!Mathf.Approximately(newStartAngle, startAngle) && !Mathf.Approximately(minAngle, maxAngle))
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(minAngle, maxAngle, newStartAngle));
            _startValue.floatValue = Mathf.Lerp(_minValue.floatValue, _maxValue.floatValue, t);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private float AngleOf(float value)
    {
        float t = Mathf.InverseLerp(_minValue.floatValue, _maxValue.floatValue, value);
        return Mathf.Lerp(_minAngle.floatValue, _maxAngle.floatValue, t);
    }

    /// <summary>
    /// Draws a handle on the arc at the given angle and returns the angle it was dragged to.
    /// </summary>
    private static float AngleHandle(
        Vector3 center, Vector3 axis, Vector3 zero, float radius, float angle, float size, Color color)
    {
        Vector3 position = center + Quaternion.AngleAxis(angle, axis) * zero * radius;

        Handles.color = color;
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.FreeMoveHandle(position, size, Vector3.zero, Handles.SphereHandleCap);
        if (!EditorGUI.EndChangeCheck())
        {
            return angle;
        }

        Vector3 onPlane = Vector3.ProjectOnPlane(moved - center, axis);
        if (onPlane == Vector3.zero)
        {
            return angle;
        }

        // Unwrap relative to the previous angle, so ranges wider than 180 degrees keep working.
        float measured = Vector3.SignedAngle(zero, onPlane, axis);
        float result = angle + Mathf.DeltaAngle(angle, measured);

        if (Event.current.control)
        {
            result = Mathf.Round(result / SnapAngle) * SnapAngle;
        }

        return result;
    }
}
