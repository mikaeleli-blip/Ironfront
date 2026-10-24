"""
Builds the France 1940 map data for Ironfront.

Output (Assets/StreamingAssets/Maps/France1940/):
  map.json     vectors in km: coastline, borders, rivers, lakes, cities, countries
  terrain.png  sim raster, 250 m cells. R = terrain code, G = country id.

Sources:
  Natural Earth 10m (public domain): land, coastline, countries, borders, lakes, rivers.
  Hand-entered (approximate, marked "approx"): rivers/canals missing from Natural Earth,
  terrain regions (forests, hills, marsh), city list.

Usage:
  python tools/build_map.py <natural-earth-geojson-dir>
Get the files from https://github.com/nvkelso/natural-earth-vector/tree/master/geojson
"""
import json, math, os, sys
from shapely.geometry import shape, box, Polygon, LineString, MultiLineString, mapping
from shapely import transform as _tf
from PIL import Image, ImageDraw

NE = sys.argv[1] if len(sys.argv) > 1 else "ne"
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "StreamingAssets", "Maps", "France1940")

# ---- Projection: equirectangular, origin at SW corner, km ----
LON0, LON1, LAT0, LAT1 = 0.0, 8.0, 47.5, 53.5
LATC = 50.5
KX = 111.32 * math.cos(math.radians(LATC))   # km per degree lon
KY = 110.57                                   # km per degree lat
W_KM = (LON1 - LON0) * KX
H_KM = (LAT1 - LAT0) * KY
CELLS_PER_KM = 4                               # 250 m cells
GW, GH = int(round(W_KM * CELLS_PER_KM)), int(round(H_KM * CELLS_PER_KM))

def km(lon, lat): return ((lon - LON0) * KX, (lat - LAT0) * KY)
def proj(geom): return _tf(geom, lambda a: (a - [LON0, LAT0]) * [KX, KY])
BOX = box(LON0, LAT0, LON1, LAT1)

# ---- Terrain codes (keep in sync with Ironfront.Sim.Terrain) ----
SEA, PLAIN, FOREST, HILLS, FOREST_HILLS, URBAN, MARSH, LAKE = range(8)

# ---- Countries (keep in sync with Ironfront.Sim.Country) ----
COUNTRIES = [  # id, NE ADM0_A3, name, side in May 1940
    (1, "DEU", "Germany", "Axis"),
    (2, "FRA", "France", "Allies"),
    (3, "BEL", "Belgium", "Allies"),
    (4, "NLD", "Netherlands", "Allies"),
    (5, "LUX", "Luxembourg", "Allies"),
    (6, "GBR", "United Kingdom", "Allies"),
    (7, "CHE", "Switzerland", "Neutral"),
]

def load(name): return json.load(open(os.path.join(NE, name + ".geojson"), encoding="utf-8"))["features"]

def lines_of(g):
    if g.is_empty: return []
    if isinstance(g, LineString): return [g]
    if isinstance(g, MultiLineString): return list(g.geoms)
    if hasattr(g, "geoms"): return [l for x in g.geoms for l in lines_of(x)]
    return []

def polys_of(g):
    if g.is_empty: return []
    if isinstance(g, Polygon): return [g]
    if hasattr(g, "geoms"): return [p for x in g.geoms for p in polys_of(x)]
    return []

def pts(line, tol):
    l = line.simplify(tol)
    return [round(c, 3) for xy in l.coords for c in xy]

