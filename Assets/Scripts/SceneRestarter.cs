using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Restarts the active scene with a fade out / fade in. Call <see cref="RestartScene"/> from code or
/// wire it to a UnityEvent (UI button, procedure completed, ...).
/// The fade runs on a temporary object that survives the reload, so the component can live anywhere
/// in the scene; the fade quad is attached to the main camera and moved to the new one after loading.
/// </summary>
public class SceneRestarter : MonoBehaviour
{
    [SerializeField]
    [Min(0f)]
    private float _fadeOutDuration = 0.5f;

    [SerializeField]
    [Min(0f)]
    private float _fadeInDuration = 0.5f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Time kept black after the scene is loaded, so the XR rig and tracking can settle.")]
    private float _holdDuration = 0.2f;

    [SerializeField]
    private Color _fadeColor = Color.black;

    [SerializeField]
    [Tooltip("Also fades the global audio volume (AudioListener.volume).")]
    private bool _fadeAudio = true;

    [SerializeField]
    [Tooltip("Invoked right before the fade out starts.")]
    private UnityEvent _whenRestartStarted = new UnityEvent();

    public UnityEvent WhenRestartStarted => _whenRestartStarted;

    /// <summary>True while a restart (fade out, reload, fade in) is in progress.</summary>
    public static bool IsRestarting => SceneFader.Current != null;

    /// <summary>
    /// Fades out, reloads the active scene and fades back in. Ignored if a restart is already running.
    /// </summary>
    public void RestartScene()
    {
        if (IsRestarting)
        {
            return;
        }

        _whenRestartStarted.Invoke();

        var faderObject = new GameObject("[SceneRestarter Fader]");
        DontDestroyOnLoad(faderObject);
        var fader = faderObject.AddComponent<SceneFader>();
        fader.Run(SceneManager.GetActiveScene().buildIndex, _fadeOutDuration, _holdDuration,
            _fadeInDuration, _fadeColor, _fadeAudio);
    }

    /// <summary>
    /// Persistent helper that draws the fade and drives the reload. Destroys itself when done.
    /// </summary>
    private class SceneFader : MonoBehaviour
    {
        private const string FadeShaderName = "Oculus/Unlit Transparent Color";
        private const int FadeRenderQueue = 5000;

        public static SceneFader Current { get; private set; }

        private Material _material;
        private Mesh _mesh;
        private GameObject _quad;
        private Color _color;
        private bool _fadeAudio;
        private float _initialVolume;

        public void Run(int sceneBuildIndex, float fadeOut, float hold, float fadeIn, Color color,
            bool fadeAudio)
        {
            Current = this;
            _color = color;
            _fadeAudio = fadeAudio;
            _initialVolume = AudioListener.volume;

            _material = new Material(Shader.Find(FadeShaderName)) { renderQueue = FadeRenderQueue };
            _mesh = CreateQuadMesh();

            StartCoroutine(RestartRoutine(sceneBuildIndex, fadeOut, hold, fadeIn));
        }

        private IEnumerator RestartRoutine(int sceneBuildIndex, float fadeOut, float hold, float fadeIn)
        {
            AttachToMainCamera();
            yield return Fade(0f, 1f, fadeOut);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneBuildIndex);
            while (!load.isDone)
            {
                SetAlpha(1f);
                yield return null;
            }

            // The old camera (and the quad with it) is gone: wait for the new one and stay black.
            while (!AttachToMainCamera())
            {
                yield return null;
            }
            SetAlpha(1f);
            yield return new WaitForSecondsRealtime(hold);

            yield return Fade(1f, 0f, fadeIn);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            Color color = _color;
            color.a = alpha;
            _material.color = color;

            if (_quad != null)
            {
                _quad.SetActive(alpha > 0f);
            }

            if (_fadeAudio)
            {
                AudioListener.volume = _initialVolume * (1f - alpha);
            }
        }

        private bool AttachToMainCamera()
        {
            if (_quad != null)
            {
                return true;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            _quad = new GameObject("Fade Quad");
            _quad.layer = camera.gameObject.layer;
            _quad.transform.SetParent(camera.transform, false);
            // ZTest Always: depth only has to fall inside the frustum, size covers a wide FOV.
            float distance = Mathf.Max(camera.nearClipPlane * 2f, 0.1f);
            _quad.transform.localPosition = new Vector3(0f, 0f, distance);
            _quad.transform.localScale = Vector3.one * distance * 8f;
            _quad.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = _quad.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return true;
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "Fade Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_fadeAudio)
            {
                AudioListener.volume = _initialVolume;
            }
            if (_quad != null)
            {
                Destroy(_quad);
            }
            Destroy(_material);
            Destroy(_mesh);

            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
