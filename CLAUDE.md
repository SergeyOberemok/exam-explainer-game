# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ExamCards is a Unity 6 (`6000.6.3f1`, URP 2D) quiz game. The player sees exam questions one at a time. Each question has several answer cards, and exactly one is correct. A **Reveal** button swaps the current question's text for a simplified version. In the Editor, questions come straight from MongoDB (database `exam-explainer`); player builds use a mock until a backend API exists. The UI is built with **UI Toolkit** (UXML/USS), not uGUI. It started from the Figma file "ExamCards" (`yklzlKknayD83RhAn8BCRM`), page **PickOne** (root frame `4:2`, 1280×1024), and has since been reworked for long, text-only questions.

## Architecture

- **Domain — `Assets/Scripts/Exam/`** (asmdef `ExamCards.Exam`, `noEngineReferences: true`, so it is plain C# and unit-testable):
  - `ExamItem` / `QuestionPrompt` mirror a document in the `examination` or `examination_simplified` collection. `QuestionAssembler.Build` joins them (`question_id` is a string holding the original's `_id`), converts the correct letter to an index, and throws on inconsistent data, naming the question.
  - `Question` is the playable model: `Original`, optional `Simplified`, and `CorrectIndex`, which is the same for both versions.
  - `IQuestionService` is the question source. `QuestionServices.Create()` picks it: `Factory` if set, otherwise `MockQuestionService`, which also serves tests and player builds. A real API service should implement the same interface.
  - `ExamSession` walks the questions and keeps the score: `Answer(index)` returns an `AnswerResult` and records `AnsweredIndex`, `Reveal()` switches `CurrentPrompt` to the simplified text until the next question and doesn't affect the score, and `MoveNext()` throws if the current question is unanswered, and returns false and sets `IsFinished` at the end.
  - **50/50:** `Eliminate()` removes two random wrong answers from the current unanswered question and uses one of the exam's `FiftyFiftyRemaining` charges (2 by default). It works once per question, and `Answer` throws for an eliminated option. Eliminated answers clear on `MoveNext()`.
  - A question has at most `Question.MaxAnswers` (4) answers, which `QuestionAssembler` enforces, because the screen has one key per answer.
- **MongoDB — `Assets/Scripts/Exam.Mongo/`** (asmdef `ExamCards.Exam.Mongo`, **Editor only**):
  - `MongoQuestionService` reads `DATABASE_URL` from the git-ignored `.env` in the project root and registers itself as `QuestionServices.Factory` on load. Without a `.env` it doesn't register, so the Editor uses the mock.
  - The driver DLLs are in `Assets/Plugins/MongoDB/` (MongoDB.Driver 3.13.0 and dependencies, `netstandard` builds from NuGet), set to Editor only and explicitly referenced. Together these keep the driver and the credentials out of player builds, so **never ship them or make them available on other platforms**.
  - The connection pins TLS 1.2. Under TLS 1.3, UnityTLS fails the Atlas handshake with "Cannot request client certificate before receiving one from the server".
  - The first load after a domain reload can be slow if the machine's primary DNS server doesn't answer `mongodb+srv` SRV lookups. Keep the driver's default 30s server selection timeout.
- **UI — `Assets/UI/PickOne/`** (lives in Assembly-CSharp):
  - `PickOne.uxml` is the whole screen. `AnswerCard.uxml` is the per-answer card template, instantiated from code into `cardsRow`.
  - `PickOne.uss` holds all styles. The palette lives as `:root` variables.
  - `PickOneScreen` (MonoBehaviour on the `UIDocument` GameObject) loads questions and handles card picks (click or keys 1–4), Reveal (click or R), and Next, Play Again and Retry (click, Enter or Space), then shows the results overlay. A failed or empty load shows a message and the Retry button. `RenderPrompt()` draws the current text and state, and runs for a new question, an answer and a reveal.
  - `AnswerCardView` binds one card's text and switches state only through USS classes (`answer-card--interactive|correct|wrong|dimmed`). `Bind` doesn't touch state, so a reveal keeps a card's result colours.
  - `GradientElement` is a `[UxmlElement]` that draws gradients with vertex colors, because USS has no gradients.
  - The 50/50 power-up (`fiftyFiftyButton`, click or key F) is wired to `ExamSession.Eliminate()`; eliminated cards use the `answer-card--eliminated` state.
  - Header nav, coins, timer, lives, streak and the other two power-ups (Freeze, Codex Lore) are **static placeholders**; they are not wired up yet.
- **Scene setup**: the menu **Tools ▸ ExamCards ▸ Setup PickOne Scene** (`Assets/Editor/PickOneSceneSetup.cs`) creates `Assets/UI/PanelSettings.asset` (uses theme `Assets/UI/ExamTheme.tss`, scale-with-screen 1280×1024) and wires the `PickOneUI` GameObject into `SampleScene`. Re-run it after changing serialized references instead of editing the scene YAML.

## UI Toolkit conventions in this project

- USS has no `box-shadow`, `gap`, or gradients. The neo-brutal hard shadows are done two ways:
  - **1–2 px offsets:** thicker right/bottom borders.
  - **3–4 px offsets:** a `.brutal.brutal--s3|s4` wrapper whose first child is an absolute `.brutal__shadow`, translated under the real `.box`.
- `name` attributes are camelCase; classes are BEM-style kebab-case. Avoid inline `style` in UXML; the only inline style set from C# is a data-driven value (progress width).
- Labels showing exam data set `enable-rich-text="false"`, because the text can contain literal tags such as `<example>`.
- Buttons are `focusable="false"`, so Enter and Space only act through `PickOneScreen`'s key handling and don't also trigger a focused button.
- SVG icons in `Icons/` import as `VectorImage` and are referenced from USS via `url("project://database/...")`.
- Fonts: the design uses Outfit and Nunito Sans, but the project currently uses Unity's default font.

## Working with the Editor

- A running Editor can be driven with the Unity CLI: `unity status`, `unity recompile`, `unity command eval '<C#>'`, `unity command capture game_view -- --source screen --save_path ...` (the path is relative to `Assets/`, so delete what you capture), `unity command editor.playmode start|stop`, `unity command console get -- --tail 20 --level warn`, and `unity command editor invoke_menu --path "Tools/ExamCards/Setup PickOne Scene"`.
- Tests: `unity command test run --mode EditMode --filter ExamCards`, or Window ▸ General ▸ Test Runner. The EditMode tests are in `Assets/Tests/EditMode/` (asmdef `ExamCards.Exam.Tests`).
- The active input handler is the Input System only (`Keyboard.current`), not legacy `Input`.

## Conventions for editing

- Every file and folder under `Assets/` has a paired `.meta` file holding its GUID. Never delete or recreate a file without its `.meta`. Move or rename files so the `.meta` goes with them, ideally inside the Editor.
- `Library/`, `Temp/`, `obj/`, `Logs/`, and `UserSettings/` are generated and should not be touched or committed.
