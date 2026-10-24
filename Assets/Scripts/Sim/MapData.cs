using System;

namespace Ironfront.Sim
{
    /// <summary>Terrain codes stored in the R channel of terrain.png. Keep in sync with tools/build_map.py.</summary>
    public enum Terrain : byte { Sea = 0, Plain = 1, Forest = 2, Hills = 3, ForestHills = 4, Urban = 5, Marsh = 6, Lake = 7 }

    public enum Side { Neutral, Allies, Axis }

    // ---- map.json (all coordinates in km from the map's south-west corner, x east, y north) ----

    [Serializable]
    public sealed class MapFile
    {
        public string name;
        public float lonMin, lonMax, latMin, latMax, latCenter;
        public float kmPerDegLon, kmPerDegLat, widthKm, heightKm;
        public int cellsPerKm, gridW, gridH;
        public CountryDef[] countries;
        public Polyline[] coast;
        public Polyline[] borders;
        public RiverDef[] rivers;
        public AreaDef[] lakes;
        public AreaDef[] regions;
        public CityDef[] cities;

        public double Lon(float xKm) { return lonMin + xKm / kmPerDegLon; }
        public double Lat(float yKm) { return latMin + yKm / kmPerDegLat; }

        public CountryDef Country(int id)
        {
            if (countries != null) foreach (var c in countries) if (c.id == id) return c;
            return null;
        }
    }

    [Serializable]
    public sealed class CountryDef
    {
        public int id;
        public string code, name, side;
        public Side Side { get { return side == "Axis" ? Side.Axis : side == "Allies" ? Side.Allies : Side.Neutral; } }
    }

    [Serializable] public sealed class Polyline { public float[] pts; }

    [Serializable]
    public sealed class RiverDef
    {
        public string name;
        public bool major, approx, canal;
        public float[] pts;
    }

    [Serializable]
    public sealed class AreaDef
    {
        public string name;
        public int terrain;
        public bool approx;
        public float[] pts;
    }

    [Serializable]
    public sealed class CityDef
    {
        public string name;
        public float x, y;
        /// <summary>1 = capital/metropolis, 2 = city, 3 = town of operational note.</summary>
        public int tier;
        public int country;
    }

    /// <summary>The sim raster: terrain type and country per cell. Row 0 is the southern edge.</summary>
    public sealed class TerrainGrid
    {
        public readonly int W, H, CellsPerKm;
        readonly byte[] terrain, country;

        public TerrainGrid(int w, int h, int cellsPerKm, byte[] terrain, byte[] country)
        {
            if (terrain.Length != w * h || country.Length != w * h) throw new ArgumentException("grid size mismatch");
            W = w; H = h; CellsPerKm = cellsPerKm;
            this.terrain = terrain; this.country = country;
        }

        public float WidthKm { get { return (float)W / CellsPerKm; } }
        public float HeightKm { get { return (float)H / CellsPerKm; } }

        public bool InBounds(float xKm, float yKm) { return xKm >= 0 && yKm >= 0 && xKm < WidthKm && yKm < HeightKm; }

        int Index(float xKm, float yKm)
        {
            int cx = (int)(xKm * CellsPerKm), cy = (int)(yKm * CellsPerKm);
            if (cx < 0) cx = 0; else if (cx >= W) cx = W - 1;
            if (cy < 0) cy = 0; else if (cy >= H) cy = H - 1;
            return cy * W + cx;
        }

        public Terrain TerrainAt(float xKm, float yKm) { return (Terrain)terrain[Index(xKm, yKm)]; }
        public int CountryAt(float xKm, float yKm) { return country[Index(xKm, yKm)]; }
        public Terrain TerrainAtCell(int i) { return (Terrain)terrain[i]; }
        public int CountryAtCell(int i) { return country[i]; }

        public static string Name(Terrain t)
        {
            switch (t)
            {
                case Terrain.Sea: return "Sea";
                case Terrain.Plain: return "Open ground";
                case Terrain.Forest: return "Forest";
                case Terrain.Hills: return "Hills";
                case Terrain.ForestHills: return "Wooded hills";
                case Terrain.Urban: return "Urban";
                case Terrain.Marsh: return "Marsh";
                case Terrain.Lake: return "Lake";
                default: return t.ToString();
            }
        }
    }
}
