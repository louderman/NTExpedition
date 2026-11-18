"""Build the salvageshuttle layout for NTExpedition: copy sprites + emit TSV layout.

All SS14-specific resolution (icon smoothing, RSI cells, direction offsets, rotations, sorting)
happens here so the Unity side is a dumb placer.  TSV columns:

  label \t spriteAssetPath \t cellName \t posX \t posY \t rotDeg \t sortingOrder
"""
import csv
import json
import math
import os
import shutil

FR = "/home/dan/Documents/SS1/frontier-station-14/Resources/Textures"
PROJ = "/home/dan/Documents/gamedesignlab/NTExpedition"
DEST = os.path.join(PROJ, "Assets/Sprites/SS14/SalvageShuttle")
SHIPKIT = "Assets/Sprites/SS14/ShipKit"
NEW = "Assets/Sprites/SS14/SalvageShuttle"
SCRATCH = os.path.expanduser("~/.hermes/profiles/coder/cache/scratch")

COPY = [
    ("_NF/Structures/Walls/solid.rsi", "Walls/Reinforced", ["full"] + [f"solid{i}" for i in range(8)] + [f"reinf_over{i}" for i in range(8)]),
    ("_NF/Structures/Walls/reinforced_diagonal.rsi", "Walls/ReinforcedDiagonal", [f"state{i}" for i in range(5)]),
    ("_NF/Structures/Windows/reinforced_window.rsi", "Windows/Reinforced", ["full"] + [f"rwindow{i}" for i in range(8)]),
    ("_NF/Structures/Windows/reinforced_window_diagonal.rsi", "Windows/ReinforcedDiagonal", ["state0", "state1"]),
    ("Structures/catwalk.rsi", "Catwalk", [f"catwalk_{i}" for i in range(8)]),
    ("Structures/Walls/grille.rsi", "Grilles", ["grille", "grille_diagonal"]),
    ("Structures/Doors/Airlocks/Glass/shuttle.rsi", "Doors/GlassShuttle", ["closed"]),
    ("Structures/Doors/Airlocks/Standard/engineering.rsi", "Doors/Engineering", ["closed"]),
    ("Structures/Doors/Airlocks/Standard/salvage.rsi", "Doors/Salvage", ["closed"]),
    ("_NF/Structures/Doors/secret_door_reinforced.rsi", "Doors/SecretDoorReinforced", ["closed"]),
    ("_NF/Structures/Shuttles/thruster.rsi", "Shuttles/Thruster", ["base"]),
    ("_NF/Structures/Shuttles/gyroscope.rsi", "Shuttles/Gyroscope", ["base"]),
    ("_NF/Structures/Storage/canister.rsi", "Storage/Canister", ["nitrogen", "oxygen"]),
    ("Structures/Storage/closet.rsi", "Storage/Closet", ["generic", "generic_door"]),
    ("Structures/Power/Generation/portable_generator.rsi", "Power/PortableGenerator", ["portgen1"]),
    ("Structures/Power/smes.rsi", "Power/SMES", ["smes", "smes-oc0", "smes-op1"]),
    ("Structures/Power/substation.rsi", "Power/Substation", ["substation"]),
]
# Plain tile textures: no RSI meta.json beside them (Tiles/*, Tiles/Asteroid/*,
# Tiles/Planet/Concrete/*). 4-variant concrete sheets are 128x32 = 4 columns.
PLAIN = [
    ("Tiles", "Tiles", ["lattice"]),
    ("Tiles/Asteroid", "Tiles", ["asteroid_tile"]),
    ("Tiles/Planet/Concrete", "Tiles/Concrete", ["grayconcrete", "concrete", "oldconcrete"]),
]
# RSI metas consulted but whose PNGs are reused straight out of ShipKit
REUSE_META = {"Structures/Furniture/chairs.rsi": "Chairs", "Structures/Machines/computers.rsi": "Consoles"}

# ------------------------------------------------------------------ directions table
DIRS = {}       # (rsi_key, state_name) -> directions (int)


def load_dirs(rsi_rel, key):
    p = os.path.join(FR, rsi_rel, "meta.json")
    meta = json.load(open(p))
    for st in meta.get("states", []):
        DIRS[(key, st["name"])] = int(st.get("directions", 1))
    return meta


