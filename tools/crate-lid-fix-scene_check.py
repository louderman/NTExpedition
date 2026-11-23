"""Scene integrity check for the StorageCrate lid fix (no Unity needed).

Verifies the hand-edited HomeShip.unity:
  * every document anchor id is unique
  * the new CrateLid GameObject/Transform/SpriteRenderer block is internally consistent
  * the crate transform's child list and the lid's father agree
  * every local {fileID: N} referenced by the crate cluster resolves to an anchor in the scene
  * the sprite references point at the Steel closed/open sprites
Exit code 0 = all checks pass.
"""
import re
import sys
import collections

SCENE = "/home/dan/Documents/gamedesignlab/NTExpedition/Assets/Scenes/HomeShip.unity"
text = open(SCENE, encoding="utf-8").read()

anchors = {}
docs = re.findall(r"^--- !u!(\d+) &(-?\d+)", text, re.M)
for line_no, (cls, fid) in enumerate(docs, 1):
    anchors[fid] = (cls, line_no)

fails = []
ids = [fid for _, fid in docs]

dupes = {i: c for i, c in collections.Counter(ids).items() if c > 1}
print(f"documents: {len(docs)}  unique anchors: {len(set(ids))}")
if dupes:
    fails.append(f"duplicate anchor ids: {dupes}")
else:
    print("PASS unique anchors")

for fid in ("919100001", "919100002", "919100003"):
    if fid not in anchors:
        fails.append(f"missing anchor {fid}")
print("PASS new anchors present" if not fails else "")


def block(start_id):
    """Return the text of the document whose anchor id is start_id."""
    pat = re.compile(r"^--- !u!(\d+) &" + start_id + r"$", re.M)
    m = pat.search(text)
    if not m:
        return None
    nxt = re.search(r"^--- !u!", text[m.end():], re.M)
    return text[m.start():m.end() + (nxt.start() if nxt else len(text))]


def refs(blk, ignore_guid=False):
    out = set()
    for m in re.finditer(r"\{fileID: (-?\d+)(?:, guid: ([0-9a-f]+), type: (\d+))?\}", blk):
        if m.group(2) and ignore_guid:
            continue
        out.add(int(m.group(1)))
    return out


go = block("919100001")
tr = block("919100002")
sr = block("919100003")
crate_go = block("224699926")
crate_tr = block("224699932")
mb = block("224699927")

checks = [
    ("CrateLid GameObject exists", go is not None),
    ("CrateLid transform exists", tr is not None),
    ("CrateLid renderer exists", sr is not None),
    ("GameObject name is CrateLid", "m_Name: CrateLid" in (go or "")),
    ("GameObject lists transform+renderer",
     "- component: {fileID: 919100002}" in (go or "") and "- component: {fileID: 919100003}" in (go or "")),
    ("renderer m_GameObject -> CrateLid", "m_GameObject: {fileID: 919100001}" in (sr or "")),
    ("transform m_GameObject -> CrateLid", "m_GameObject: {fileID: 919100001}" in (tr or "")),
    ("transform m_Father -> StorageCrate transform", "m_Father: {fileID: 224699932}" in (tr or "")),
    ("StorageCrate transform lists lid child", "- {fileID: 919100002}" in (crate_tr or "")),
    ("StorageCrate transform still a root", "m_Father: {fileID: 0}" in (crate_tr or "")),
    ("renderer sorting order 1 (above base)", "m_SortingOrder: 1" in (sr or "")),
    ("renderer sprite = Steel closed.png", "guid: 542ef38a5812995848b8a301b30cb36d" in (sr or "")),
    ("renderer sprite fileID = closed_0", "m_Sprite: {fileID: -3330984756735713963," in (sr or "")),
    ("renderer sprite assigned", "m_WasSpriteAssigned: 1" in (sr or "")),
    ("renderer uses same builtin sprite material",
     "fileID: 2100000, guid: a97c105638bdf8b4a8650670310a4cd3" in (sr or "")),
    ("StorageCrate.lidRenderer -> CrateLid renderer", "lidRenderer: {fileID: 919100003}" in (mb or "")),
    ("StorageCrate.closedLidSprite = Steel closed",
     "closedLidSprite: {fileID: -3330984756735713963, guid: 542ef38a5812995848b8a301b30cb36d" in (mb or "")),
    ("StorageCrate.openLidSprite = Steel open",
     "openLidSprite: {fileID: -5680835434184144748, guid: 87f4c2983814231859a48fb6dd571881" in (mb or "")),
    ("StorageCrate still has its original refs (inventoryUI/player)",
     "inventoryUI: {fileID: 224699928}" in (mb or "") and "player: {fileID: 1718987722}" in (mb or "")),
    ("crate GameObject still has all 6 components",
     len(re.findall(r"- component: \{fileID: \d+\}", crate_go or "")) == 6),
]
for name, ok in checks:
    print(("PASS " if ok else "FAIL ") + name)
    if not ok:
        fails.append(name)

# every local fileID referenced by the three new blocks + the crate cluster must be a scene anchor
ctr = set()
for blk in (go, tr, sr, crate_tr, mb, crate_go):
    for fid in refs(blk or "", ignore_guid=True):
        if fid != 0:
            ctr.add(fid)
unresolved = sorted(f for f in ctr if str(f) not in anchors)
print("crate-cluster local refs:", sorted(ctr))
if unresolved:
    fails.append(f"unresolved local fileID refs: {unresolved}")
    print("FAIL unresolved refs:", unresolved)
else:
    print("PASS all crate-cluster local fileID refs resolve to scene anchors")

print()
if fails:
    print("RESULT: FAIL")
    for f in fails:
        print(" -", f)
    sys.exit(1)
print("RESULT: PASS")
