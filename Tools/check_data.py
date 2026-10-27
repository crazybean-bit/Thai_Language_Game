"""Checks the story data in Assets/Resources/Data for mistakes the game can't catch.

Run with: python3 Tools/check_data.py

  - every dialogue a map rule, choice, tile or "next" points to exists
  - every door leads to a map that exists, onto a tile you can stand on
  - every Thai word someone says clearly is in words.json
  - every word in words.json is said clearly somewhere, so every group can be confirmed
  - tile answers, tile words and listening-moment options are real words
"""
import json
import os
import sys

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Data")


def load(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return json.load(f)


def is_thai(token):
    return any("฀" <= c <= "๿" for c in token)


def thai_words(text):
    return [t for t in (text or "").split() if is_thai(t)]


def main():
    words = {w["thai"].strip(): w for w in load("words.json")["words"]}
    dialogues = {d["id"]: d for d in load("dialogue.json")["dialogues"]}
    maps_dir = os.path.join(DATA, "Maps")
    worlds = {f[:-5]: load(os.path.join("Maps", f)) for f in sorted(os.listdir(maps_dir)) if f.endswith(".json")}
    problems = []
    heard = set()

    def ref(target, where):
        if target and target not in dialogues:
            problems.append(f"{where}: no dialogue '{target}'")

    def known(word, where):
        if word not in words:
            problems.append(f"{where}: '{word}' is not in words.json")

    for name, world in worlds.items():
        ref(world.get("introDialogue"), f"Maps/{name} introDialogue")
        rows = world["rows"]
        for o in world["objects"]:
            for r in o.get("rules") or []:
                ref(r.get("dialogue"), f"Maps/{name} {o['id']}")
                if r.get("goTo"):
                    target = worlds.get(r["goTo"])
                    if target is None:
                        problems.append(f"Maps/{name} {o['id']}: no map '{r['goTo']}'")
                    elif not (0 <= r.get("goY", 0) < len(target["rows"]) and
                              target["rows"][r.get("goY", 0)][r.get("goX", 0):r.get("goX", 0) + 1] in (".", ",", ":", ";")):
                        problems.append(f"Maps/{name} {o['id']}: arrives on a tile you can't stand on in '{r['goTo']}'")

    for d in dialogues.values():
        ref(d.get("next"), f"{d['id']} next")
        for i, line in enumerate(d["lines"]):
            where = f"{d['id']}[{i}]"
            text = line.get("text", "")
            clear = line.get("heard") or d.get("heard")
            if line.get("speaker") and clear and is_thai(text):
                for w in thai_words(text):
                    known(w, where)
                    heard.add(w)
            for c in line.get("choices") or []:
                ref(c.get("next"), where)
                for w in thai_words(c.get("text")):
                    known(w, where + " choice")
            t = line.get("tiles")
            if t:
                for target in [t.get("correct"), t.get("wrong")] + [r.get("next") for r in t.get("reactions") or []]:
                    ref(target, where + " tiles")
                for w in thai_words(" ".join([t.get("answer", "")] + (t.get("words") or []) + (t.get("bonus") or [])
                                              + (t.get("accept") or []))):
                    known(w, where + " tiles")
            if line.get("catchWord"):
                for w in [line["catchWord"]] + (line.get("catchOptions") or []):
                    known(w, where + " catch")

    for w, entry in words.items():
        if w not in heard:
            problems.append(f"words.json: '{w}' ({entry.get('group')}) is never said clearly, so its group can't be confirmed")

    for p in problems:
        print(p)
    print(f"{len(dialogues)} dialogues, {len(words)} words, {len(heard & set(words))} heard clearly. "
          f"{len(problems)} problem(s).")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
