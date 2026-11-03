# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ExamCards is a Unity 6 (`6000.6.3f1`, URP 2D) quiz game. The player sees exam questions one at a time. Each question has several answer cards, and exactly one is correct. A **Reveal** button swaps the current question's text for a simplified version. Questions come from a FastAPI backend (`/game` routes, `http://127.0.0.1:8000` by default) in both the Editor and player builds; the game never talks to the database, and the backend, not the game, decides whether an answer is right. The UI is built with **UI Toolkit** (UXML/USS), not uGUI. It started from the Figma file "ExamCards" (`yklzlKknayD83RhAn8BCRM`), page **PickOne** (root frame `4:2`, 1280×1024), and has since been reworked for long, text-only questions.

## Architecture

- **Domain — `Assets/Scripts/Exam/`** (asmdef `ExamCards.Exam`, `noEngineReferences: true`, so it is plain C# and unit-testable):
  - **The client never holds the answer key.** `Question` is what the player sees: `Original` and optional `Simplified` `QuestionPrompt`s (question text and answers only). The correct answer, per-answer feedback and explanation arrive only after answering, as an `AnswerReview` (`CorrectIndex`, plus a `PromptReview` for each version; `Simplified` is null when there is none).
  - `IExamRound` is one round held by whoever has the key: its `Questions`, `FiftyFiftyCharges`, `SubmitAnswerAsync(questionId, index)` → `AnswerReview`, and `EliminateAsync(questionId)` → removed indices. Each question is answered once; sending the same answer (or 50/50) again repeats the result, so a retry after a lost response is safe.
  - `IQuestionService.StartRoundAsync()` starts a round. `QuestionServices.Create()` picks the service: `Factory` if set (the API assembly sets it), otherwise `MockQuestionService`, which EditMode tests use. A round can't be replayed, so Play Again starts a new one.
  - `LocalExamRound` is an `IExamRound` over `KeyedQuestion`s (question + review) with the same rules as the backend; the mock and the tests use it.
  - `QuestionValidator` checks questions (1..`Question.MaxAnswers` (4) answers, simplified has the same count), reviews (correct index in range, one feedback per answer) and 50/50 results, throwing `FormatException` that names the question.
  - `Api/GameQuestionDtos.cs` mirrors the backend's JSON as `[Serializable]` classes with snake_case public fields, as `JsonUtility` needs. `Api/GameQuestionMapper` turns a round response into `Question`s (answers ordered by `index`; rejects duplicates, wrong `simplified` flags and simplified versions without an original), and answer/50/50 responses into reviews and indices (rejecting a response for another question or one whose `is_correct` contradicts `correct_index`).
  - `ExamSession(round)` walks the questions and keeps score and lives (`MaxLives` 3, a wrong answer costs one; the exam ends after the question that uses the last). `AnswerAsync(index)` and `EliminateAsync()` go through the round; only one runs at a time (`IsBusy`), and a failure leaves the question unchanged so the player can try again. `CurrentReview`/`CurrentPromptReview` hold the feedback for the shown version. `Reveal()` switches `CurrentPrompt` to the simplified text until the next question and doesn't affect the score. `MoveNext()` throws if the current question is unanswered, and returns false and sets `IsFinished` at the end.
  - **50/50** removes two wrong answers (`EliminatedPerUse`) from the current unanswered question, once per question, within the round's charges (2 by default); the round picks them, because only it knows which are wrong. Eliminated answers clear on `MoveNext()`.
- **Backend API — `Assets/Scripts/Exam.Api/`** (asmdef `ExamCards.Exam.Api`, all platforms):
  - `ApiQuestionService.StartRoundAsync` sends one `POST /game/rounds {"count": N}`, which returns the questions and their simplified versions without the answer key. `ApiExamRound` then posts to `/game/rounds/{round_id}/answers` and `/fifty-fifty`. Rounds expire on the server after a day.
  - HTTP goes through `IHttpTransport`: `UnityWebRequestTransport` in the game (main thread only, awaited with `Awaitable.FromAsyncOperation`; cancelling the token aborts the request, so cancel on the main thread), a fake in the tests. `GameApiClient` posts JSON and throws `ApiException` (method, URL, status, and FastAPI's `detail`) or `FormatException` for a bad body.
  - `ApiQuestionService` registers itself as `QuestionServices.Factory` in `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.
  - `ExamApiSettings` (ScriptableObject at `Assets/Resources/ExamApiSettings.asset`, created by the setup menu) holds the base URL, questions per round (10, max 50) and request timeout. Without the asset the code defaults apply. `TryValidateBaseUrl` requires an absolute http(s) URL; `OnValidate` warns in the Inspector.
  - Plain `http://` is allowed only in the Editor and development builds (`PlayerSettings.insecureHttpOption = DevelopmentOnly`). `Assets/Editor/ExamApiBuildCheck.cs` fails a build with an invalid URL, and a release build whose URL isn't `https://`.
  - The backend's OpenAPI spec is at `<base URL>/openapi.json` (docs at `/docs`). Its source is the sibling repo `../exam-explainer-api` (`src/claude_exam/api/routers/game.py`).
- **UI — `Assets/UI/PickOne/`** (lives in Assembly-CSharp):
  - `PickOne.uxml` is the whole screen. `AnswerCard.uxml` is the per-answer card template, instantiated from code into `cardsRow`.
  - `PickOne.uss` holds all styles. The palette lives as `:root` variables.
  - `PickOneScreen` (MonoBehaviour on the `UIDocument` GameObject) starts a round and handles card picks (click or keys 1–4), Reveal (click or R), 50/50 (click or F), and Next, Play Again and Retry (click, Enter or Space), then shows the results overlay. It creates the question service inside the load, so a bad setting ends in the failure message too. A failed or empty load shows a message and the Retry button. A failed answer or 50/50 shows `statusText` (in the question's chip row) and leaves the question playable. `RenderPrompt()` draws the current text and state, and runs for a new question, an answer, a 50/50 and a reveal. Cancelling `_roundCancellation` (on disable and on each new round) drops any request still running.
  - `AnswerCardView` binds one card's text and switches state only through USS classes (`answer-card--interactive|correct|wrong|dimmed|eliminated`). `Bind` doesn't touch state, so a reveal keeps a card's result colours.
  - `GradientElement` is a `[UxmlElement]` that draws gradients with vertex colors, because USS has no gradients.
  - Lives (`hearts`) follow `ExamSession.Lives`.
  - Header nav, coins, timer, streak and the other two power-ups (Freeze, Codex Lore) are **static placeholders**; they are not wired up yet.
- **Scene setup**: the menu **Tools ▸ ExamCards ▸ Setup PickOne Scene** (`Assets/Editor/PickOneSceneSetup.cs`) creates `Assets/Resources/ExamApiSettings.asset` if missing and `Assets/UI/PanelSettings.asset` (uses theme `Assets/UI/ExamTheme.tss`, scale-with-screen 1280×1024) and wires the `PickOneUI` GameObject into `SampleScene`. Re-run it after changing serialized references instead of editing the scene YAML.

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
