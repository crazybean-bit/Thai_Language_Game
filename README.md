# Thai_Language_Game

A 2D mystery RPG prototype in the spirit of *7 Days to End with You*, except the "unknown language" is real Thai. You wash up on Ko Saeng, a Thai island, the morning after a tsunami, with no memory. A young woman, Mali, takes you in. Everyone speaks Thai. You work out what words mean from context and write your guesses in a journal, and as you get groups of words right they lock in and show how they're pronounced. The story and the plan for the full game are in the design doc.

## Opening the project

1. Install **Unity 6.3** (6000.3.27f1, the version the project is set to; any later 6000.x works too) through Unity Hub. Older versions like 2020 or 2022 won't work because the scripts use the TextMeshPro that ships inside `com.unity.ugui` 2.0. If Hub asks for a missing editor version, just pick the latest Unity 6 LTS.
2. In Unity Hub choose **Add > Add project from disk** and pick this folder. Unity fills in the rest of `ProjectSettings` on first open.
3. If Unity asks to import **TMP Essentials**, click *Import TMP Essentials* (or use *Window > TextMeshPro > Import TMP Essential Resources*).
4. Use the menu **Thai Mystery > Open Prototype Scene**. The first time, this creates `Assets/Scenes/Prototype.unity` (choose *Don't Save* if asked about the Untitled scene). Then press **Play**.

If the scene ever breaks, **Thai Mystery > Rebuild Prototype Scene** recreates it. The scene only holds a camera and one `GameBootstrap` object; everything else (map, characters, camera, UI) is built in code from the JSON files.

The yellow "Input Manager is marked for deprecation" warning is harmless: `GameInput.cs` works with either the old Input Manager or the new Input System.

## Controls

| Action | Keys |
| --- | --- |
| Move | WASD / Arrow keys |
| Inspect / talk / next line | Space, Z, Enter, or click the message box |
| Pick a dialogue choice | Up/Down then Space, or click |
| Journal | J or Tab (Esc closes) |
| Replay the line's voice | R |
| Guess a word | Click any Thai word in a message or a journal card, type, press Enter |
| Build a sentence (tiles) | Click tiles or press their number, Backspace removes the last, Enter says it. The speaker on a tile plays the word; R replays the last line you heard |
| Show the story values (debug) | F1 |

## How the language mechanic works

- **Words are learned by ear.** At first the protagonist can't make out anything: Thai speech and labels show as a blurred, shifting jumble of letters he can't focus on, with no voice. Once Mali starts teaching him, every word she pronounces clearly unlocks: it appears properly, goes into the journal, and your guess appears under it in every message from then on.
- **Each word moves through five stages**, and each step needs a different kind of evidence, so replaying a word never carries it all the way (`WordBook.cs`):
  - *Noise*: nobody has said it clearly to him. Blurred, shifting letters, no voice.
  - *Heard*: someone said it clearly. It shows, and replays in their voice, muffled.
  - *Understood*: its group is confirmed in the journal. The voice is less foggy.
  - *Familiar*: he caught it in fast speech, in a **listening moment** ("What did I catch?"). His voice starts to blend in.
  - *Owned*: he said it himself, in a choice or with sentence tiles. His own voice, solid ink.
  The journal shows a word's stage as four pips. Remembering a word (inspecting a labelled thing, clicking a word) plays it the way he knows it now.
- **Sentence tiles**: sometimes he has to answer by putting words he has heard in order. It's meant to be done by ear: each tile plays its word as it's added (or from its speaker button without adding it), and R replays what was just said to him. Getting it right raises his confidence and can warm Mali up; some wrong answers get their own reaction (try thanking her with ค่ะ).
- **Mali's warmth and recognition** are hidden values. Warmth goes up and down with how he treats her and how well he talks, and changes how she answers (she drops the polite ค่ะ when she's cold). Recognition rises when something from their past surfaces; when it crosses a threshold she **shuts off**, goes cold, and words tied to their past slip back out of reach.
- **Confidence**: when he's nervous he stumbles over words (hesitant voice clips) and a couple of tiles fog over; when he's confident he talks faster and extra tiles can appear. After three failures in a row, Mali does something small and kind.
- **Money**: odd jobs (helping Lung Daeng refill the generator) pay baht, shown at the top right.
- Words belong to **groups** (e.g. *bed, window, mirror, door*). A group confirms only when every word in it has been heard and guessed right, so you can't brute-force one word at a time. Confirmed words turn green and reveal a romanization and a short learning note.
- Guesses are matched loosely: case, punctuation and leading "to/a/the" are ignored, and each word lists several accepted answers.
- Thai is normally written without spaces between words. The game's script puts spaces between words so each one can be clicked and studied, the same way the original game separates its glyph-words.

