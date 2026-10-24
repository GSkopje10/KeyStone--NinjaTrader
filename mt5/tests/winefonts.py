import sys
from fontTools.ttLib import TTFont
src, dst, fam, sub = sys.argv[1:5]; fam = fam.replace("_", " ")
f = TTFont(src)
for r in f['name'].names:
    if r.nameID in (1, 16): r.string = fam
    elif r.nameID in (2, 17): r.string = sub
    elif r.nameID == 4: r.string = fam + (" " + sub if sub != "Regular" else "")
    elif r.nameID == 6: r.string = (fam + "-" + sub).replace(" ", "")
f.save(dst)
