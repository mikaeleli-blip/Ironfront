using System.Collections.Generic;
using System.IO;
using Ironfront.Sim;
using UnityEngine;

namespace Ironfront.Game
{
    public enum MapMode { Terrain, Political }

    /// <summary>
    /// Loads Maps/&lt;name&gt;/map.json + terrain.png from StreamingAssets, builds the TerrainGrid for the sim,
    /// and renders the base map: terrain texture on a km-sized quad, plus coast, borders, rivers and lakes as lines
    /// whose on-screen width stays constant while zooming.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        public MapFile Data { get; private set; }
        public TerrainGrid Grid { get; private set; }
        public MapMode Mode { get; private set; }
        public float WidthKm { get { return Grid.WidthKm; } }
        public float HeightKm { get { return Grid.HeightKm; } }

        MapCamera cam;
        Material spriteMat;
        Texture2D tex;
        Color32[] terrainPixels, politicalPixels;

        struct Line { public LineRenderer lr; public float px; }
        readonly List<Line> lines = new List<Line>();

        // Order on screen, back to front
        const int OrderTerrain = 0, OrderLake = 5, OrderRiverMinor = 10, OrderRiverMajor = 11, OrderCoast = 12, OrderBorder = 13;

        static readonly Color32[] TerrainColors =
        {
            new Color32( 92, 122, 148, 255), // Sea
            new Color32(222, 214, 178, 255), // Plain
            new Color32(150, 170, 112, 255), // Forest
            new Color32(204, 186, 142, 255), // Hills
            new Color32(136, 154, 102, 255), // Wooded hills
            new Color32(168, 150, 138, 255), // Urban
            new Color32(176, 192, 162, 255), // Marsh
            new Color32(120, 156, 184, 255), // Lake
        };

        /// <summary>Political colours by country id (index). 0 = no country.</summary>
        public static readonly Color32[] CountryColors =
        {
            new Color32(200, 200, 190, 255),
            new Color32(118, 118, 112, 255), // Germany
            new Color32( 86, 118, 186, 255), // France
            new Color32(214, 184,  80, 255), // Belgium
            new Color32(226, 136,  60, 255), // Netherlands
            new Color32(120, 176, 196, 255), // Luxembourg
            new Color32(184,  84,  76, 255), // United Kingdom
            new Color32(176, 130, 170, 255), // Switzerland
        };

        static readonly Color RiverColor = new Color32(70, 112, 160, 255);
        static readonly Color CanalColor = new Color32(80, 124, 170, 255);
        static readonly Color CoastColor = new Color32(48, 70, 92, 255);
        static readonly Color BorderColor = new Color32(70, 40, 40, 230);
        static readonly Color LakeEdgeColor = new Color32(60, 98, 140, 255);

        public void Load(string mapName, MapCamera camera)
        {
            cam = camera;
            string dir = Path.Combine(Application.streamingAssetsPath, "Maps", mapName);
            Data = JsonUtility.FromJson<MapFile>(File.ReadAllText(Path.Combine(dir, "map.json")));

            // Sim raster from the PNG: R = terrain, G = country. Texture row 0 is the bottom (south) row.
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            raw.LoadImage(File.ReadAllBytes(Path.Combine(dir, "terrain.png")));
            int w = raw.width, h = raw.height;
            var px = raw.GetPixels32();
            Destroy(raw);
            var t = new byte[w * h]; var c = new byte[w * h];
            for (int i = 0; i < px.Length; i++) { t[i] = px[i].r; c[i] = px[i].g; }
            Grid = new TerrainGrid(w, h, Data.cellsPerKm, t, c);

            spriteMat = new Material(Shader.Find("Sprites/Default"));
            BuildTexture();
            BuildQuad();
            BuildLines();

            cam.SetBounds(WidthKm, HeightKm);
            cam.Zoomed += UpdateLineWidths;
            UpdateLineWidths();
        }

        // ---------- Terrain texture ----------

