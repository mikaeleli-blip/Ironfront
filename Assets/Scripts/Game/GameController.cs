using Ironfront.Sim;
using UnityEngine;

namespace Ironfront.Game
{
    /// <summary>
    /// Boots the game when you press Play (the scene only needs to exist), owns the clock, the map and the HUD,
    /// and handles global hotkeys.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameController : MonoBehaviour
    {
        public const string MapName = "France1940";
        /// <summary>Opening view: the Ardennes–Meuse sector.</summary>
        static readonly Vector2 HomeLonLat = new Vector2(4.9f, 50.0f);
        const float HomeHalfHeightKm = 140f;

        public static GameController Instance { get; private set; }

        public GameClock Clock { get; private set; }
        public MapView Map { get; private set; }
        public MapCamera Cam { get; private set; }
        public Hud Hud { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Ironfront").AddComponent<GameController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;

            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }
            Cam = camera.GetComponent<MapCamera>();
            if (Cam == null) Cam = camera.gameObject.AddComponent<MapCamera>();

            Clock = new GameClock();

            Map = new GameObject("Map").AddComponent<MapView>();
            Map.Load(MapName, Cam);

            Hud = gameObject.AddComponent<Hud>();
            Hud.Bind(this);

            Home();
        }

        void Home()
        {
            var d = Map.Data;
            float x = (HomeLonLat.x - d.lonMin) * d.kmPerDegLon;
            float y = (HomeLonLat.y - d.latMin) * d.kmPerDegLat;
            Cam.LookAt(new Vector2(x, y), HomeHalfHeightKm);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) Clock.TogglePause();
            for (int s = 1; s <= GameClock.MaxSpeed; s++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + s) || Input.GetKeyDown(KeyCode.Keypad0 + s)) Clock.SetSpeed(s);
            if (Input.GetKeyDown(KeyCode.T)) Map.SetMode(MapMode.Terrain);
            if (Input.GetKeyDown(KeyCode.P)) Map.SetMode(MapMode.Political);
            if (Input.GetKeyDown(KeyCode.G)) Cam.ShowGrid = !Cam.ShowGrid;
            if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.Home)) Home();
            if (Input.GetKeyDown(KeyCode.F1)) Hud.ShowHelp = !Hud.ShowHelp;

            Clock.Advance(Time.unscaledDeltaTime);
        }
    }
}
