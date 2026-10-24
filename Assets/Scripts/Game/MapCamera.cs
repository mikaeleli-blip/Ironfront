using UnityEngine;

namespace Ironfront.Game
{
    /// <summary>
    /// Orthographic map camera. World units are kilometres (x east, y north).
    /// Pan: WASD / arrows, middle- or right-drag, screen edges (optional). Zoom: mouse wheel (toward cursor), +/- keys.
    /// Draws the km grid with GL in OnPostRender.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCamera : MonoBehaviour
    {
        public const float MinHalfHeightKm = 2.5f;
        public const float MaxHalfHeightKm = 360f;

        public Camera Cam { get; private set; }
        public float MapW { get; private set; }
        public float MapH { get; private set; }
        public bool ShowGrid = true;
        public bool EdgeScroll = false;

        /// <summary>Raised whenever the zoom changes, so line widths can follow.</summary>
        public event System.Action Zoomed;

        float targetSize;
        Vector3 dragAnchorWorld;
        bool dragging;
        Material glMat;

        public float KmPerPixel { get { return Cam.orthographicSize * 2f / Mathf.Max(1, Screen.height); } }

        void Awake()
        {
            Cam = GetComponent<Camera>();
            Cam.orthographic = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color32(52, 74, 96, 255);
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            transform.position = new Vector3(0, 0, -10);
            transform.rotation = Quaternion.identity;

            var shader = Shader.Find("Hidden/Internal-Colored");
            glMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            glMat.SetInt("_ZWrite", 0);
        }

        public void SetBounds(float w, float h) { MapW = w; MapH = h; }

        public void LookAt(Vector2 km, float halfHeightKm)
        {
            transform.position = new Vector3(km.x, km.y, -10);
            Cam.orthographicSize = targetSize = Mathf.Clamp(halfHeightKm, MinHalfHeightKm, MaxHalfHeightKm);
            Clamp();
            if (Zoomed != null) Zoomed();
        }

        public Vector2 ScreenToKm(Vector2 screen)
        {
            var w = Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            return new Vector2(w.x, w.y);
        }

        public Vector2 KmToScreen(Vector2 km)
        {
            var s = Cam.WorldToScreenPoint(new Vector3(km.x, km.y, 0));
            return new Vector2(s.x, s.y);
        }

        /// <summary>Visible rectangle in km.</summary>
        public Rect ViewKm
        {
            get
            {
                float h = Cam.orthographicSize, w = h * Cam.aspect;
                var p = transform.position;
                return new Rect(p.x - w, p.y - h, 2 * w, 2 * h);
            }
        }

        /// <summary>True while the mouse is over HUD chrome; the HUD sets this each frame.</summary>
        public bool PointerBlocked { get; set; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float size = Cam.orthographicSize;

            // Keyboard pan (ignored when a modifier is held, so Ctrl/Shift combos stay free)
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            {
                var v = Vector2.zero;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1;
                if (EdgeScroll && Application.isFocused)
                {
                    var m = Input.mousePosition; const float e = 6;
                    if (m.x <= e) v.x -= 1; else if (m.x >= Screen.width - e) v.x += 1;
                    if (m.y <= e) v.y -= 1; else if (m.y >= Screen.height - e) v.y += 1;
                }
                if (v != Vector2.zero)
                {
                    float speed = size * (Input.GetKey(KeyCode.LeftShift) ? 2.4f : 1.2f);
                    transform.position += (Vector3)(v.normalized * speed * dt);
                }
            }

            // Drag pan (middle or right button): keep the grabbed point under the cursor
            if (Input.GetMouseButtonDown(2) || (Input.GetMouseButtonDown(1) && !PointerBlocked))
            {
                dragging = true;
                dragAnchorWorld = Cam.ScreenToWorldPoint(Input.mousePosition);
            }
            if (dragging && (Input.GetMouseButton(2) || Input.GetMouseButton(1)))
            {
                var now = Cam.ScreenToWorldPoint(Input.mousePosition);
                transform.position += dragAnchorWorld - now;
            }
            else dragging = false;

            // Zoom toward the cursor, smoothed
            float wheel = Input.mouseScrollDelta.y;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) wheel += 2;
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) wheel -= 2;
            if (Mathf.Abs(wheel) > 0.01f && !PointerBlocked)
                targetSize = Mathf.Clamp(targetSize * Mathf.Pow(0.85f, wheel), MinHalfHeightKm, MaxHalfHeightKm);

            if (Mathf.Abs(size - targetSize) > 0.0005f * targetSize)
            {
                var before = Cam.ScreenToWorldPoint(Input.mousePosition);
                Cam.orthographicSize = Mathf.Lerp(size, targetSize, 1f - Mathf.Exp(-14f * dt));
                if (Mathf.Abs(Cam.orthographicSize - targetSize) < 0.001f * targetSize) Cam.orthographicSize = targetSize;
                var after = Cam.ScreenToWorldPoint(Input.mousePosition);
                transform.position += before - after;
                if (Zoomed != null) Zoomed();
            }

            Clamp();
        }

        void Clamp()
        {
            if (MapW <= 0) return;
            float h = Cam.orthographicSize, w = h * Cam.aspect;
            float mx = Mathf.Min(w, 40f), my = Mathf.Min(h, 40f);   // allow a little sea margin past the edge
            var p = transform.position;
            p.x = 2 * w >= MapW + 2 * mx ? MapW / 2 : Mathf.Clamp(p.x, w - mx, MapW - w + mx);
            p.y = 2 * h >= MapH + 2 * my ? MapH / 2 : Mathf.Clamp(p.y, h - my, MapH - h + my);
            transform.position = p;
        }

        /// <summary>Grid spacing in km for the current zoom: aims for roughly 60–150 px between lines.</summary>
        public float GridStepKm
        {
            get
            {
                float kmpp = KmPerPixel;
                float[] steps = { 1, 2, 5, 10, 25, 50, 100 };
                foreach (var s in steps) if (s / kmpp >= 60) return s;
                return 100;
            }
        }

        void OnPostRender()
        {
            if (!ShowGrid || MapW <= 0) return;
            var r = ViewKm;
            float step = GridStepKm;
            float x0 = Mathf.Max(0, Mathf.Floor(r.xMin / step) * step), x1 = Mathf.Min(MapW, r.xMax);
            float y0 = Mathf.Max(0, Mathf.Floor(r.yMin / step) * step), y1 = Mathf.Min(MapH, r.yMax);
            float yb = Mathf.Max(0, r.yMin), yt = Mathf.Min(MapH, r.yMax);
            float xl = Mathf.Max(0, r.xMin), xr = Mathf.Min(MapW, r.xMax);

            glMat.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(GL.LINES);
            for (float x = x0; x <= x1; x += step)
            {
                bool major = Mathf.Abs(x / (step * 5) - Mathf.Round(x / (step * 5))) < 0.001f;
                GL.Color(new Color(0.1f, 0.1f, 0.12f, major ? 0.28f : 0.13f));
                GL.Vertex3(x, yb, -1); GL.Vertex3(x, yt, -1);
            }
            for (float y = y0; y <= y1; y += step)
            {
                bool major = Mathf.Abs(y / (step * 5) - Mathf.Round(y / (step * 5))) < 0.001f;
                GL.Color(new Color(0.1f, 0.1f, 0.12f, major ? 0.28f : 0.13f));
                GL.Vertex3(xl, y, -1); GL.Vertex3(xr, y, -1);
            }
            GL.End();
            GL.PopMatrix();
        }
    }
}