# ---- Hand-entered rivers/canals missing from Natural Earth (lon, lat) — approximate ----
EXTRA_RIVERS = {
    "Albert Canal": [(4.40,51.24),(4.60,51.21),(4.83,51.17),(5.05,51.08),(5.20,51.00),(5.33,50.94),(5.55,50.92),(5.65,50.85),(5.66,50.76),(5.62,50.67)],
    "Dyle": [(4.42,50.58),(4.55,50.67),(4.60,50.72),(4.66,50.80),(4.70,50.88),(4.62,50.96),(4.48,51.03),(4.38,51.07)],
    "Sambre": [(3.78,49.98),(3.69,50.12),(3.80,50.19),(3.97,50.28),(4.17,50.34),(4.44,50.41),(4.66,50.44),(4.87,50.46)],
    "Oise": [(4.28,49.99),(4.08,49.92),(3.63,49.90),(3.36,49.66),(3.22,49.61),(3.00,49.58),(2.84,49.42)],
    "Lys": [(2.40,50.64),(2.65,50.66),(2.88,50.69),(3.12,50.80),(3.27,50.83),(3.53,50.98),(3.72,51.05)],
    "Semois": [(5.78,49.69),(5.55,49.71),(5.31,49.70),(5.07,49.79),(4.90,49.83),(4.73,49.88)],
}
RENAME = {"Aire": "Aisne"}  # Natural Earth's "Aire" line runs Aire -> Aisne -> lower Oise
MAJOR = {"Rhine", "Rhin", "Waal", "Maas", "Seine", "Lek", "Nederrijn", "IJssel", "Schelde", "Mosel", "Loire"}

# ---- Terrain regions (lon, lat polygons) — approximate, to be refined with real elevation/forest data ----
REGIONS = [
    ("Ardennes", FOREST_HILLS, [(4.40,49.85),(4.80,49.75),(5.30,49.65),(5.80,49.60),(6.00,49.90),(6.10,50.35),(5.80,50.50),(5.00,50.45),(4.40,50.25)]),
    ("Eifel", FOREST_HILLS, [(6.10,50.20),(6.40,49.95),(6.90,49.95),(7.20,50.25),(7.00,50.55),(6.10,50.55)]),
    ("Hunsrück", FOREST_HILLS, [(6.60,49.60),(7.00,49.60),(7.90,49.85),(7.80,50.05),(6.60,49.85)]),
    ("Pfälzerwald", FOREST_HILLS, [(7.50,49.05),(8.00,49.05),(8.00,49.50),(7.40,49.50)]),
    ("Vosges", FOREST_HILLS, [(6.60,48.20),(6.80,47.80),(7.20,47.75),(7.45,48.30),(7.25,48.75),(6.70,48.60)]),
    ("Black Forest", FOREST_HILLS, [(7.60,47.60),(8.00,47.60),(8.00,48.80),(7.75,48.80)]),
    ("Sauerland", FOREST_HILLS, [(7.20,50.80),(8.00,50.80),(8.00,51.30),(7.30,51.30)]),
    ("Westerwald", HILLS, [(7.60,50.40),(8.00,50.40),(8.00,50.80),(7.60,50.80)]),
    ("Argonne", FOREST, [(4.90,48.95),(5.15,48.95),(5.10,49.40),(4.88,49.40)]),
    ("Plateau de Langres", HILLS, [(4.60,47.50),(5.60,47.50),(5.60,48.00),(4.60,48.00)]),
    ("Forêt de Compiègne", FOREST, [(2.80,49.30),(3.05,49.30),(3.05,49.42),(2.80,49.42)]),
    ("Peel marshes", MARSH, [(5.70,51.25),(6.00,51.25),(6.00,51.55),(5.60,51.55)]),
]