## Changing the content (no code needed)

All content lives in `Assets/Resources/Data`:

- `words.json`: every Thai word with accepted answers, romanization, group and note.
- `dialogue.json`: dialogue lines. Thai text is space separated; lines with no Thai are the protagonist's thoughts and show in italics. Lines can set flags and offer choices. `"heard"` on a dialogue (or a line) says whether the protagonist can make out what a character says: `"always"`, a flag name, or left out for noise. Clear lines play their voice clip and teach every word in them; noise shows only the words he already knows, and plays the voice muffled and a little too fast to follow. Lines with no speaker are what he reads or remembers.
  - **Conditions** (`"when"` on a line, choice or map rule, `"showWhen"` on an object) look like `"met_mali"`, `"!asked_photo"`, `"warmth>=housemate"` or `"job_done & !money_talk"`. Values: `warmth`, `recognition`, `confidence`, `money`, `shutoffs`; named numbers: `housemate` (25), `close` (60), `low` (35), `high` (70), `shutoff` (30).
  - **Effects** (`"warmth"`, `"recognition"`, `"confidence"`, `"money"` on a line or choice) change the values when the line shows or the choice is picked. `"fogPast": true` makes past-tied words (`"past": true` in words.json) slip back.
  - **Listening moments**: a noise line with `"catchWord"` and `"catchOptions"`.
  - **Sentence tiles**: `"tiles"` on a line, with `answer`, optional `accept`, extra `words`, `bonus` words for when he's confident, `reactions` to specific wrong sentences, and `correct`/`wrong` dialogues.
  - `"mali_shutoff"` and `"kind_moment"` are special: the game cuts to them by itself.
- `Maps/<name>.json`: one file per map (`house`, `yard`). The layout is text rows: indoors `#` outer wall, `-` upper wall, `=` lower wall, `.` floor, `,` rug; outdoors `:` sand, `;` grass, `~` sea, `%` hedge, `R` roof, `H` house wall. Each map has a player start, objects (`height` and `width` make big things like the bed or the boat block more than one tile) and lighting. Each object has rules that pick a dialogue based on flags and conditions, so Mali says different things after you try the door or find the photo. A rule with `"goTo": "yard", "goX": 10, "goY": 3, "goFacing": "down"` moves the player to another map after its dialogue (if any), which is how doors work. The game starts on `house` (set on `GameBootstrap`).

After editing, run `python3 Tools/check_data.py`: it catches missing dialogues, Thai words that aren't in words.json, and words that are never said clearly (so their group could never be confirmed).

## Art

Placeholder pixel art is generated by `Tools/generate_sprites.py` (Python with Pillow and numpy) into `Assets/Resources/Sprites`. Drop in real art with the same file names to replace any of it; no code changes needed.