for src_rel, dst_rel, stems in COPY:
    meta = load_dirs(src_rel, src_rel)
    src_dir = os.path.join(FR, src_rel)
    dst_dir = os.path.join(DEST, dst_rel)
    os.makedirs(dst_dir, exist_ok=True)
    shutil.copy2(os.path.join(src_dir, "meta.json"), os.path.join(dst_dir, "source-meta.json"))
    for stem in stems:
        s = os.path.join(src_dir, stem + ".png")
        if not os.path.exists(s):
            print(f"!! MISSING {s}")
            continue
        shutil.copy2(s, os.path.join(dst_dir, stem + ".png"))
for rsi_rel in REUSE_META:
    load_dirs(rsi_rel, rsi_rel)   # DIRS is keyed by the raw RSI path used in P dicts

plain_sprites = []
for src_rel, dst_rel, stems in PLAIN:
    src_dir = os.path.join(FR, src_rel)
    dst_dir = os.path.join(DEST, dst_rel)
    os.makedirs(dst_dir, exist_ok=True)
    for stem in stems:
        s = os.path.join(src_dir, stem + ".png")
        if not os.path.exists(s):
            print(f"!! MISSING plain {s}")
            continue
        shutil.copy2(s, os.path.join(dst_dir, stem + ".png"))
        plain_sprites.append({"dest": dst_rel + "/" + stem + ".png", "source": src_rel + "/" + stem + ".png"})
print("copied", len(plain_sprites), "plain tiles")

manifest = {"createdBy": "SalvageShuttle recreation (kanban t_282d2507)",
            "sourceRevision": "fc74013522ef2ee1c5049c986748c9f0866fb4c3",
            "settings": {"pixelsPerUnit": 32, "filter": "Point", "compression": "Uncompressed",
                         "mipmaps": False, "slice": "32x32 grid, left-to-right top-to-bottom",
                         "note": "cellIndex == RsiDirection for 4-direction states"},
            "sprites": [{"dest": d + "/" + s + ".png", "source": sr + "/" + s + ".png"}
                        for sr, d, stems in COPY for s in stems] + plain_sprites}
json.dump(manifest, open(os.path.join(DEST, "import-manifest.json"), "w"), indent=1)
print("copied", len(manifest["sprites"]), "pngs")

TILE_SPRITE = {
    "Plating": (f"{SHIPKIT}/Floors/plating.png", "plating", 1),
    "Lattice": (f"{NEW}/Tiles/lattice.png", "lattice", 1),
    "FloorAsteroidTile": (f"{NEW}/Tiles/asteroid_tile.png", "asteroid_tile", 1),
    "FloorGrayConcrete": (f"{NEW}/Tiles/Concrete/grayconcrete.png", "grayconcrete", 4),
    "FloorConcrete": (f"{NEW}/Tiles/Concrete/concrete.png", "concrete", 4),
    "FloorOldConcrete": (f"{NEW}/Tiles/Concrete/oldconcrete.png", "oldconcrete", 4),
}
FLOOR_ORDER = {"Lattice": -11, "Plating": -10}

