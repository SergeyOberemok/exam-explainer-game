# ExamCards

A Unity 6 quiz game for practising certification exam questions. The player sees one question at a time with several answer cards, and exactly one is correct.

- **Reveal** swaps the question for a simplified, plain-words version.
- **Search** (header button or `/`) finds any question by text and plays it right away.
- **50/50** removes two wrong answers. Three lives, and a wrong answer costs one.
- Questions arrive in batches from a backend, so a round keeps going until the lives or the questions run out.

The UI is built with UI Toolkit (UXML/USS) and started from the Figma file "ExamCards", page PickOne.

## Requirements

- Unity **6000.6.3f1** (URP 2D). Install it with Unity Hub or the Unity CLI.
- The backend API from the sibling repo [`../exam-explainer-api`](../exam-explainer-api): Python 3.13+, [uv](https://docs.astral.sh/uv/) and a MongoDB instance.

The game never talks to the database. The backend holds the answer key and decides whether an answer is right, so the client only learns the correct answer after answering.

## Getting started

1. **Start the backend** (see its README for `.env` values such as `DATABASE_URL`):

   ```bash
   cd ../exam-explainer-api
   uv sync
   uv run fastapi dev        # http://127.0.0.1:8000, docs at /docs
   ```

   Questions come from importing an exam file (`POST /examination/import`); see the backend README.

2. **Open this folder** in Unity Hub with editor 6000.6.3f1. The first import takes a while.

3. **Set up the scene** once: menu **Tools ▸ ExamCards ▸ Setup PickOne Scene**. It creates `Assets/Resources/ExamApiSettings.asset` and `Assets/UI/PanelSettings.asset` if missing and wires the `PickOneUI` GameObject into `SampleScene`. Re-run it after changing serialized references instead of editing the scene YAML.

4. **Open `SampleScene` and press Play.**

If the backend is down or has no playable questions, the game shows a message and a Retry button.

## Configuration

`Assets/Resources/ExamApiSettings.asset` holds:

| Setting | Default | Meaning |
| --- | --- | --- |
| Base URL | `http://127.0.0.1:8000` | Backend address, absolute http(s) URL |
| Questions per round / later batch | 10 (max 50) | Size of the first round and of each later batch |
| Search results | 8 (max 50) | Results per search query |
| Request timeout | | Per-request timeout |

Without the asset the code defaults apply. Plain `http://` is allowed only in the Editor and development builds; a release build must use `https://`. `Assets/Editor/ExamApiBuildCheck.cs` fails a build with an invalid URL, or a release build without `https://`.

## Controls

| Action | Input |
| --- | --- |
| Pick an answer | Click, or keys `1`–`4` |
| Reveal simplified text | Click or `R` |
| 50/50 | Click or `F` |
| Next / Play Again / Retry | Click, `Enter` or `Space` |
| Search | Header button or `/` (then `↑`/`↓`, `Enter`, `Esc`) |

## Project layout

| Path | Contents |
| --- | --- |
| `Assets/Scripts/Exam/` | Domain (asmdef `ExamCards.Exam`, no engine references): questions, reviews, `ExamSession`, 50/50, mock service and validators |
| `Assets/Scripts/Exam.Api/` | Backend client: `/v1/game` HTTP calls, DTOs and mapper, settings |
| `Assets/UI/PickOne/` | The screen: `PickOne.uxml`, `AnswerCard.uxml`, `PickOne.uss` and the views (`PickOneScreen`, `SearchPanelView`, `AnswerCardView`, ...) |
| `Assets/Resources/EntityIcons/` | Icon SVGs, one per key of the backend's icon catalog |
| `Assets/Editor/` | Scene setup, icon import and build checks |
| `Assets/Tests/EditMode/` | EditMode tests and the contract JSON files |

See [CLAUDE.md](CLAUDE.md) for the detailed architecture and UI conventions.

## Tests

Run them from **Window ▸ General ▸ Test Runner** (EditMode), or with the Unity CLI against a running Editor:

```bash
unity command test run --mode EditMode --filter ExamCards
```

`Assets/Tests/EditMode/Contract/*.json` are real backend responses written by the backend's `tests/test_contract.py`. Don't edit them by hand. After an intended contract change run `UPDATE_CONTRACT=1 uv run --with pytest pytest tests/test_contract.py` in the backend and commit both repos.

## Icons

Don't add icons by hand. Add the key to the backend's `icon_catalog.py`, run its `scripts/export_game_icons.py` (writes the SVGs into `Assets/Resources/EntityIcons/`), then update the contract as above. Icons are [Lucide](https://lucide.dev) glyphs (ISC license, see `Assets/Resources/EntityIcons/LICENSE.txt`).

## Working with the Editor from the command line

With the Unity CLI and a running Editor:

```bash
unity status
unity recompile
unity command editor.playmode start     # or stop
unity command console get -- --tail 20 --level warn
unity command editor invoke_menu --path "Tools/ExamCards/Setup PickOne Scene"
```

## Notes

- Every file and folder under `Assets/` has a paired `.meta` file. Move or rename files together with their `.meta`, ideally inside the Editor.
- `Library/`, `Temp/`, `obj/`, `Logs/` and `UserSettings/` are generated and not committed.
- The header navigation, coins, timer, streak and the Freeze and Codex Lore power-ups are static placeholders for now.
- The input handler is the Input System only (`Keyboard.current`), not legacy `Input`.
