using Ironfront.Sim;
using UnityEngine;

namespace Ironfront.Game
{
    /// <summary>
    /// IMGUI overlay: night shade, map labels (cities, regions), top bar (date, time controls, map mode),
    /// cursor readout and scale bar. Placeholder look until the real UI pass.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        GameController gc;
        GUIStyle smallCenter, bar, barText, barTextBold, btn, btnOn, cityStyle1, cityStyle2, cityStyle3, regionStyle, shadowStyle, small, help;
        Texture2D white;
        bool stylesReady;
        bool showHelp;

        static readonly Color Panel = new Color(0.10f, 0.11f, 0.10f, 0.88f);
        static readonly Color Ink = new Color(0.93f, 0.91f, 0.84f);
        static readonly Color Accent = new Color(0.86f, 0.70f, 0.36f);

        const float TopH = 34f;

        public void Bind(GameController controller) { gc = controller; }

        public bool ShowHelp { get { return showHelp; } set { showHelp = value; } }

        void Styles()
        {
            if (stylesReady) return;
            white = Texture2D.whiteTexture;

            bar = new GUIStyle { normal = { background = white } };
            barText = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, normal = { textColor = Ink } };
            barTextBold = new GUIStyle(barText) { fontStyle = FontStyle.Bold, fontSize = 15 };
            small = new GUIStyle(barText) { fontSize = 12 };
            smallCenter = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            help = new GUIStyle(small) { alignment = TextAnchor.UpperLeft, wordWrap = false, richText = true };

            btn = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            btn.normal.textColor = Ink; btn.hover.textColor = Color.white;
            btnOn = new GUIStyle(btn);
            btnOn.normal.textColor = Accent; btnOn.hover.textColor = Accent;