R = "_NF/Structures/Walls/solid.rsi"
P = {
    "WallReinforced": dict(kind="corners", sprite=f"{NEW}/Walls/Reinforced", rsi=R, base="reinf_over",
                           state="full", key="walls", mode="Corners", order=0, png=R),
    "WallReinforcedDiagonal": dict(kind="diagNF", sprite=f"{NEW}/Walls/ReinforcedDiagonal",
                                   rsi="_NF/Structures/Walls/reinforced_diagonal.rsi", base="state",
                                   state="state0", key="walls", mode="DiagonalNF", order=0),
    "ReinforcedWindow": dict(kind="corners", sprite=f"{NEW}/Windows/Reinforced",
                             rsi="_NF/Structures/Windows/reinforced_window.rsi", base="rwindow",
                             state="full", key="walls", mode="Corners", order=5),
    "ReinforcedWindowDiagonal": dict(kind="diag", sprite=f"{NEW}/Windows/ReinforcedDiagonal",
                                     rsi="_NF/Structures/Windows/reinforced_window_diagonal.rsi",
                                     base="state", state="state0", key="walls", mode="Diagonal", order=5),
    "Catwalk": dict(kind="corners", sprite=f"{NEW}/Catwalk", rsi="Structures/catwalk.rsi",
                    base="catwalk_", state="catwalk_0", key="catwalk", mode="Corners", order=-8),
    "Grille": dict(kind="sprite", sprite=f"{NEW}/Grilles", rsi="Structures/Walls/grille.rsi",
                   state="grille", order=0),
    "GrilleDiagonal": dict(kind="sprite", sprite=f"{NEW}/Grilles", rsi="Structures/Walls/grille.rsi",
                           state="grille_diagonal", order=0),
    "AirlockExternalGlassShuttleArrivals": dict(kind="sprite", sprite=f"{NEW}/Doors/GlassShuttle",
        rsi="Structures/Doors/Airlocks/Glass/shuttle.rsi", state="closed", key="walls", mode="NoSprite", order=10),
    "AirlockEngineering": dict(kind="sprite", sprite=f"{NEW}/Doors/Engineering",
        rsi="Structures/Doors/Airlocks/Standard/engineering.rsi", state="closed", key="walls", mode="NoSprite", order=10),
    "AirlockSalvage": dict(kind="sprite", sprite=f"{NEW}/Doors/Salvage",
        rsi="Structures/Doors/Airlocks/Standard/salvage.rsi", state="closed", key="walls", mode="NoSprite", order=10),
    "SolidReinforcedSecretDoor": dict(kind="sprite", sprite=f"{NEW}/Doors/SecretDoorReinforced",
        rsi="_NF/Structures/Doors/secret_door_reinforced.rsi", state="closed", key="walls", mode="NoSprite", order=0),
    "Thruster": dict(kind="sprite", sprite=f"{NEW}/Shuttles/Thruster", rsi="_NF/Structures/Shuttles/thruster.rsi",
                     state="base", order=11),
    "DebugThruster": dict(kind="sprite", sprite=f"{NEW}/Shuttles/Thruster", rsi="_NF/Structures/Shuttles/thruster.rsi",
                          state="base", order=11),
    "Gyroscope": dict(kind="sprite", sprite=f"{NEW}/Shuttles/Gyroscope", rsi="_NF/Structures/Shuttles/gyroscope.rsi",
                      state="base", order=11),
    "Chair": dict(kind="sprite", sprite=f"{SHIPKIT}/Chairs", rsi="Structures/Furniture/chairs.rsi",
                  state="chair", noRot=True, order=11),
    "ChairPilotSeat": dict(kind="sprite", sprite=f"{SHIPKIT}/Chairs", rsi="Structures/Furniture/chairs.rsi",
                           state="shuttle", noRot=True, order=11),
    "ComputerShuttle": dict(kind="multi", sprite=f"{SHIPKIT}/Consoles", rsi="Structures/Machines/computers.rsi",
                            states=["computer", "generic_keyboard", "shuttle", "generic_keys"], order=11),
    "LockerSalvageSpecialist": dict(kind="multi", sprite=f"{NEW}/Storage/Closet", rsi="Structures/Storage/closet.rsi",
                                    states=["generic", "generic_door"], noRot=True, order=11),
    "NitrogenCanister": dict(kind="sprite", sprite=f"{NEW}/Storage/Canister", rsi="_NF/Structures/Storage/canister.rsi",
                             state="nitrogen", noRot=True, order=11),
    "OxygenCanister": dict(kind="sprite", sprite=f"{NEW}/Storage/Canister", rsi="_NF/Structures/Storage/canister.rsi",
                           state="oxygen", noRot=True, order=11),
    "PortableGeneratorSuperPacman": dict(kind="sprite", sprite=f"{NEW}/Power/PortableGenerator",
        rsi="Structures/Power/Generation/portable_generator.rsi", state="portgen1", noRot=True, order=11),
    "SMESBasic": dict(kind="multi", sprite=f"{NEW}/Power/SMES", rsi="Structures/Power/smes.rsi",
                      states=["smes", "smes-oc0", "smes-op1"], noRot=True, order=11),
    "SubstationBasicEmpty": dict(kind="sprite", sprite=f"{NEW}/Power/Substation", rsi="Structures/Power/substation.rsi",
                                 state="substation", noRot=True, order=11),
}

