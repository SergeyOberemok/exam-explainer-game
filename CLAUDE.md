# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ExamCards is a Unity 6 (`6000.6.3f1`, URP 2D) quiz game. The player sees exam questions one at a time. Each question has several answer cards, and exactly one is correct. Questions will come from a backend API; for now they come from a mocked service. The UI is built with **UI Toolkit** (UXML/USS), not uGUI. It implements the Figma file "ExamCards" (`yklzlKknayD83RhAn8BCRM`), page **PickOne** (root frame `4:2`, 1280×1024).

## Architecture

- **Domain — `Assets/Scripts/Exam/`** (asmdef `ExamCards.Exam`, `noEngineReferences: true`, so it is plain C# and unit-testable):
  - `Question` / `AnswerOption` are the data models, shaped for later JSON deserialization from the API. `Image` / `ClueImage` hold an image key (mock) or a URL (real API).
  - `IQuestionService` is the question source. `MockQuestionService` returns hardcoded questions after a fake delay. A real API service should implement the same interface, and `PickOneScreen` is where the service gets created.
  - `ExamSession` walks the questions and keeps the score: `Answer(index)` returns an `AnswerResult`, `MoveNext()` returns false at the end.
- **UI — `Assets/UI/PickOne/`** (lives in Assembly-CSharp):
  - `PickOne.uxml` is the whole screen. `AnswerCard.uxml` is the per-option card template, instantiated from code into `cardsRow`.
  - `PickOne.uss` holds all styles. The palette lives as `:root` variables.
  - `PickOneScreen` (MonoBehaviour on the `UIDocument` GameObject) loads questions, binds the current one, handles clicks and keys 1–4, applies correct/wrong feedback, advances after `_feedbackSeconds`, and shows the results overlay. Mock images resolve by texture name from its serialized `_images` array.
  - `AnswerCardView` binds one card and switches state only through USS classes (`answer-card--interactive|correct|wrong|dimmed`).
  - `GradientElement` is a `[UxmlElement]` that draws gradients with vertex colors, because USS has no gradients.
  - Header nav, coins, timer, lives, streak and power-ups are **static placeholders**; they are not wired up yet.
- **Scene setup**: the menu **Tools ▸ ExamCards ▸ Setup PickOne Scene** (`Assets/Editor/PickOneSceneSetup.cs`) creates `Assets/UI/PanelSettings.asset` (uses theme `Assets/UI/ExamTheme.tss`, scale-with-screen 1280×1024) and wires the `PickOneUI` GameObject into `SampleScene`. Re-run it after changing serialized references instead of editing the scene YAML.

## UI Toolkit conventions in this project

- USS has no `box-shadow`, `gap`, or gradients. The neo-brutal hard shadows are done two ways:
  - **1–2 px offsets:** thicker right/bottom borders.
  - **3–4 px offsets:** a `.brutal.brutal--s3|s4` wrapper whose first child is an absolute `.brutal__shadow`, translated under the real `.box`.
- `name` attributes are camelCase; classes are BEM-style kebab-case. Avoid inline `style` in UXML; the only inline styles set from C# are data-driven values (images, progress width).
- SVG icons in `Icons/` import as `VectorImage` and are referenced from USS via `url("project://database/...")`.
- Fonts: the design uses Outfit and Nunito Sans, but the project currently uses Unity's default font.

## Working with the Editor

- A running Editor can be driven with the Unity CLI: `unity status`, `unity recompile`, `unity command eval '<C#>'`, `unity command capture game_view --save_path ...` (the path is relative to `Assets/`, so delete what you capture), and `unity command editor invoke_menu --path "Tools/ExamCards/Setup PickOne Scene"`.
- Tests: `unity command test run --mode EditMode --filter ExamCards`, or Window ▸ General ▸ Test Runner. The EditMode tests are in `Assets/Tests/EditMode/` (asmdef `ExamCards.Exam.Tests`).
- The active input handler is the Input System only (`Keyboard.current`), not legacy `Input`.

## Conventions for editing

- Every file and folder under `Assets/` has a paired `.meta` file holding its GUID. Never delete or recreate a file without its `.meta`. Move or rename files so the `.meta` goes with them, ideally inside the Editor.
- `Library/`, `Temp/`, `obj/`, `Logs/`, and `UserSettings/` are generated and should not be touched or committed.
