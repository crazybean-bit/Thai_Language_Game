"""Generates Thai voice clips for the game with text-to-speech.

Creates WAV files in Assets/Resources/Audio:
  Lines/<dialogueId>_<lineIndex>.wav   for every Thai line a character says (lines he can't
                                       make out play muffled)
  Words/w_<hex codepoints>.wav         every word in words.json, in the voice that teaches it
  Words/Own/w_<hex codepoints>.wav     every word in the protagonist's own voice
  Words/Hesitant/w_<hex codepoints>.wav  every word in his voice, stumbling over it (Azure only:
                                       the pause needs SSML)

The game plays these automatically when they exist and falls back to voice blips
when they don't, so you can generate them any time.

Usage (no extra Python packages needed):

  Azure (free tier: 0.5M characters/month):
    export AZURE_TTS_KEY=...  AZURE_TTS_REGION=southeastasia
    python3 Tools/generate_voices.py --provider azure

  Google Cloud (free tier: 1M characters/month for standard voices):
    export GOOGLE_TTS_KEY=...
    python3 Tools/generate_voices.py --provider google

Options:
  --dry-run   list what would be generated without calling any service
  --force     regenerate clips that already exist (e.g. after editing a line)

Voices per speaker are set in Tools/voices.json.
"""
import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request
from xml.sax.saxutils import escape

# What he says before a word he's unsure of, unless the word has its own "stutter".
FILLER = "เอ่อ"
PAUSE = '<break time="300ms"/>'

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
DATA = os.path.join(ROOT, "Assets", "Resources", "Data")
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")


def is_thai(text):
    return any("฀" <= ch <= "๿" for ch in text)


def word_key(thai):
    """Must match VoicePlayer.WordKey in C#."""
    return "w" + "".join("_%04x" % ord(ch) for ch in thai)


def spoken(text):
    # The game shows spaces between words for learners; real Thai has none, so
    # remove them so the voice reads the line as one natural phrase.
    return "".join(text.split())


def stutter_ssml(w):
    """"ขอ... ขอบคุณ" in words.json becomes a pause where the dots are. Must match
    VoiceGeneratorWindow.Stutter in C#."""
    if not (w.get("stutter") or "").strip():
        return escape(FILLER) + PAUSE + escape(w["thai"].strip())
    return PAUSE.join(escape(spoken(p)) for p in w["stutter"].split("..."))


def collect():
    with open(os.path.join(DATA, "dialogue.json"), encoding="utf-8") as f:
        dialogues = json.load(f)["dialogues"]
    with open(os.path.join(DATA, "words.json"), encoding="utf-8") as f:
        words = json.load(f)["words"]

    jobs = []
    for d in dialogues:
        for i, line in enumerate(d["lines"]):
            text = line.get("text", "")
            # Narration is played back word by word from the word clips.
            if is_thai(text) and line.get("speaker"):
                jobs.append({
                    "path": os.path.join(AUDIO, "Lines", f"{d['id']}_{i}.wav"),
                    "text": spoken(text),
                    "speaker": line.get("speaker", ""),
                    "word": False,
                })
    for w in words:
        jobs.append({
            "path": os.path.join(AUDIO, "Words", word_key(w["thai"]) + ".wav"),
            "text": w["thai"],
            "speaker": "words",
            "word": True,
        })
        jobs.append({
            "path": os.path.join(AUDIO, "Words", "Own", word_key(w["thai"]) + ".wav"),
            "text": w["thai"],
            "speaker": "own",
            "word": True,
        })
        jobs.append({
            "path": os.path.join(AUDIO, "Words", "Hesitant", word_key(w["thai"]) + ".wav"),
            "text": stutter_ssml(w),
            "speaker": "own",
            "word": True,
            "ssml": True,
        })
    return jobs


def azure_tts(text, voice, rate, ssml=False):
    key = os.environ.get("AZURE_TTS_KEY")
    region = os.environ.get("AZURE_TTS_REGION", "southeastasia")
    if not key:
        sys.exit("Set AZURE_TTS_KEY (and AZURE_TTS_REGION) first.")
    body = (
        '<speak version="1.0" xml:lang="th-TH" xmlns="http://www.w3.org/2001/10/synthesis">'
        f'<voice name="{voice}"><prosody rate="{rate}">{text if ssml else escape(text)}</prosody></voice></speak>'
    ).encode("utf-8")
    req = urllib.request.Request(
        f"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1",
        data=body,
        headers={
            "Ocp-Apim-Subscription-Key": key,
            "Content-Type": "application/ssml+xml",
            "X-Microsoft-OutputFormat": "riff-24khz-16bit-mono-pcm",
            "User-Agent": "thai-language-game",
        },
    )
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read()


def google_tts(text, voice, rate, ssml=False):
    key = os.environ.get("GOOGLE_TTS_KEY")
    if not key:
        sys.exit("Set GOOGLE_TTS_KEY first.")
    speaking_rate = 1.0 + float(rate.rstrip("%")) / 100 if rate.endswith("%") else 1.0
    body = json.dumps({
        "input": {"text": text},
        "voice": {"languageCode": "th-TH", "name": voice},
        "audioConfig": {"audioEncoding": "LINEAR16", "sampleRateHertz": 24000, "speakingRate": speaking_rate},
    }).encode("utf-8")
    req = urllib.request.Request(
        f"https://texttospeech.googleapis.com/v1/text:synthesize?key={key}",
        data=body,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=60) as r:
        return base64.b64decode(json.load(r)["audioContent"])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--provider", choices=["azure", "google"], default="azure")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "voices.json"), encoding="utf-8") as f:
        config = json.load(f)
    voices = config[args.provider]
    word_rate = config.get("wordRate", "-20%")
    synth = azure_tts if args.provider == "azure" else google_tts

    jobs = collect()
    if args.provider != "azure":
        jobs = [j for j in jobs if not j.get("ssml")]
    todo = [j for j in jobs if args.force or not os.path.exists(j["path"])]
    chars = sum(len(j["text"]) for j in todo)
    print(f"{len(jobs)} clips in total, {len(todo)} to generate ({chars} characters).")

    for j in todo:
        voice = voices.get(j["speaker"], voices.get("", ""))
        rel = os.path.relpath(j["path"], ROOT)
        if args.dry_run:
            print(f"  {rel}  [{voice}]  {j['text']}")
            continue
        try:
            rate = config.get("hesitantRate", "-25%") if j.get("ssml") else word_rate if j["word"] else "0%"
            audio = synth(j["text"], voice, rate, j.get("ssml", False))
        except urllib.error.HTTPError as e:
            sys.exit(f"{args.provider} returned {e.code} for {rel}: {e.read()[:300]!r}")
        os.makedirs(os.path.dirname(j["path"]), exist_ok=True)
        with open(j["path"], "wb") as f:
            f.write(audio)
        print(f"  wrote {rel}")


if __name__ == "__main__":
    main()
