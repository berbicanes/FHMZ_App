#!/usr/bin/env python3
from __future__ import annotations

"""
Iscrpan obilazak ArcGIS REST kataloga AVP Save.

Prolazi svaki folder, svaki servis, svaki sloj i svaku tabelu, i ispisuje sva polja.
Postoji zato što je pretraga po jednom sloju dala pogrešan zaključak: sloj realnog
vremena nema temperaturu vode, ali to ne znači da je nema nigdje u katalogu.

Rezultat ide u JSON da bi se mogao čitati bez ponovnog gađanja njihovog servera.
Pauza između poziva je namjerna — njihova infrastruktura je javna imovina
(CLAUDE.md, zlatno pravilo 6).
"""

import json
import sys
import time
import urllib.parse
import urllib.request

ROOT = "https://isvportal.voda.ba/server/rest/services"
UA = "VodostajiBiH-probe/0.1 (+https://github.com/berbicanes/FHMZ_App)"
DELAY = 0.35

# Riječi koje odaju mjerenje koje nas zanima. Namjerno široko — bolje lažno pozitivno
# koje se odbaci pogledom nego propušteno polje.
INTEREST = [
    "temp", "_t_", "tw", "voda", "water", "celsius", "°c",
    "protok", "flow", "q_", "discharge", "protok",
    "padavin", "precip", "kisa", "rain",
]


def get(url: str) -> dict | None:
    joiner = "&" if "?" in url else "?"
    try:
        request = urllib.request.Request(f"{url}{joiner}f=json", headers={"User-Agent": UA})
        with urllib.request.urlopen(request, timeout=40) as response:
            return json.loads(response.read().decode("utf-8", "replace"))
    except Exception as error:                       # noqa: BLE001 — probe, ne produkcija
        print(f"    ! {url} → {error}", file=sys.stderr)
        return None
    finally:
        time.sleep(DELAY)


def interesting(field: dict) -> bool:
    blob = f"{field.get('name', '')} {field.get('alias', '')}".lower()
    return any(word in blob for word in INTEREST)


def walk_service(base: str, name: str, kind: str, out: list) -> None:
    url = f"{base}/{urllib.parse.quote(name)}/{kind}"
    meta = get(url)
    if not meta:
        return

    entries = [(layer, "layer") for layer in meta.get("layers") or []]
    entries += [(table, "table") for table in meta.get("tables") or []]

    print(f"\n  ▸ {name} ({kind}) — {len(entries)} slojeva/tabela")

    for entry, entry_kind in entries:
        detail = get(f"{url}/{entry['id']}")
        if not detail:
            continue

        fields = detail.get("fields") or []
        hits = [f for f in fields if interesting(f)]

        record = {
            "service": name,
            "kind": kind,
            "entryKind": entry_kind,
            "id": entry["id"],
            "name": detail.get("name"),
            "url": f"{url}/{entry['id']}",
            "fields": [
                {"name": f.get("name"), "type": f.get("type"), "alias": f.get("alias")}
                for f in fields
            ],
            "interesting": [f.get("name") for f in hits],
        }
        out.append(record)

        mark = "  ★" if hits else "   "
        print(f"   {mark} [{entry['id']:>3}] {detail.get('name')}  ({len(fields)} polja)")
        for f in hits:
            print(f"          → {f.get('name'):<22}{f.get('type','')[13:]:<10}{f.get('alias')}")


def walk_folder(folder: str | None, out: list) -> None:
    base = ROOT if folder is None else f"{ROOT}/{urllib.parse.quote(folder)}"
    listing = get(base)
    if not listing:
        return

    print(f"\n══════ FOLDER: {folder or '(korijen)'} ══════")

    for service in listing.get("services") or []:
        # Ime servisa u podfolderu dolazi kao `folder/ime`; putanja se gradi od korijena.
        walk_service(ROOT, service["name"], service["type"], out)


def main() -> None:
    root = get(ROOT)
    if not root:
        sys.exit("Katalog nije dostupan.")

    collected: list = []

    walk_folder(None, collected)
    for folder in root.get("folders") or []:
        walk_folder(folder, collected)

    with open(sys.argv[1], "w", encoding="utf-8") as handle:
        json.dump(collected, handle, ensure_ascii=False, indent=2)

    hits = [r for r in collected if r["interesting"]]
    print(f"\n═══ UKUPNO: {len(collected)} slojeva/tabela, {len(hits)} sa zanimljivim poljima ═══")
    for record in hits:
        print(f"  {record['name']}  ({record['url']})")
        print(f"      {record['interesting']}")


if __name__ == "__main__":
    main()