# ---- Cities: name, lon, lat, tier (1 capital/metropolis, 2 city, 3 town of note) ----
CITIES = [
    ("Paris",2.35,48.86,1),("Brussels",4.35,50.85,1),("Amsterdam",4.90,52.37,1),("The Hague",4.30,52.08,1),("Luxembourg",6.13,49.61,1),
    ("Rotterdam",4.48,51.92,1),("Antwerp",4.40,51.22,1),("Cologne",6.96,50.94,1),("Lille",3.06,50.63,1),
    ("Ghent",3.72,51.05,2),("Liège",5.57,50.63,2),("Namur",4.87,50.47,2),("Charleroi",4.44,50.41,2),("Mons",3.95,50.45,2),("Leuven",4.70,50.88,2),
    ("Utrecht",5.12,52.09,2),("Arnhem",5.90,51.98,2),("Nijmegen",5.86,51.84,2),("Breda",4.78,51.59,2),("Maastricht",5.69,50.85,2),
    ("Aachen",6.08,50.78,2),("Düsseldorf",6.78,51.23,2),("Essen",7.01,51.46,2),("Duisburg",6.76,51.43,2),("Münster",7.63,51.96,2),("Bonn",7.10,50.73,2),
    ("Koblenz",7.59,50.36,2),("Trier",6.64,49.75,2),("Saarbrücken",7.00,49.24,2),("Freiburg",7.85,48.00,2),("Basel",7.59,47.56,2),
    ("Strasbourg",7.75,48.58,2),("Metz",6.18,49.12,2),("Nancy",6.18,48.69,2),("Reims",4.03,49.26,2),("Amiens",2.30,49.89,2),("Rouen",1.10,49.44,2),
    ("Le Havre",0.11,49.49,2),("Dunkirk",2.38,51.03,2),("Calais",1.86,50.95,2),("Boulogne",1.61,50.73,2),("Arras",2.78,50.29,2),
    ("Orléans",1.91,47.90,2),("Mulhouse",7.34,47.75,2),("Dover",1.31,51.13,2),
    ("Sedan",4.94,49.70,3),("Dinant",4.91,50.26,3),("Monthermé",4.73,49.88,3),("Charleville-Mézières",4.72,49.77,3),("Stonne",4.92,49.55,3),
    ("Montcornet",4.02,49.69,3),("Abbeville",1.83,50.11,3),("Cambrai",3.24,50.18,3),("Saint-Quentin",3.29,49.85,3),("Laon",3.62,49.56,3),
    ("Le Cateau",3.54,50.10,3),("Verdun",5.38,49.16,3),("Longwy",5.76,49.52,3),("Thionville",6.17,49.36,3),("Gembloux",4.69,50.56,3),
    ("Hannut",5.08,50.67,3),("Eben-Emael",5.68,50.80,3),("Wavre",4.61,50.72,3),("Bastogne",5.72,50.00,3),("Ypres",2.88,50.85,3),
    ("Ostend",2.92,51.23,3),("Nieuwpoort",2.75,51.13,3),("Vlissingen",3.57,51.44,3),("Bergen op Zoom",4.29,51.49,3),("Moerdijk",4.63,51.70,3),
    ("Grebbeberg",5.60,51.95,3),("Soissons",3.32,49.38,3),("Compiègne",2.83,49.42,3),("Beauvais",2.08,49.43,3),("Château-Thierry",3.40,49.05,3),
    ("Châlons",4.37,48.96,3),("Saint-Dizier",4.95,48.64,3),("Bar-le-Duc",5.16,48.77,3),("Troyes",4.08,48.30,3),("Épinal",6.45,48.17,3),
    ("Belfort",6.86,47.64,3),("Chartres",1.49,48.45,3),("Ramsgate",1.42,51.33,3),
]
URBAN_R = {1: 4.0, 2: 2.0, 3: 0.8}  # km
URBAN_R_OVERRIDE = {"Paris": 8.0}