            cityStyle1 = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.12f, 0.10f, 0.09f) } };
            cityStyle2 = new GUIStyle(cityStyle1) { fontSize = 13 };
            cityStyle3 = new GUIStyle(cityStyle1) { fontSize = 12, fontStyle = FontStyle.Normal };
            shadowStyle = new GUIStyle(cityStyle1);
            regionStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.22f, 0.28f, 0.16f, 0.75f) } };

            stylesReady = true;
        }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (gc == null || gc.Map == null || gc.Map.Data == null) return;
            Styles();

            var cam = gc.Cam;
            var mouse = Event.current.mousePosition;   // GUI space (y down)

            // Night shade over the map, under the HUD
            float dark = gc.Clock.Darkness;
            if (dark > 0.01f) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.06f, 0.16f, 0.30f * dark));

            DrawRegionLabels(cam);
            DrawCities(cam);
            DrawTopBar();
            DrawCursorInfo(cam);
            DrawScaleBar(cam);
            if (showHelp) DrawHelp();

            // Let the camera know when the pointer sits on HUD chrome
            if (Event.current.type == EventType.Repaint)
                cam.PointerBlocked = mouse.y < TopH || (showHelp && helpRect.Contains(mouse));
        }

        Vector2 GuiPoint(MapCamera cam, float x, float y)
        {
            var s = cam.KmToScreen(new Vector2(x, y));
            return new Vector2(s.x, Screen.height - s.y);
        }

        // ---------- Labels ----------

        void DrawCities(MapCamera cam)
        {
            float kmpp = cam.KmPerPixel;
            var view = cam.ViewKm;
            foreach (var c in gc.Map.Data.cities)
            {
                // Level of detail: capitals always, cities below ~0.55 km/px, towns below ~0.22 km/px
                if (c.tier == 2 && kmpp > 0.55f) continue;
                if (c.tier == 3 && kmpp > 0.22f) continue;
                if (c.x < view.xMin - 5 || c.x > view.xMax + 5 || c.y < view.yMin - 5 || c.y > view.yMax + 5) continue;

                var p = GuiPoint(cam, c.x, c.y);
                float d = c.tier == 1 ? 9 : c.tier == 2 ? 7 : 5;
                Fill(new Rect(p.x - d / 2 - 1, p.y - d / 2 - 1, d + 2, d + 2), new Color(0.95f, 0.93f, 0.86f));
                Fill(new Rect(p.x - d / 2, p.y - d / 2, d, d), new Color(0.15f, 0.12f, 0.12f));

                var st = c.tier == 1 ? cityStyle1 : c.tier == 2 ? cityStyle2 : cityStyle3;
                var r = new Rect(p.x + d / 2 + 4, p.y - 11, 220, 22);
                // Light halo so labels read over any terrain
                shadowStyle.fontSize = st.fontSize; shadowStyle.fontStyle = st.fontStyle;
                shadowStyle.normal.textColor = new Color(0.97f, 0.95f, 0.88f, 0.9f);
                for (int i = 0; i < 4; i++)
                {
                    var o = r; o.x += (i & 1) == 0 ? -1 : 1; o.y += (i & 2) == 0 ? -1 : 1;
                    GUI.Label(o, c.name, shadowStyle);
                }
                GUI.Label(r, c.name, st);
            }
        }

        void DrawRegionLabels(MapCamera cam)
        {
            float kmpp = cam.KmPerPixel;
            if (kmpp > 0.9f || kmpp < 0.04f || gc.Map.Data.regions == null) return;
            foreach (var reg in gc.Map.Data.regions)
            {
                if (reg.pts == null || reg.pts.Length < 6) continue;
                float sx = 0, sy = 0; int n = reg.pts.Length / 2;
                for (int i = 0; i < n; i++) { sx += reg.pts[2 * i]; sy += reg.pts[2 * i + 1]; }
                var p = GuiPoint(cam, sx / n, sy / n);
                GUI.Label(new Rect(p.x - 120, p.y - 12, 240, 24), reg.name.ToUpperInvariant(), regionStyle);
            }
        }

        // ---------- Top bar ----------

        void DrawTopBar()
        {
            Fill(new Rect(0, 0, Screen.width, TopH), Panel);
            Fill(new Rect(0, TopH, Screen.width, 1), new Color(0, 0, 0, 0.6f));

            var clock = gc.Clock;
            float x = 12;
            GUI.Label(new Rect(x, 0, 120, TopH), "IRONFRONT", barTextBold);
            x += 118;

            GUI.Label(new Rect(x, 0, 260, TopH), clock.Now.ToString("ddd d MMM yyyy  HH:mm", System.Globalization.CultureInfo.InvariantCulture), barTextBold);
            x += 230;

            string phase = clock.Darkness >= 0.99f ? "Night" : clock.Darkness <= 0.01f ? "Day" : clock.Now.Hour < 12 ? "Dawn" : "Dusk";
            GUI.Label(new Rect(x, 0, 60, TopH), phase, small);
            x += 54;

            if (GUI.Button(new Rect(x, 5, 74, TopH - 10), clock.Paused ? "Play" : "Pause", clock.Paused ? btnOn : btn)) clock.TogglePause();
            x += 80;
            for (int s = 1; s <= GameClock.MaxSpeed; s++)
            {
                if (GUI.Button(new Rect(x, 5, 26, TopH - 10), s.ToString(), clock.Speed == s ? btnOn : btn)) clock.SetSpeed(s);
                x += 28;
            }
            x += 14;

            var map = gc.Map;
            if (GUI.Button(new Rect(x, 5, 78, TopH - 10), "Terrain", map.Mode == MapMode.Terrain ? btnOn : btn)) map.SetMode(MapMode.Terrain);
            x += 80;
            if (GUI.Button(new Rect(x, 5, 78, TopH - 10), "Political", map.Mode == MapMode.Political ? btnOn : btn)) map.SetMode(MapMode.Political);
            x += 80;
            if (GUI.Button(new Rect(x, 5, 56, TopH - 10), "Grid", gc.Cam.ShowGrid ? btnOn : btn)) gc.Cam.ShowGrid = !gc.Cam.ShowGrid;
            x += 58;

            if (GUI.Button(new Rect(Screen.width - 40, 5, 30, TopH - 10), "?", showHelp ? btnOn : btn)) showHelp = !showHelp;
        }

        // ---------- Cursor readout ----------

        void DrawCursorInfo(MapCamera cam)
        {
            var km = cam.ScreenToKm(Input.mousePosition);
            var grid = gc.Map.Grid;
            string line1, line2;
            if (grid.InBounds(km.x, km.y))
            {
                var data = gc.Map.Data;
                double lat = data.Lat(km.y), lon = data.Lon(km.x);
                var ter = grid.TerrainAt(km.x, km.y);
                var country = data.Country(grid.CountryAt(km.x, km.y));
                line1 = string.Format("{0:0.000}° {1}  {2:0.000}° {3}    ({4:0.0}, {5:0.0}) km",
                    System.Math.Abs(lat), lat >= 0 ? "N" : "S", System.Math.Abs(lon), lon >= 0 ? "E" : "W", km.x, km.y);
                line2 = TerrainGrid.Name(ter) + (country != null ? "  ·  " + country.name : "");
            }
            else { line1 = "Off map"; line2 = ""; }

            var r = new Rect(10, Screen.height - 54, 380, 44);
            Fill(r, Panel);
            GUI.Label(new Rect(r.x + 10, r.y + 2, r.width - 20, 20), line1, small);
            GUI.Label(new Rect(r.x + 10, r.y + 21, r.width - 20, 20), line2, barText);
        }

        // ---------- Scale bar ----------

        void DrawScaleBar(MapCamera cam)
        {
            float kmpp = cam.KmPerPixel;
            float[] nice = { 1, 2, 5, 10, 20, 25, 50, 100, 200 };
            float km = nice[0];
            foreach (var n in nice) { if (n / kmpp <= 180) km = n; }
            float px = km / kmpp;

            var r = new Rect(Screen.width - px - 36, Screen.height - 46, px + 26, 36);
            Fill(r, Panel);
            float bx = r.x + 13, by = r.y + 24;
            Fill(new Rect(bx, by, px, 3), Ink);
            Fill(new Rect(bx, by - 6, 2, 9), Ink);
            Fill(new Rect(bx + px - 2, by - 6, 2, 9), Ink);
            Fill(new Rect(bx + px / 2 - 1, by - 3, 2, 6), Ink);
            GUI.Label(new Rect(r.x, r.y + 1, r.width, 18), km + " km", smallCenter);
        }

        // ---------- Help ----------

        Rect helpRect;

        void DrawHelp()
        {
            const string text =
                "<b>Controls</b>\n" +
                "Pan            WASD / arrows, right- or middle-drag\n" +
                "Fast pan       hold Shift\n" +
                "Zoom           mouse wheel, + / −\n" +
                "Pause          Space\n" +
                "Speed          1 – 5\n" +
                "Map mode       T terrain,  P political\n" +
                "Grid           G\n" +
                "Home view      H\n" +
                "Help           F1";
            helpRect = new Rect(Screen.width - 330, TopH + 10, 320, 196);
            Fill(helpRect, Panel);
            GUI.Label(new Rect(helpRect.x + 12, helpRect.y + 8, helpRect.width - 24, helpRect.height - 16), text, help);
        }
    }
}
