using UnityEngine;

/// <summary>
/// Turns the emission of a material assigned to a MeshRenderer on and off with a steady intensity.
/// The state is changed through public methods (e.g. from UnityEvents).
/// </summary>
public class EmissionToggle : MonoBehaviour
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
    [Tooltip("Emission color used when Override Color is enabled. Multiplied by the intensity.")]
    [ColorUsage(false, true)]
    private Color _emissionColor = Color.white;

    [SerializeField]
    [Tooltip("Emission intensity while on.")]
    private float _intensity = 1f;

    [SerializeField]
    private bool _onAtStart;

    private Material _material;
    private Color _originalEmission;
    private bool _originalKeyword;
    private bool _on;

    public bool IsOn => _on;

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

    private void Start()
    {
        if (_onAtStart)
        {
            TurnOn();
        }
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }

    /// <summary>
    /// Turns the emission on.
    /// </summary>
    public void TurnOn()
    {
        _on = true;
        _material.EnableKeyword("_EMISSION");
        _material.SetColor(EmissionColorId, EmissionColor() * _intensity);
    }

    /// <summary>
    /// Turns the emission off, restoring the material's original emission.
    /// </summary>
    public void TurnOff()
    {
        _on = false;
        _material.SetColor(EmissionColorId, _originalEmission);
        if (!_originalKeyword)
        {
            _material.DisableKeyword("_EMISSION");
        }
    }

    /// <summary>
    /// Turns the emission on or off.
    /// </summary>
    public void SetOn(bool on)
    {
        if (on)
        {
            TurnOn();
        }
        else
        {
            TurnOff();
        }
    }

    public void Toggle() => SetOn(!_on);

    private Color EmissionColor()
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