        void BuildTexture()
        {
            int w = Grid.W, h = Grid.H;
            terrainPixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var ter = Grid.TerrainAtCell(i);
                    Color32 col = TerrainColors[(int)ter < TerrainColors.Length ? (int)ter : 1];
                    // Gentle mottling so open ground doesn't read as a flat fill
                    float n = Mathf.PerlinNoise(x * 0.012f, y * 0.012f) * 0.6f + Mathf.PerlinNoise(x * 0.05f + 31.7f, y * 0.05f + 11.3f) * 0.4f;
                    float k = ter == Terrain.Sea || ter == Terrain.Lake ? 0.97f + n * 0.05f : 0.93f + n * 0.1f;
                    terrainPixels[i] = Scale(col, k);
                }
            }

            tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.SetPixels32(terrainPixels);
            tex.Apply(true);
            Mode = MapMode.Terrain;
        }

        void BuildPolitical()
        {
            int n = Grid.W * Grid.H;
            politicalPixels = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                var ter = Grid.TerrainAtCell(i);
                if (ter == Terrain.Sea || ter == Terrain.Lake) { politicalPixels[i] = terrainPixels[i]; continue; }
                int cid = Grid.CountryAtCell(i);
                var cc = CountryColors[cid < CountryColors.Length ? cid : 0];
                // Country colour, lightly shaded by terrain so forests and hills still read
                politicalPixels[i] = Color32.Lerp(cc, terrainPixels[i], 0.28f);
            }
        }

        public void SetMode(MapMode mode)
        {
            if (mode == Mode) return;
            if (mode == MapMode.Political && politicalPixels == null) BuildPolitical();
            tex.SetPixels32(mode == MapMode.Political ? politicalPixels : terrainPixels);
            tex.Apply(true);
            Mode = mode;
        }

        static Color32 Scale(Color32 c, float k)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);
        }

        void BuildQuad()
        {
            float w = WidthKm, h = HeightKm;
            var mesh = new Mesh { name = "MapQuad" };
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(w, 0, 0), new Vector3(0, h, 0), new Vector3(w, h, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(spriteMat) { mainTexture = tex };
            mr.sharedMaterial = mat;
            mr.sortingOrder = OrderTerrain;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ---------- Vector lines ----------

        void BuildLines()
        {
            var root = new GameObject("Lines").transform;
            root.SetParent(transform, false);

            if (Data.lakes != null) foreach (var l in Data.lakes) AddLine(root, "Lake " + l.name, l.pts, LakeEdgeColor, 1.0f, OrderLake, true);
            if (Data.rivers != null)
                foreach (var r in Data.rivers)
                {
                    float px = r.major ? 2.6f : r.canal ? 1.4f : 1.7f;
                    AddLine(root, r.name, r.pts, r.canal ? CanalColor : RiverColor, px, r.major ? OrderRiverMajor : OrderRiverMinor, false);
                }
            if (Data.coast != null) foreach (var c in Data.coast) AddLine(root, "Coast", c.pts, CoastColor, 1.2f, OrderCoast, false);
            if (Data.borders != null) foreach (var b in Data.borders) AddLine(root, "Border", b.pts, BorderColor, 1.6f, OrderBorder, false);
        }

        void AddLine(Transform parent, string name, float[] pts, Color color, float widthPx, int order, bool loop)
        {
            if (pts == null || pts.Length < 4) return;
            int n = pts.Length / 2;
            var v = new Vector3[n];
            for (int i = 0; i < n; i++) v[i] = new Vector3(pts[2 * i], pts[2 * i + 1], 0);

            var go = new GameObject(string.IsNullOrEmpty(name) ? "Line" : name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = spriteMat;
            lr.startColor = lr.endColor = color;
            lr.positionCount = n;
            lr.SetPositions(v);
            lr.loop = loop;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.TransformZ;
            lr.sortingOrder = order;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lines.Add(new Line { lr = lr, px = widthPx });
        }

        float lastKmpp = -1;

        void UpdateLineWidths()
        {
            float kmpp = cam.KmPerPixel;
            if (Mathf.Abs(kmpp - lastKmpp) < 1e-5f * kmpp) return;
            lastKmpp = kmpp;
            // Lines thin slightly when zoomed far out so the map doesn't drown in ink
            float zoomScale = Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0.3f, 1.2f, kmpp));
            foreach (var l in lines) l.lr.widthMultiplier = l.px * zoomScale * kmpp;
        }
    }
}
