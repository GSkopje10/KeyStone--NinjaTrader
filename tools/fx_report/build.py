#!/usr/bin/env python3
"""Builds the HFT Flip Lab page: tools/fx_report/template.html + the simulator's fx_hft.json (+ analysis.html, next.html)
→ one self-contained HTML file. Usage: build.py <fx_hft.json> <out.html> [--standalone]
--standalone adds the <!doctype html> / charset / viewport wrapper so the file opens cleanly straight from disk."""
import os, sys
here = os.path.dirname(os.path.abspath(__file__))
data = open(sys.argv[1], encoding="utf-8").read().replace("</", "<\\/")
page = open(os.path.join(here, "template.html"), encoding="utf-8").read()
for tag, name in (("<!--ANALYSIS-->", "analysis.html"), ("<!--NEXT-->", "next.html")):
    p = os.path.join(here, name)
    page = page.replace(tag, open(p, encoding="utf-8").read() if os.path.exists(p) else "")
page = page.replace("/*DATA*/", data, 1)
if "--standalone" in sys.argv:
    page = '<!doctype html>\n<html lang="en">\n<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover"></head>\n<body>\n' + page + "\n</body>\n</html>\n"
open(sys.argv[2], "w", encoding="utf-8").write(page)
print(sys.argv[2], round(len(page) / 1e6, 2), "MB")