- **Style**: dark, muted colours with outlines tinted by the colour they border, tall two-tile walls and cluttered rooms, aiming at the mood of *7 Days to End with You*. It's still generated programmer art; a commissioned artist or a pixel art pack is the real upgrade.
- **Tiles** are 32×32. Floors (`floor_0..3`) and walls (`wall_upper_0..1`, `wall_lower_0..1`) have variants that the map mixes so the pattern doesn't repeat. Rugs (`rug_0..15`) are picked automatically to draw borders on the open edges. Floors next to walls get soft shadows (`ao_top/left/right`).
- **Objects** are 32 px wide and up to 64 px tall; their bottom edge sits on their tile, so wall decor placed on the lower wall reaches up the wall.
- **Character sheets** use the RPG Maker layout: 3 columns (step, idle, step) × 4 rows (down, left, right, up). Frames can be any size and are drawn at 32 px per tile, so a 48×48 frame stands about one and a half tiles tall.
- **Lung Daeng** comes from PixelLab too (`Tools/art/pixellab/lung_daeng`). The yard's tiles and objects are still generated placeholders the yard's tiles and objects (`sand_*`, `grass_*`, `sea_*`, `generator`, `boat`, ...).
- **The player and Mali** come from [PixelLab](https://www.pixellab.ai)'s Character Creator (48 px, low top-down). Their source frames live in `Tools/art/pixellab/<name>/`. To swap one, export from PixelLab and run `python Tools/import_pixellab.py player path/to/export.zip` (the zip or the 8-direction GIF both work). Loose downloads work too: put the rotation PNGs (`south.png`, `west.png`, ...) and the `*_walking_<direction>.gif` files in one folder and pass the folder. With a walk animation, the whole cycle goes into `<name>_walk.png` and plays while walking; without one, the standing pose bobs a pixel. `generate_sprites.py` keeps these instead of drawing its own.
- **Portraits** (e.g. `portrait_mali`) show beside the dialogue box. Set `"portrait"` on a dialogue in `dialogue.json` (or on a single line; `"none"` hides it).

## Lighting

Each map has a `lighting` section: overall `darkness`, a `darkColor`, and a list of lights with a position, radius, intensity, colour and optional `flicker`. The room is drawn dark with soft pools of light around them (`LightingOverlay.cs`). Set `darkness` to 0 to turn it off.

## Voices and sound

While a line appears word by word, each speaker makes little voice blips at their own pitch. If a recorded clip exists for a line he can make out, it plays instead. Words he remembers play from their word clips: `Audio/Words` in the voice that taught them (through a muffled "memory" filter while they're new), and `Audio/Words/Own` in his own voice once learned. `Audio/Words/Hesitant` is his voice stumbling over a word ("เอ่อ..." first, or the word's own `"stutter"` in words.json), for when he's nervous.

Clips live in `Assets/Resources/Audio` and are generated with Azure text-to-speech, which uses natural-sounding Thai neural voices.

**Easiest way, inside Unity:** open **Thai Mystery > Generate Voices...**, paste the key and region from your Azure Speech resource (*Keys and Endpoint* page), and click **Generate missing clips**. It reads `dialogue.json` and `words.json`, makes a clip for every line a character says and every word (in Mali's voice, in the protagonist's, and his hesitant version), and saves your key only on your computer. Clips that already exist are kept, so it only costs anything for new lines. Each voice has a pitch and speed (e.g. `+6%`, `-5%`), a **Test** button that speaks the test sentence without saving anything, and a **Redo** button that remakes just that character's clips after you change their voice. The free tier is rate limited, so it pauses now and then; the whole game takes a few minutes. Pick which voice reads which character in the same window.

**Or from the command line** with `Tools/generate_voices.py` (Azure or Google):

```bash
# Azure (free tier is plenty; the whole game is a few thousand characters)
export AZURE_TTS_KEY=...  AZURE_TTS_REGION=southeastasia
python3 Tools/generate_voices.py --provider azure

# or Google Cloud
export GOOGLE_TTS_KEY=...
python3 Tools/generate_voices.py --provider google
```

Use `--dry-run` to see the list of clips first, and `--force` to redo clips after editing a line. Which voice reads which character is set in `Tools/voices.json`. You can also replace any WAV with a real recording of the same name.

## Font

Thai text uses **Sarabun** (SIL Open Font License, see `Assets/Resources/Fonts/Sarabun-OFL.txt`). It covers Thai and Latin, and uses the looped letter style taught in textbooks, which helps learners. A dynamic TextMeshPro font asset is created from the TTF at runtime (`ThaiFonts.cs`), so there's no atlas to bake.

Known limitation: TextMeshPro positions Thai vowel and tone marks with the font's mark tables, which works for normal text, but it doesn't do full Thai shaping. A few rare combinations (for example a lower vowel under ฐ or ญ) may not look perfect.

## Code layout

```
Assets/Scripts
  Core/      GameBootstrap (builds the scene), Game (shared refs), GameState (warmth, recognition,
             confidence, money, conditions), GameInput, ThaiFonts, SpriteLibrary
  Data/      JSON data classes, WordBook (guesses, groups, word stages, saving)
  World/     WorldLoader (builds maps, doors), GridMap, PlayerController, CharacterSprite, Interactable,
             CameraFollow, LightingOverlay
  Dialogue/  DialogueRunner, VoicePlayer
  UI/        GameUI, DialogueUI, TilePanel, JournalUI, GuessPopup, WordRow, UIFactory
Assets/Editor  pixel-art import settings and the Thai Mystery menu
```

`GameBootstrap` has a *Reset Save On Play* toggle (on by default) so each Play starts fresh; turn it off to keep your guesses between runs. **Thai Mystery > Clear Saved Guesses** wipes them manually.