def main():
    os.makedirs(OUT, exist_ok=True)
    out = {"name": "France 1940", "lonMin": LON0, "lonMax": LON1, "latMin": LAT0, "latMax": LAT1,
           "latCenter": LATC, "kmPerDegLon": KX, "kmPerDegLat": KY, "widthKm": W_KM, "heightKm": H_KM,
           "cellsPerKm": CELLS_PER_KM, "gridW": GW, "gridH": GH,
           "countries": [{"id": i, "code": c, "name": n, "side": s} for i, c, n, s in COUNTRIES],
           "coast": [], "borders": [], "rivers": [], "lakes": [], "cities": [], "regions": []}

    # Raster: draw at grid resolution, y flipped (row 0 = north) for PNG, Unity flips back on load
    img = Image.new("RGB", (GW, GH), (SEA, 0, 0))
    d = ImageDraw.Draw(img)
    def px(x, y): return (x * CELLS_PER_KM, (H_KM - y) * CELLS_PER_KM)
    def fill_poly(p, color):
        d.polygon([px(*c) for c in p.exterior.coords], fill=color)
        for hole in p.interiors: d.polygon([px(*c) for c in hole.coords], fill=(SEA, 0, 0))

    # Land base
    for ft in load("ne_10m_land"):
        g = shape(ft["geometry"]).intersection(BOX)
        for p in polys_of(proj(g)): fill_poly(p, (PLAIN, 0, 0))

    # Countries (land only: country id in G)
    code_to_id = {c: i for i, c, _, _ in COUNTRIES}
    for ft in load("ne_10m_admin_0_countries"):
        cid = code_to_id.get(ft["properties"].get("ADM0_A3"))
        if not cid: continue
        g = shape(ft["geometry"]).intersection(BOX)
        for p in polys_of(proj(g)):
            d.polygon([px(*c) for c in p.exterior.coords], fill=(PLAIN, cid, 0))

    # Terrain regions (keep country id)
    arr = img.load()
    for name, code, ll in REGIONS:
        poly = Polygon([km(*c) for c in ll])
        out["regions"].append({"name": name, "terrain": code, "approx": True, "pts": [round(v, 2) for xy in poly.exterior.coords for v in xy]})
        mask = Image.new("L", (GW, GH), 0)
        ImageDraw.Draw(mask).polygon([px(*c) for c in poly.exterior.coords], fill=1)
        mx0, my0, mx1, my1 = [int(v) for v in mask.getbbox() or (0, 0, 0, 0)]
        m = mask.load()
        for yy in range(my0, my1):
            for xx in range(mx0, mx1):
                if m[xx, yy] and arr[xx, yy][0] != SEA:
                    arr[xx, yy] = (code, arr[xx, yy][1], 0)

    # Lakes
    for ft in load("ne_10m_lakes"):
        g = shape(ft["geometry"])
        if not g.intersects(BOX): continue
        for p in polys_of(proj(g.intersection(BOX))):
            out["lakes"].append({"name": ft["properties"].get("name") or "", "pts": pts(p.exterior, 0.05)})
            cx, cy = p.representative_point().coords[0]
            d.polygon([px(*c) for c in p.exterior.coords], fill=(LAKE, 0, 0))

    # Urban
    for name, lon, lat, tier in CITIES:
        x, y = km(lon, lat)
        r = URBAN_R_OVERRIDE.get(name, URBAN_R[tier])
        cx, cy = px(x, y); rr = r * CELLS_PER_KM
        gx, gy = int(cx), int(cy)
        cid = arr[min(max(gx, 0), GW - 1), min(max(gy, 0), GH - 1)][1]
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=(URBAN, cid, 0))
        out["cities"].append({"name": name, "x": round(x, 2), "y": round(y, 2), "tier": tier, "country": cid})

    # Coastline
    for ft in load("ne_10m_coastline"):
        for l in lines_of(proj(shape(ft["geometry"]).intersection(BOX))):
            out["coast"].append({"pts": pts(l, 0.05)})

    # Land borders between our countries
    for ft in load("ne_10m_admin_0_boundary_lines_land"):
        for l in lines_of(proj(shape(ft["geometry"]).intersection(BOX))):
            out["borders"].append({"pts": pts(l, 0.05)})

    # Rivers
    seen = []
    for fname in ("ne_10m_rivers_lake_centerlines", "ne_10m_rivers_europe"):
        for ft in load(fname):
            p = ft["properties"]
            if p.get("featurecla") != "River": continue
            g = shape(ft["geometry"])
            if not g.intersects(BOX): continue
            name = RENAME.get(p.get("name"), p.get("name") or "")
            for l in lines_of(proj(g.intersection(BOX))):
                if l.length < 2: continue
                out["rivers"].append({"name": name, "major": name in MAJOR, "approx": False, "pts": pts(l, 0.05)})
    for name, ll in EXTRA_RIVERS.items():
        out["rivers"].append({"name": name, "major": False, "approx": True, "canal": "Canal" in name, "pts": pts(LineString([km(*c) for c in ll]), 0.01)})

    img.save(os.path.join(OUT, "terrain.png"), optimize=True)
    with open(os.path.join(OUT, "map.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, separators=(",", ":"))
    print(f"grid {GW}x{GH}  {W_KM:.1f}x{H_KM:.1f} km  rivers {len(out['rivers'])}  coast {len(out['coast'])}  borders {len(out['borders'])}  cities {len(out['cities'])}")

if __name__ == "__main__":
    main()
