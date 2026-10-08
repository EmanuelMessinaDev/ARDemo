using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Smoothly rotates a gauge needle towards a target value, mapping the gauge's value range to an angle range.
/// The target is set through public methods (e.g. from UnityEvents with a static parameter).
/// </summary>
public class GaugeNeedle : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Needle transform to rotate. Its pivot must be at the gauge center.")]
    private Transform _needle;

    [SerializeField]
    [Tooltip("Local axis the needle rotates around.")]
    private Vector3 _axis = Vector3.forward;

    [Header("Range")]
    [SerializeField]
    [Tooltip("Value shown when the needle is at Min Angle.")]
    private float _minValue;

    [SerializeField]
    [Tooltip("Value shown when the needle is at Max Angle.")]
    private float _maxValue = 10f;

    [SerializeField]
    [Tooltip("Needle angle, relative to its rest rotation, for Min Value.")]
    private float _minAngle = -135f;

    [SerializeField]
    [Tooltip("Needle angle, relative to its rest rotation, for Max Value.")]
    private float _maxAngle = 135f;

    [SerializeField]
    [Tooltip("Value shown at start.")]
    private float _startValue;

    [Header("Motion")]
    [SerializeField]
    [Tooltip("Approximate time to reach the target value.")]
    private float _smoothTime = 0.5f;

    [SerializeField]
    [Tooltip("Maximum speed in value units per second. 0 means unlimited.")]
    private float _maxSpeed;

    [Header("Fluctuation")]
    [SerializeField]
    [Tooltip("Maximum random deviation, in value units, shown while the value is above 0. 0 disables it.")]
    private float _fluctuation = 0.05f;

    [SerializeField]
    [Tooltip("How fast the fluctuation changes. Higher values look more nervous.")]
    private float _fluctuationSpeed = 1.5f;

    [Header("Gizmo")]
    [SerializeField]
    [Tooltip("Local direction the needle points to at rest. Used only by the scene gizmo.")]
    private Vector3 _needleDirection = Vector3.up;

    [SerializeField]
    [Tooltip("Radius of the scene gizmo arc.")]
    private float _gizmoRadius = 0.1f;

    [Header("Events")]
    [SerializeField]
    [Tooltip("Invoked every frame the shown value changes.")]
    private UnityEvent<float> _whenValueChanged;

    [SerializeField]
    [Tooltip("Invoked once when the needle reaches the target value.")]
    private UnityEvent _whenTargetReached;

    private const float ReachedThreshold = 0.001f;

    private Quaternion _restRotation;
    private float _value;
    private float _target;
    private float _velocity;
    private bool _moving;
    private float _noiseSeed;

    /// <summary>Value the needle is at, without fluctuation.</summary>
    public float Value => _value;

    /// <summary>Value actually shown by the needle, fluctuation included.</summary>
    public float ShownValue { get; private set; }

    /// <summary>Value the needle is moving towards.</summary>
    public float Target => _target;

    public UnityEvent<float> WhenValueChanged => _whenValueChanged;
    public UnityEvent WhenTargetReached => _whenTargetReached;

    private void Awake()
    {
        if (_needle == null)
        {
            _needle = transform;
        }

        _restRotation = _needle.localRotation;
        _noiseSeed = Random.Range(0f, 1000f);
        SetValueImmediate(_startValue);
    }

    /// <summary>
    /// Smoothly moves the needle to the given value, clamped to the gauge range.
    /// </summary>
    public void SetValue(float value)
    {
        _target = Clamp(value);
        _moving = true;
    }

    /// <summary>
    /// Smoothly moves the needle to a fraction (0..1) of the gauge range.
    /// </summary>
    public void SetNormalizedValue(float normalized)
    {
        SetValue(Mathf.LerpUnclamped(_minValue, _maxValue, normalized));
    }

    /// <summary>
    /// Smoothly moves the needle by the given amount from its current target.
    /// </summary>
    public void AddValue(float delta)
    {
        SetValue(_target + delta);
    }

    /// <summary>
    /// Moves the needle to the given value without animation.
    /// </summary>
    public void SetValueImmediate(float value)
    {
        _target = Clamp(value);
        _value = _target;
        _velocity = 0f;
        _moving = false;
        ApplyRotation();
        _whenValueChanged.Invoke(_value);
    }

    /// <summary>
    /// Sets the time used to reach the target, so different events can move the needle at different paces.
    /// </summary>
    public void SetSmoothTime(float smoothTime)
    {
        _smoothTime = Mathf.Max(0f, smoothTime);
    }

    private void Update()
    {
        if (!_moving)
        {
            if (_fluctuation > 0f && _value > 0f)
            {
                ApplyRotation();
            }

            return;
        }

        float maxSpeed = _maxSpeed > 0f ? _maxSpeed : Mathf.Infinity;
        _value = Mathf.SmoothDamp(_value, _target, ref _velocity, _smoothTime, maxSpeed);

        if (Mathf.Abs(_value - _target) < ReachedThreshold * Mathf.Abs(_maxValue - _minValue))
        {
            _value = _target;
            _velocity = 0f;
            _moving = false;
        }

        ApplyRotation();
        _whenValueChanged.Invoke(_value);

        if (!_moving)
        {
            _whenTargetReached.Invoke();
        }
    }

    /// <summary>
    /// Needle angle, relative to its rest rotation, for the given value.
    /// </summary>
    public float AngleOf(float value)
    {
        float t = Mathf.InverseLerp(_minValue, _maxValue, value);
        return Mathf.Lerp(_minAngle, _maxAngle, t);
    }

    private void ApplyRotation()
    {
        ShownValue = Clamp(_value + Fluctuation());
        _needle.localRotation = _restRotation * Quaternion.AngleAxis(AngleOf(ShownValue), _axis);
    }

    private float Fluctuation()
    {
        if (_fluctuation <= 0f || _value <= 0f || !Application.isPlaying)
        {
            return 0f;
        }

        // Two octaves of Perlin noise in -1..1 give an irregular, flow-like movement.
        float time = Time.time * _fluctuationSpeed;
        float noise = (Mathf.PerlinNoise(time, _noiseSeed) - 0.5f) * 2f * 0.7f
            + (Mathf.PerlinNoise(time * 3.1f, _noiseSeed + 17f) - 0.5f) * 2f * 0.3f;

        // Fades in near 0, so the needle never dips below 0 because of the fluctuation.
        float weight = Mathf.Clamp01(_value / _fluctuation);
        return noise * _fluctuation * weight;
    }

    private float Clamp(float value)
    {
        return Mathf.Clamp(value, Mathf.Min(_minValue, _maxValue), Mathf.Max(_minValue, _maxValue));
    }

    private void Reset()
    {
        _needle = transform;
    }
}