# ------------------------------------------------------------------ read decoded map
tiles = []
with open(os.path.join(SCRATCH, "out/tiles.csv"), newline="") as f:
    for row in csv.DictReader(f):
        tiles.append((int(row["x"]), int(row["y"]), row["tile"], int(row["variant"])))
ents = []
with open(os.path.join(SCRATCH, "out/entities.csv"), newline="") as f:
    for row in csv.DictReader(f):
        rot = float(row["rot"].split()[0]) if row["rot"] else 0.0
        ents.append({"uid": row["uid"], "proto": row["proto"], "x": float(row["x"]),
                     "y": float(row["y"]), "rot": rot})
print("decoded:", len(tiles), "tiles,", len(ents), "entities")

CELL_OF = {0: "south", 90: "east", 180: "north", 270: "west"}
# angle -> RsiDirection cell index (South 0, North 1, East 2, West 3)
DIR_CELL = {0: 0, 90: 2, 180: 1, 270: 3}


def rot_deg(rad):
    return round(rad * 180.0 / math.pi)


def norm_angle(a):
    return int(round(a)) % 360


def rot_vec(v, ang_deg):
    a = math.radians(ang_deg)
    return (round(v[0] * math.cos(a) - v[1] * math.sin(a)),
            round(v[0] * math.sin(a) + v[1] * math.cos(a)))


def tile_of(x, y):
    return (int(math.floor(x)), int(math.floor(y)))


for e in ents:
    e["tile"] = tile_of(e["x"], e["y"])
by_tile = {}
for e in ents:
    by_tile.setdefault(e["tile"], []).append(e)

DIRV = {"n": (0, 1), "ne": (1, 1), "e": (1, 0), "se": (1, -1),
        "s": (0, -1), "sw": (-1, -1), "w": (-1, 0), "nw": (-1, 1)}


def matching(self_p, cand, off):
    cp = P.get(cand["proto"], {})
    if cp.get("key") is None or cp["key"] != self_p["key"]:
        return False
    if cp["mode"] in ("Diagonal", "DiagonalNF"):
        # EntityIsSmoothOnEdge(target=cand, sourceDir=off from self to cand)
        ux, uy = -off[0], -off[1]
        a = -cand["rot"]
        rx = ux * math.cos(a) - uy * math.sin(a)
        ry = ux * math.sin(a) + uy * math.cos(a)
        ang = math.atan2(ry, rx) + math.pi / 2
        ang = (ang + 4 * math.pi) % (2 * math.pi)
        idx = (int(math.floor((ang + math.pi / 4) / (math.pi / 2)) * 2) % 8)
        return idx in (0, 2)      # South or East (GetCardinalDir only yields the 4 cardinals)
    return True


def hit(self_p, mine, off):
    t = (mine["tile"][0] + off[0], mine["tile"][1] + off[1])
    return any(matching(self_p, c, off) for c in by_tile.get(t, []))


def layer_cell_rot(p, state, offset_deg, rdeg, noRot):
    """RsiDirection cell index + Unity rotation for one layer (SpriteComponent.EffectiveDirection)."""
    d = DIRS.get((p["rsi"], state))
    if d is None:
        raise SystemExit(f"unknown state {state!r} in {p['rsi']}")
    if d == 1:
        return 0, (0 if noRot else rdeg)
    # FIX (t_1c29e9bf): Dir4 cell art is already positioned in its own corner
    # quadrant (cell 0=SE/BR, 2=NE/TR, 1=NW/TL, 3=SW/BL), so the cell index alone
    # encodes orientation -> NO extra Unity rotation. The old -offset_deg rotated
    # the pre-positioned pieces out of their quadrants (3/4 of each overlay tile
    # left uncovered, exposing the plain base wall).
    return DIR_CELL[norm_angle(rdeg + offset_deg)], (0 if noRot else 0)


out_lines = []
counts = {}
warn = []

for (x, y, tname, variant) in tiles:
    if tname == "Space":
        continue
    if tname not in TILE_SPRITE:
        warn.append("unknown tile " + tname)
        continue
    path, stem, nvar = TILE_SPRITE[tname]
    out_lines.append([f"floor#{x}_{y}", path, f"{stem}_{variant if nvar > 1 else 0}",
                      x + 0.5, y + 0.5, 0, FLOOR_ORDER.get(tname, -9)])
    counts[tname] = counts.get(tname, 0) + 1

