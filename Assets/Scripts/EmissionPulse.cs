using UnityEngine;

/// <summary>
/// Pulses the emission color of a material assigned to a MeshRenderer between two intensities.
/// The pulse is started and stopped through public methods (e.g. from UnityEvents).
/// </summary>
public class EmissionPulse : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField]
    private MeshRenderer _renderer;

    [SerializeField]
    [Tooltip("Index of the material in the renderer's materials array.")]
    private int _materialIndex;

    [SerializeField]
    [Tooltip("If disabled, the material's base color is used as emission color.")]
    private bool _overrideColor;

    [SerializeField]
    [Tooltip("Emission color used when Override Color is enabled. Multiplied by the pulse intensity.")]
    [ColorUsage(false, true)]
    private Color _emissionColor = Color.white;

    [SerializeField]
    [Tooltip("Emission intensity at the low point of the pulse.")]
    private float _minIntensity;

    [SerializeField]
    [Tooltip("Emission intensity at the high point of the pulse.")]
    private float _maxIntensity = 1f;

    [SerializeField]
    [Tooltip("Pulses per second.")]
    private float _frequency = 1f;

    [SerializeField]
    [Tooltip("If disabled, the pulse follows a sine wave.")]
    private bool _useCurve;

    [SerializeField]
    [Tooltip("Shapes the pulse over one period (0..1 in, 0..1 out). Used when Use Curve is enabled.")]
    private AnimationCurve _curve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));

    [SerializeField]
    private bool _playOnEnable;

    private Material _material;
    private Color _originalEmission;
    private bool _originalKeyword;
    private bool _pulsing;
    private float _time;

    public bool IsPulsing => _pulsing;

    private void Awake()
    {
        if (_renderer == null)
        {
            _renderer = GetComponent<MeshRenderer>();
        }

        // Accessing .materials creates per-renderer instances, so the shared asset is not modified.
        _material = _renderer.materials[_materialIndex];
        _originalEmission = _material.GetColor(EmissionColorId);
        _originalKeyword = _material.IsKeywordEnabled("_EMISSION");
    }

    private void OnEnable()
    {
        if (_playOnEnable)
        {
            StartPulse();
        }
    }

    private void OnDisable()
    {
        StopPulse();
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }

    /// <summary>
    /// Starts pulsing the emission from the low point.
    /// </summary>
    public void StartPulse()
    {
        if (_pulsing)
        {
            return;
        }

        _pulsing = true;
        _time = 0f;
        _material.EnableKeyword("_EMISSION");
        Apply(0f);
    }

    /// <summary>
    /// Stops pulsing and restores the material's original emission.
    /// </summary>
    public void StopPulse()
    {
        if (!_pulsing)
        {
            return;
        }

        _pulsing = false;
        _material.SetColor(EmissionColorId, _originalEmission);
        if (!_originalKeyword)
        {
            _material.DisableKeyword("_EMISSION");
        }
    }

    /// <summary>
    /// Starts or stops the pulse.
    /// </summary>
    public void SetPulse(bool active)
    {
        if (active)
        {
            StartPulse();
        }
        else
        {
            StopPulse();
        }
    }

    public void TogglePulse() => SetPulse(!_pulsing);

    private void Update()
    {
        if (!_pulsing)
        {
            return;
        }

        _time += Time.deltaTime;
        Apply(Mathf.Repeat(_time * _frequency, 1f));
    }

    private void Apply(float phase)
    {
        float t = _useCurve && _curve != null && _curve.length > 0
            ? _curve.Evaluate(phase)
            : 0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI);

        float intensity = Mathf.LerpUnclamped(_minIntensity, _maxIntensity, t);
        _material.SetColor(EmissionColorId, PulseColor() * intensity);
    }

    private Color PulseColor() => FullBrightness(SourceColor());

    /// <summary>
    /// Raises the HSV value to at least 1, keeping hue and saturation. HDR colors above 1 are left as they are.
    /// </summary>
    private static Color FullBrightness(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        return Color.HSVToRGB(h, s, Mathf.Max(v, 1f), true);
    }

    private Color SourceColor()
    {
        if (_overrideColor)
        {
            return _emissionColor;
        }

        if (_material.HasProperty(BaseColorId))
        {
            return _material.GetColor(BaseColorId);
        }

        return _material.HasProperty(ColorId) ? _material.GetColor(ColorId) : _emissionColor;
    }

    private void Reset()
    {
        _renderer = GetComponent<MeshRenderer>();
    }
}
