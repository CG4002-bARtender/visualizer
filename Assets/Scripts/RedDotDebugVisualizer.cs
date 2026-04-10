using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a crosshair overlay at the tracked green dot position.
/// Uses a Screen Space Overlay Canvas so it is never scissored by camera viewport rects,
/// which fixes the split-screen case where OnGUI gets clipped to the left eye's viewport.
///
/// No scene setup required — the canvas is created at runtime.
/// In stereo mode, a crosshair is drawn in each eye half at the same relative position.
/// </summary>
public class RedDotDebugVisualizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave blank to auto-find in scene.")]
    public RedCircleTracker tracker;
    [Tooltip("Leave blank to auto-find in scene.")]
    public SplitScreenManager splitScreen;

    [Header("Overlay")]
    public float markerRadius = 24f;
    public Color detectedColor = new Color(0.2f, 1f, 0.2f, 0.9f);
    public Color lostColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    public bool showHUD = true;

    [Header("World-Space Marker (optional)")]
    [Tooltip("Prefab placed in AR space at the tracked position. Leave blank to skip.")]
    public GameObject markerPrefab;
    public float markerDepth = 1.5f;

    // Canvas overlay
    private GameObject _overlayRoot;
    private RectTransform _leftRT, _rightRT;
    private Image[] _leftImgs, _rightImgs;
    private Text _hudText;

    // World-space marker
    private GameObject _worldMarker;
    private Camera _cam;

    void Start()
    {
        if (tracker    == null) tracker    = FindObjectOfType<RedCircleTracker>();
        if (splitScreen == null) splitScreen = FindObjectOfType<SplitScreenManager>();
        _cam = Camera.main;

        BuildCanvasOverlay();

        if (markerPrefab != null)
        {
            _worldMarker = Instantiate(markerPrefab);
            _worldMarker.name = "[GreenDot] WorldMarker";
            _worldMarker.SetActive(false);
        }
    }

    void BuildCanvasOverlay()
    {
        _overlayRoot = new GameObject("[GreenDot] Overlay");
        DontDestroyOnLoad(_overlayRoot);

        var canvas = _overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _overlayRoot.AddComponent<CanvasScaler>(); // Constant Pixel Size — canvas units = screen pixels

        _leftImgs  = BuildCrosshair(_overlayRoot.transform, "Left",  out _leftRT);
        _rightImgs = BuildCrosshair(_overlayRoot.transform, "Right", out _rightRT);
        _rightRT.gameObject.SetActive(false);

        if (showHUD)
        {
            var hudGO = new GameObject("HUD");
            hudGO.transform.SetParent(_overlayRoot.transform, false);
            var rt = hudGO.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // top-left corner
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -20f);
            rt.sizeDelta = new Vector2(500f, 130f);
            _hudText = hudGO.AddComponent<Text>();
            _hudText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hudText.fontSize  = 26;
            _hudText.fontStyle = FontStyle.Bold;
            _hudText.color     = Color.green;
        }
    }

    // Builds a crosshair (6 Image rects) parented under 'parent', returns the root RectTransform.
    Image[] BuildCrosshair(Transform parent, string label, out RectTransform rootRT)
    {
        var go = new GameObject($"[GreenDot] {label}");
        go.transform.SetParent(parent, false);
        rootRT = go.AddComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 0.5f); // centered on canvas
        rootRT.sizeDelta = Vector2.zero;

        float r   = markerRadius;
        float t   = 3f;
        float len = r * 1.6f;

        var imgs = new Image[6];
        imgs[0] = AddRect(rootRT, "H",  new Vector2(len * 2, t), Vector2.zero);      // h-line
        imgs[1] = AddRect(rootRT, "V",  new Vector2(t, len * 2), Vector2.zero);      // v-line
        imgs[2] = AddRect(rootRT, "BT", new Vector2(r * 2, t),   new Vector2(0,  r)); // top border
        imgs[3] = AddRect(rootRT, "BB", new Vector2(r * 2, t),   new Vector2(0, -r)); // bottom border
        imgs[4] = AddRect(rootRT, "BL", new Vector2(t, r * 2),   new Vector2(-r, 0)); // left border
        imgs[5] = AddRect(rootRT, "BR", new Vector2(t, r * 2),   new Vector2( r, 0)); // right border
        return imgs;
    }

    Image AddRect(Transform parent, string n, Vector2 size, Vector2 offset)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
        return img;
    }

    void SetColor(Image[] imgs, Color c)
    {
        foreach (var img in imgs) img.color = c;
    }

    void Update()
    {
        if (tracker == null || _overlayRoot == null) return;

        bool detected  = tracker.IsRedDetected();
        Vector2 pos    = tracker.GetRedPosition();
        int pixelCount = tracker.GetRedPixelCount();
        bool isStereo  = splitScreen != null && splitScreen.enableStereo;

        Color c = detected ? detectedColor : lostColor;
        SetColor(_leftImgs,  c);
        SetColor(_rightImgs, c);

        // Canvas coordinate system with center anchor:
        //   (0,0) = screen center, x right, y up, units = screen pixels
        float hw      = Screen.width  * 0.5f;
        // pos.y: 0=bottom, 1=top — matches canvas Y direction
        float canvasY = (pos.y - 0.5f) * Screen.height;

        if (isStereo)
        {
            // Left eye  fills screen x [0,    hw] → canvas x [-hw, 0]
            // Right eye fills screen x [hw, width] → canvas x [0,  hw]
            _rightRT.gameObject.SetActive(true);
            _leftRT.anchoredPosition  = new Vector2(hw * (pos.x - 1f), canvasY);
            _rightRT.anchoredPosition = new Vector2(hw *  pos.x,        canvasY);
        }
        else
        {
            _rightRT.gameObject.SetActive(false);
            _leftRT.anchoredPosition = new Vector2((pos.x - 0.5f) * Screen.width, canvasY);
        }

        if (_hudText != null)
        {
            _hudText.color = detected ? Color.green : Color.gray;
            _hudText.text  = detected
                ? $"GREEN DOT  ({pos.x:F3}, {pos.y:F3})  px:{pixelCount}"
                : $"GREEN DOT: LOST  px:{pixelCount}";
        }

        // World-space marker
        if (_worldMarker != null && _cam != null)
        {
            _worldMarker.SetActive(detected);
            if (detected)
                _worldMarker.transform.position =
                    _cam.ViewportToWorldPoint(new Vector3(pos.x, pos.y, markerDepth));
        }
    }

    void OnDestroy()
    {
        if (_worldMarker != null) Destroy(_worldMarker);
        if (_overlayRoot != null) Destroy(_overlayRoot);
    }
}