for e in ents:
    p = P.get(e["proto"])
    if p is None:
        warn.append("unknown proto " + e["proto"])
        continue
    rdeg = norm_angle(rot_deg(e["rot"]))
    label = f"{e['proto']}#{e['uid']}"
    layers = []
    if p["kind"] == "corners":
        h = {k: hit(p, e, DIRV[k]) for k in DIRV}
        cne = (1 if h["n"] else 0) + (2 if h["ne"] else 0) + (4 if h["e"] else 0)
        cse = (1 if h["e"] else 0) + (2 if h["se"] else 0) + (4 if h["s"] else 0)
        csw = (1 if h["s"] else 0) + (2 if h["sw"] else 0) + (4 if h["w"] else 0)
        cnw = (1 if h["w"] else 0) + (2 if h["nw"] else 0) + (4 if h["n"] else 0)
        card = CELL_OF[rdeg]
        if card == "north":
            cne, cnw, csw, cse = csw, cse, cne, cnw
        elif card == "west":
            cne, cnw, csw, cse = cse, cne, cnw, csw
        elif card == "east":
            cne, cnw, csw, cse = cnw, csw, cse, cne
        layers.append((p["state"], 0, e, p["order"]))
        for fill, off, oo in ((cse, 0, 1), (cne, 90, 2), (cnw, 180, 3), (csw, 270, 4)):
            layers.append((f"{p['base']}{fill}", off, e, p["order"] + oo))
    elif p["kind"] in ("diagNF", "diag"):
        def rot_hit(v):
            return hit(p, e, rot_vec(v, rdeg))
        a0 = rot_hit((1, 0))
        a1 = rot_hit((0, -1))
        if p["kind"] == "diagNF":
            value = (1 if a0 else 0) | (2 if a1 else 0)
            if value == 3 and rot_hit((1, -1)):
                value = 4
        else:
            value = 1 if (a0 and a1 and rot_hit((1, -1))) else 0
        layers.append((f"{p['base']}{value}", 0, e, p["order"]))
    elif p["kind"] == "multi":
        for i, st in enumerate(p["states"]):
            layers.append((st, 0, e, p["order"] + i))
    else:
        layers.append((p["state"], 0, e, p["order"]))

    for state, off, ent, order in layers:
        cell, rot = layer_cell_rot(p, state, off, rdeg, p.get("noRot", False))
        out_lines.append([label, p["sprite"], f"{state}_{cell}", ent["x"], ent["y"], rot, order])
    counts[e["proto"]] = counts.get(e["proto"], 0) + 1

# sanity: every cell name must exist as a sliced sprite (PNG present, index < 32x32 cell count)
import struct


def png_dims(path):
    with open(path, "rb") as fh:
        d = fh.read(33)
    w, h = struct.unpack(">II", d[16:24])
    return w, h


errors = []
for line in out_lines:
    path, cellname = line[1], line[2]
    stem, idx = cellname.rsplit("_", 1)
    if path.endswith(".png"):
        png = os.path.join(PROJ, path)          # plain tile texture (no RSI dir)
    else:
        png = os.path.join(PROJ, path + "/" + stem + ".png")
    if not os.path.exists(png):
        errors.append("missing png " + png)
        continue
    w, h = png_dims(png)
    n = (w // 32) * (h // 32)
    if int(idx) >= n:
        errors.append(f"cell {cellname} out of range ({n} cells) for {png}")

os.makedirs(os.path.join(PROJ, "Assets/ShipData"), exist_ok=True)
with open(os.path.join(PROJ, "Assets/ShipData/salvageshuttle.layout.tsv"), "w") as f:
    f.write("# label\tspriteAssetPath\tcellName\tposX\tposY\trotDeg\tsortingOrder\n")
    for r in out_lines:
        f.write("\t".join(str(v) for v in r) + "\n")

print("\n== counts ==")
for k in sorted(counts):
    print(f"  {k:<34} {counts[k]}")
print("\nwarnings:", warn if warn else "none")
print("sprite errors:", errors if errors else "none")
print("placements:", len(out_lines))
print("wrote Assets/ShipData/salvageshuttle.layout.tsv")
