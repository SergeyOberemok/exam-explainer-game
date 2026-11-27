# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ExamCards is a Unity 6 (`6000.6.3f1`, URP 2D) quiz game. The player meets exam questions one at a time, as the backend composes them: a story beat, the briefing (the scenario's facts) item by item, the ask, and the answers as cards ("tools"), exactly one of them correct. After answering, every card shows what its answer leads to. A **search** modal (header button or `/`) finds any question by text and plays it right away. Questions come from a FastAPI backend (`/v2/game` routes, `http://127.0.0.1:8000` by default) in both the Editor and player builds. The game never talks to the database, and the backend, not the game, decides whether an answer is right. The UI is built with **UI Toolkit** (UXML/USS), not uGUI. It started from the Figma file "ExamCards" (`yklzlKknayD83RhAn8BCRM`), page **PickOne** (root frame `4:2`, 1280×1024), and has since been reworked for long, text-only questions.

## Architecture

- **Domain — `Assets/Scripts/Exam/`** (asmdef `ExamCards.Exam`, `noEngineReferences: true`, so it is plain C# and unit-testable):
  - **The client never holds the answer key.** `Question` is the backend's composed card without the key:
    - `Text` is the whole original. `Tools` are the answers in index order (`AnswerTool`: `Index`, `Text`, `Label` (what it proposes, unjudged), `Icon`, `ComponentRefs`), and `Answers` are their texts.
    - `Briefing` is the facts without the ask, in order (`QuestionPart`: `PartRole` context/current_state/problem/consequence/goal/constraint, `Text`, `EntityRefs`). `Ask` is the question itself as a `QuestionPart` (null without a decomposition).
    - `Cast`, `Actions` and `Flow` (the map) are described below, along with `Icon`, `Objectives`, `Difficulty`, `Scenario`, `Stage`, `Stem` and `Beat`.
  - **After answering**, the key arrives as an `AnswerReview`:
    - `CorrectIndex` and `Review` (a `PromptReview`: the source's `Explanation` and one `Feedbacks` entry per answer).
    - `Resolution`, `StoryLine` (the chapter's win or fail line for the answer that counts), and `AnsweredIndex` (the answer that counts: the one sent, or an earlier one the round had already recorded; null only in hand-built reviews).
    - `MisconceptionFor(i)` is that answer's misconception (empty for the correct one).
  - `IExamRound` is one round held by whoever has the key. It has:
    - its starting `Questions`, `FiftyFiftyCharges` and `HasMore`
    - `SubmitAnswerAsync(questionId, index)` → `AnswerReview`
    - `EliminateAsync(questionId)` → `FiftyFiftyResult` (removed indices and the round's charges left)
    - `FetchMoreAsync()` → the next batch of questions it hasn't had (empty when used up)

    Each question is answered once. The same answer (or 50/50) again repeats the result, and a *different* answer returns the one recorded first, so a lost response can't leave a question stuck.
  - `IExamRound.AddQuestionAsync(questionId)` adds a question (a search result) to the round so it can be answered there; adding one it already has returns it again. `Questions` stays the starting list.
  - **Cast** (`Cast.cs`): `Question.Cast` holds every entity the question names. The briefing, the ask, the tools, the actions and the flow nodes refer to it by `Ref`.
    - A `CastMember` has `Ref`, `Name`, a `CastType` (actor, system, object, knowledge_source, artifact, external_service, concept; an unknown type reads as object), `Aliases`, `Icon` and `InScene`.
    - `InScene` is false for a member only an answer names. It then shows only on that answer's card, so the question doesn't give the answer away.
    - `SceneCast` is the question's own chips. `ToolCast(i)` is the members answer `i` is made of that the scene doesn't have. `ToolComponents(i)` is all of them, those first, which the card shows. `CastFor(refs)` and `FindCast(ref)` look members up and skip unknown refs.
    - `Question.Actions` (`QuestionAction`: `Ref`, `Name`, `Sequence`, `ActorRef`, `TargetRef`, `InputRefs`, `OutputRefs`, `Icon`) are the scenario's events in order. They are data for the planned interactive icons; nothing shows them yet.
  - **Icon keys:** the backend, not the game, decides which icon fits.
    - It picks from its icon catalog (`../exam-explainer-api/src/claude_exam/api/icon_catalog.py`) and sends provider-neutral keys (`subagent`, `config-file`): `CastMember.Icon`, `QuestionAction.Icon`, `Question.Icon` (the question as a whole) and `AnswerTool.Icon` / `IconForAnswer(i)` (what each answer proposes). Any of them can be null.
    - `EntityIconResolver.Resolve(member)` returns the member's key, or else the key of its type: `actor` for actors, `object` for every other type, because the catalog has no keys for the others yet. `Resolve(action)` falls back to `action`.
    - `CleanKey` accepts only lower-case words joined by hyphens, because a key is used as a resource name. `DefaultIcon` is for a question or answer without a key.
  - **Learning objectives:** the backend tags questions from its objective catalog (`../exam-explainer-api/src/claude_exam/api/objective_catalog.py`).
    - `Question.Objectives` holds the keys (main one first, empty when untagged), and `Question.Difficulty` is 1–3 (0 when untagged).
    - Each answer's outcome carries a misconception: one sentence on what picking it suggests the player got wrong (empty for the correct answer). Like the rest of the key, it only arrives after answering.
    - The mapper drops anything that isn't a key instead of failing a round.
    - The screen shows a difficulty chip (`QuestionLabels.Difficulty`). Only after answering does it show the main objective (`QuestionLabels.Objective`; the key shown as text, since a key can name the answer) and, in the debrief, the misconception of the answer that counts. The rest is data for mastery tracking.
    - `GET /v2/game/objectives` (catalog with playable counts) and the `objective` filter of `POST /v2/game/rounds` exist on the backend, but the game doesn't call them yet.
  - **Story:** the backend frames the bank as one story (`../exam-explainer-api/src/claude_exam/api/scenario_catalog.py`): one company, a chapter per exam scenario, the same stages in each.
    - `Question.Scenario` and `Question.Stage` are the chapter and stage keys. `Question.Beat` is one line to show before the question. `Question.Stem` is the original text without the scenario paragraph the source repeats in every question of a chapter. All are empty when the backend has none.
    - `Question.OriginalText` is what to show for the original: the stem, or the whole `Text` without one.
    - `AnswerReview.StoryLine` is the chapter's line after the answer that counts, and each answer's outcome has its own `Consequence`.
    - The mapper trims the lines and drops a key that isn't one.
    - `GET /v2/game/story` (chapters and stages with counts) and the `scenario` / `stage` / `ordered` fields of `POST /v2/game/rounds` exist on the backend, but the game doesn't call them yet. Rounds are still random, and the map that would use them is planned.
  - **Flows and outcomes** (`DecisionFlow.cs`, `Resolution.cs`): `Question.Flow` is the pre-answer map (`DecisionFlow`: `Nodes` with `FlowNodeType`, `IsQuestion` on the decision the answer cards hang on, and `Edges`; `IsEmpty` without one).
    - After answering, `AnswerReview.Resolution` (part of the key) has `AnswerRequirements`, `Flow` (the map continued with steps marked `Added`), and one `AnswerOutcome` per answer.
    - An `AnswerOutcome` has `IsCorrect`, `Impact` (Success/Partial/Failure), `Consequence`, `Misconception`, `Reason`, `Gaps` and `SupportedRequirements`. `ForAnswer(i)` is null when it is missing. A one-line `Rule` comes with them.
    - The backend names supported requirements by their text, so `RequirementsMetBy(i)` / `MetCount(i)` match them by trimmed text and ignore any that isn't one.
    - `HasRequirementMatches` is false when no answer names any (documents composed before the backend sent them), and the screen then hides the counts.
    - Node ids are only unique within one flow: a resolution can drop the decision and give its id to a new step, so its steps are told apart by `Added`, never by comparing ids with the question's flow.
  - **Map** (`FlowMapModel.cs`, `FlowLayout.cs`): what the map modal draws. The game draws these structured flows itself; it doesn't use the decomposition's `mermaid` strings (those aren't in the game payload, and Unity can't render mermaid).
    - `FlowMapModel.ForQuestion(question, isEliminated)` is the flow plus an answer box per tool (its `Label` and icon) hanging off the `IsQuestion` node by a dashed edge, with 50/50 answers dimmed. It shows only `InScene` cast members on the nodes, so the map can't give the answer away.
    - `ForResolution(question, review)` is `Resolution.Flow` with every cast member, `Added` steps highlighted and the `Rule` as the caption.
    - `MapEdge.Style` is `Added`, `Inferred` (`FlowStatus.Inferred`), `Answer` or `Normal`. An edge without a label shows its branch type (yes/no/approved/rejected).
    - `FlowLayout.Compute(nodes, links, size)` lays out top to bottom (like mermaid's `flowchart TD`):
      - ranks by longest path, ignoring the links that close a cycle;
      - gives a link that skips ranks a lane in each rank it passes;
      - orders each rank by barycenter sweeps;
      - centres each node under its parents and each root over its children. Neighbours that would overlap move as a block centred on what they want, so the answers fan out under the decision;
      - routes a link that closes a cycle upwards: from its source's top through the rank gaps and a column right of every node it passes, into its target's bottom.

      It returns a rect per node, a point path per link (an empty one for a link it can't draw), and a label centre per labelled link (`FlowLink.LabelSize`). A label goes in the gap below its source, never in a lane beside other nodes; a link going up gets its label in its column, made wide enough for it. It is plain C# with its own `FlowPoint`/`FlowRect`, tested in `FlowLayoutTests` on the headless-sessions question's map.
    - A node's `Icon` is the backend's key for the step (null when it sends none or a bad one); the box shows it as its glyph when the game ships it, else a glyph for its `FlowNodeType`, and the cast's icons below, since the "first ref" isn't reliably what a node is about.
  - `IQuestionSearch.SearchAsync(text)` finds playable questions as whole cards, without the key. `QuestionSearchText.Normalize` collapses whitespace and line breaks, trims, and allows 2 to 2000 chars, so a whole pasted question fits. The API and mock services implement it.
  - `IQuestionService.StartRoundAsync()` starts a round (throws `NoQuestionsException` when nothing is playable). `QuestionServices.Create()` picks the service: `Factory` if set (the API assembly sets it), otherwise `MockQuestionService`, which EditMode tests use. A round can't be replayed, so Play Again starts a new one.
  - `LocalExamRound` is an `IExamRound` over `KeyedQuestion`s with the same rules as the backend; the mock and the tests use it.
    - A `KeyedQuestion` is a question plus its review, `Win` and `Fail`. `ReviewFor(i)` gives the review the backend would send for answer `i`.
    - Its optional `catalog` holds questions `AddQuestionAsync` may add (the mock's search finds two extra questions there).
    - `more` (with `batchSize`) feeds `FetchMoreAsync`, skipping anything already in the round.
  - `QuestionValidator` checks questions (1..`Question.MaxAnswers` (4) answers, tool indices 0..n-1), reviews (correct and recorded index in range, one feedback per answer) and 50/50 results, throwing `FormatException` that names the question.
  - `Api/GameQuestionDtos.cs` mirrors the backend's `/v2` JSON as `[Serializable]` classes with snake_case public fields, as `JsonUtility` needs. `Api/GameQuestionMapper` splits strict from lenient:
    - **Strict (what decides play):** tools are ordered by `index` and must be 0..n-1. It rejects duplicate questions, a batch for another index, an added question that isn't the one asked for, an answer response for another question, one whose `is_correct` contradicts `correct_index`, and one without a review (`JsonUtility` turns missing fields into defaults instead of failing).
    - **Lenient (what only describes a question):** it drops cast members without a ref or name, refs that name no member, unknown briefing roles and flow types, dangling edges, and icons that aren't keys. It also drops bad outcomes, and all of them when a verdict contradicts `correct_index`.
    - Outcomes come from `outcomes`, or from the answer's own `outcome` when a backend sends no list.
  - `ExamSession(round)` walks the questions and keeps score and lives (`MaxLives` 3; a wrong answer costs one, a partial one too, since impact is only visual for now; the exam ends after the question that uses the last).
    - **One round goes on in batches:** `NeedsMore` is true when the current question is answered, is the last one loaded, and the round `HasMore`. `LoadMoreAsync()` then appends the round's next batch (never a question already played or added by search), and `MoveNext()` continues. So the exam runs until the lives or the questions run out (`IsLastQuestion`). `MoveNext()` throws while `NeedsMore`.
    - The answer that counts is `review.AnsweredIndex ?? the pick`. The 50/50 count is taken from the round.
    - `AnswerAsync(index)` and `EliminateAsync()` go through the round. Only one runs at a time (`IsBusy`), and a failure leaves the question unchanged so the player can try again. `CurrentReview` holds the review.
    - `MoveNext()` throws if the current question is unanswered or a request is running. At the end it returns false and sets `IsFinished`.
  - `PlayNowAsync(questionId)` (search) makes a question current. One later in the exam moves up; a new one is added through the round first and counts towards `Total`. An answered current question stays put; an unanswered one is shown again right after. It returns `AlreadyCurrent`/`AlreadyAnswered` without changes, and throws when `!CanPlayNow` (busy, finished or out of lives). `IndexOf(id)` tells where a question is in play order.
  - **50/50** removes two wrong answers (`EliminatedPerUse`) from the current unanswered question, once per question, within the round's charges (2 by default). The round picks them, because only it knows which are wrong. Eliminated answers are kept per question id, so a question skipped by `PlayNowAsync` keeps them.
- **Backend API — `Assets/Scripts/Exam.Api/`** (asmdef `ExamCards.Exam.Api`, all platforms):
  - All paths start with `ApiQuestionService.Prefix` (`/v2`).
    - `ApiQuestionService.StartRoundAsync` sends one `POST /v2/game/rounds {"count": N}`, which returns the composed cards without the answer key. `HasMore` is set when the backend's `total` exceeds them.
    - `ApiExamRound` then posts to `/v2/game/rounds/{round_id}/answers` (all outcomes come back), `/fifty-fifty`, `/questions` (add a search result) and `/batches {batch_index, count}` (`FetchMoreAsync`). The batch index only advances on a non-empty batch, so a retry gets the same batch.
    - On 409 `already_answered` it reads the recorded answer from `GET …/answers/{question_id}`.
    - `SearchAsync` sends `POST /v2/search/game-questions {q, limit}`; a body, because a pasted question can pass a URL limit.
    - Rounds expire on the server after a day.
    - The backend's `/v1` no longer has game routes. `/v2/game/questions`, `/sections` and GET search exist, but play doesn't need them.
  - HTTP goes through `IHttpTransport`: `UnityWebRequestTransport` in the game, and a fake in the tests. `UnityWebRequestTransport` runs on the main thread only and is awaited with `Awaitable.FromAsyncOperation`; cancelling the token aborts the request, so cancel on the main thread.
  - `GameApiClient` sends JSON (`PostAsync`, `GetAsync`). It throws `ApiException` (method, URL, status, the backend's `detail` and `Code`, e.g. `ApiErrorCodes.AlreadyAnswered`) or `FormatException` for a bad body.
  - An idempotent request (everything except starting a round) is retried once after `DefaultRetryDelaySeconds` (0.5 s, via `Awaitable.WaitForSecondsAsync`) on a transient failure: no response, 5xx or `round_changed`.
  - Round and search routes are rate-limited. A 429 is `ApiException.IsRateLimited`; it isn't retried, and the screen asks the player to wait a moment.
  - `ApiQuestionService` registers itself as `QuestionServices.Factory` in `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.
  - `ExamApiSettings` (ScriptableObject at `Assets/Resources/ExamApiSettings.asset`, created by the setup menu) holds:
    - the base URL
    - questions per round and per later batch (10, max 50)
    - search results per query (8, max 50)
    - the request timeout

    Without the asset the code defaults apply. `TryValidateBaseUrl` requires an absolute http(s) URL; `OnValidate` warns in the Inspector.
  - Plain `http://` is allowed only in the Editor and development builds (`PlayerSettings.insecureHttpOption = DevelopmentOnly`). `Assets/Editor/ExamApiBuildCheck.cs` fails a build with an invalid URL, and a release build whose URL isn't `https://`.
  - The backend's OpenAPI spec is at `<base URL>/openapi.json` (docs at `/docs`). Its source is the sibling repo `../exam-explainer-api`: `src/claude_exam/api/routers/game_v2.py`, with the cards composed by `compose.py`.
- **UI — `Assets/UI/PickOne/`** (lives in Assembly-CSharp):
  - `PickOne.uxml` is the whole screen. `AnswerCard.uxml` is the per-answer card template, instantiated from code into `cardsRow`.
  - `PickOne.uss` holds all styles. The palette lives as `:root` variables.
  - `PickOneScreen` (MonoBehaviour on the `UIDocument` GameObject) starts a round and handles input: card picks (click or keys 1–4; once answered they flip the card), 50/50 (click or F), Q (debrief/question tab), M (map), and Next, Play Again and Retry (click, Enter or Space). At the end it shows the results overlay.
    - It creates the question service inside the load, so a bad setting ends in the failure message too.
    - A failed or empty load (including `NoQuestionsException`) shows a message and the Retry button. In a player build a missing service factory is logged as an error instead of quietly serving mock questions.
    - Next on the last loaded question loads the round's next batch (`LoadMoreAndAdvance`, status "Loading more questions…"; Next retries a failure). The results show the score out of answered questions.
    - A failed answer or 50/50 shows `statusText` (in the question's chip row) and leaves the question playable.
    - `RenderPrompt()` draws the current text and state. It runs for a new question, an answer, a 50/50 and a scenario toggle.
    - Cancelling `_roundCancellation` (on disable and on each new round) drops any request still running.
  - **The question, item by item, typed out:** for a new question `RenderPrompt()` types `storyBeat`, then each briefing row, then the ask, one after another, like someone writing the player instructions.
    - The briefing rows are `QuestionPartsView` rows in `questionParts`: a role chip, the text, and compact icons for the cast members the fact names. `Bind(…, forTyping: true)` builds them empty and exposes them as `Steps`.
    - `TypingSequence` (UI) plays the steps on one scheduled tick, with the timing from `TypewriterSequence` (domain, unit-tested; built on `TypewriterTimeline`). Each part is capped on its own (`QuestionTyping`: 110 cps, 2.5 s max), with 0.25 s between parts.
    - A row not reached yet is hidden (`question-part--pending`). While its text types it has `question-part--typing`, which hides its icons; they fade in once the text is done. Each step scrolls into view as it starts.
    - Without an ask, the stem (`OriginalText`) types alone after the beat.
    - Enter/Space or a click on the question panel finishes the typing at once. A redraw while typing with the same text (50/50, busy) leaves it alone; an answer or a text change shows everything at once.
    - The panel scrolls back to the top only when what it shows changes (new question, debrief/question tab, scenario text).
  - **Layout:** the question panel (`.question-section`, `flex-grow`, min 300 px) takes the room the cards don't need. `cardsRow` has a fixed height of 300 px before and after answering, so a flip never moves it; a face longer than its card scrolls in `cardScroll`.
    - `chapterChip` (chapter and stage, hidden without a chapter) toggles the whole `Text` with its scenario paragraph for the current question (`_showScenario`, reset per question).
  - **Debrief:** once answered, the panel's `panelTabs` (DEBRIEF / QUESTION, or Q) switch it between the question and the debrief (`explanationSection`). An answer opens the debrief (`_showQuestionAfterAnswer`, reset per question); the chapter chip switches to the question. The debrief shows, in order:
    1. `storyOutcome` (the story line)
    2. `resolutionRule`
    3. the misconception of the answer that counts ("Why it was tempting", wrong answers only)
    4. the requirements, with a ✓/✗ for the answer that counts (only with `HasRequirementMatches`)
    5. the explanation
    6. every answer's source feedback

    `RequirementsView` builds the last two lists. Sections without data are hidden.
  - `SearchPanelView` is the search modal (`searchOverlay`): a debounced text field (300 ms), a status line, result rows built in code, and arrow-key selection.
    - Each row has a section chip, a NEW / IN THIS ROUND / CURRENT QUESTION / ANSWERED chip, the `OriginalText` and the answers, never the correct one.
    - The text field is multiline only so long text wraps (it grows to 160 px, then scrolls). A trickle-down `KeyDownEvent` handler keeps Enter and ↑/↓ from the field, so they only play / move the highlighted result. Pasted line breaks become spaces.
    - Its `max-length` (8000) is above `MaxTextLength` on purpose, so whitespace is collapsed before `Normalize` cuts the text.
    - `PickOneScreen` runs the search (cancelled by the next one, by closing and on disable) and plays the chosen result via `ExamSession.PlayNowAsync(id)`. A failure keeps the modal open with a message.
    - While the modal is open, `Update` hands keys to it (Esc, ↑/↓, Enter) and the question shortcuts are off.
  - **Map modal** (`mapOverlay`, `FlowMapView`): the `mapButton` chip (MAP [M], next to the panel tabs in `meta-actions`) opens it while the current question has a flow.
    - Before answering it shows the question's map. After answering it opens on the resolution, and the QUESTION / RESOLUTION tabs (or ←/→) switch between them.
    - While it is open, `Update` hands it the keys (Esc or M close it, ←/→ switch) before any other shortcut, and the question's typing is finished.
    - It closes on a new question, a new round and the results. An answer or a 50/50 that lands while it is open rebinds it (`RefreshMap`). The view keeps its layout while the bind key (question, map, eliminated answers) is the same; a new round calls `FlowMapView.Reset`, since a question there can have the same id and a different map.
    - Laying out takes two passes. `Bind` builds the boxes (`.flow-node`, absolute, fixed width) and edge labels hidden under `map__canvas--measuring`. Once the modal is open and every box and label has a size, it runs `FlowMapModel.Layout`, then sets the boxes' and labels' `left`/`top`, the canvas size and its centring margin. A hidden element gets no layout, so the second pass waits for the modal to open. It polls every 16 ms, at most 30 times; after that, anything still unsized takes a stand-in size so the map still shows.
    - `FlowEdgeLayer` (first child of the canvas) draws the edges with Painter2D: vertical beziers through the layout's points, an arrowhead at the target, added edges thicker and green, inferred and answer edges muted, answer edges dashed (`SetDashPattern`, reset per path). Its colours are the USS custom properties `--flow-edge-*` on `.flow-edges`. Edge labels are absolute `Label`s centred on the layout's label points.
  - **Outcomes on the cards:** once answered, a card with an `AnswerOutcome` shows `outcomeSection` instead of the source feedback (the debrief lists that), via `AnswerCardView.BindResolution`:
    - the `Consequence` and the `Reason`
    - "Meets n of m requirements" (with `HasRequirementMatches`)
    - for a wrong answer, "Missing: …" from its `Gaps`

    The impact chip (WORKS / PARTLY WORKS / FAILS, `card__impact--success|partial|failure`) is in the card's header, not the back, so it shows on both faces once answered; before that it is hidden. The cards show no key hints (no key chip, no `[n]` in the select button), but the keys 1–4 still work. The card also gets `answer-card--impact-*`; a picked partial answer is tinted gold and labelled "Partly works". Without an outcome the card shows the feedback as before. Empty lines are hidden.
  - **Card flip:** a card has two faces in `cardScroll`. `cardFront` holds the answer (and the cast chips show only on the front). `cardBack` holds the feedback or `outcomeSection`.
    - When the question is answered, `SetState` flips every card that has a back. The answer that counts goes first, and each next card waits `FlipStaggerMs` (90 ms). Afterwards a click on the card or its key 1–4 (`Toggle`) flips it again.
    - UI Toolkit has no 3D rotation, so the flip is a USS `scale` transition. `answer-card--flip-out` squashes the card to `scale: 0 1`. After `FlipHalfMs` (140 ms, matching the USS duration) the faces swap and the class comes off. A flip asked for while one runs only changes the side it lands on.
    - A new answer text in `Bind`, or an unanswered state, puts the card back on its front at once. The click that answers doesn't also flip back, because the card notes on `PointerDown` (trickle-down) whether it was already answered.
  - `AnswerCardView` binds one card's text and switches state only through USS classes (`answer-card--interactive|correct|wrong|dimmed|eliminated`). `Bind` doesn't touch state.
  - **Large icons** (`.hero-icon`): `questionIcon` in the question's chip row shows `Question.Icon`, and `cardIcon` in each card's header (`AnswerCardView.BindIcon`) shows `IconForAnswer(i)`. A question or tool without a key shows `DefaultIcon`. USS tints a card's icon by its state class.
  - `EntityStripView` fills a container with cast chips: icon + name, or icon only with `compact`. Type colours come from `entity--actor|object|concept`; `entity--action` stays for actions.
    - `RenderPrompt()` fills `questionEntities` (up to 8) with `SceneCast`, and each card's `cardEntities` (up to 4, in the header) with `ToolComponents(i)`.
    - Runtime panels show no tooltips, so compact icons have no visible name.
    - `EntityIcons` loads `Resources/EntityIcons/<key>` with a cache. `For(member)` falls back to the type; `For(key)` / `Show(element, key)` fall back to `DefaultIcon`. The icon's `background-image` is the other data-driven inline style.
  - **Icon files:** `Assets/Resources/EntityIcons/<key>.svg`, one per key of the backend's catalog. Today they are Lucide glyphs (ISC, `LICENSE.txt` there, v1.69.0) with the stroke in white so the importer has a colour and USS tints them. Don't add them by hand: add the key to the backend's `icon_catalog.py`, run its `scripts/export_game_icons.py` (writes the SVGs here) and `UPDATE_CONTRACT=1` for `tests/test_contract.py` (writes `Contract/icon_catalog.json`). `Assets/Editor/EntityIconImport.cs` imports them as `VectorImage`, and `EntityIconResolverTests` checks every catalog key loads. A key the game doesn't ship only falls back, so the backend can add keys before the game does. Changing the icon set means replacing these files under the same names.
  - `GradientElement` is a `[UxmlElement]` that draws gradients with vertex colors, because USS has no gradients.
  - Lives (`hearts`) follow `ExamSession.Lives`.
  - Header nav, coins, timer, streak and the other two power-ups (Freeze, Codex Lore) are **static placeholders**; they are not wired up yet.
- **Scene setup**: the menu **Tools ▸ ExamCards ▸ Setup PickOne Scene** (`Assets/Editor/PickOneSceneSetup.cs`) creates `Assets/Resources/ExamApiSettings.asset` if missing and `Assets/UI/PanelSettings.asset` (uses theme `Assets/UI/ExamTheme.tss`, scale-with-screen 1280×1024) and wires the `PickOneUI` GameObject into `SampleScene`. Re-run it after changing serialized references instead of editing the scene YAML.

## UI Toolkit conventions in this project

- USS has no `box-shadow`, `gap`, or gradients. The neo-brutal hard shadows are done two ways:
  - **1–2 px offsets:** thicker right/bottom borders.
  - **3–4 px offsets:** a `.brutal.brutal--s3|s4` wrapper whose first child is an absolute `.brutal__shadow`, translated under the real `.box`.
- `name` attributes are camelCase; classes are BEM-style kebab-case. Avoid inline `style` in UXML; the only inline styles set from C# are data-driven values (progress width, an entity's icon, a map box's or edge label's position and the map canvas's size and margin).
- Labels showing exam data set `enable-rich-text="false"`, because the text can contain literal tags such as `<example>`.
- Buttons are `focusable="false"`, so Enter and Space only act through `PickOneScreen`'s key handling and don't also trigger a focused button.
- SVG icons in `Icons/` import as `VectorImage` and are referenced from USS via `url("project://database/...")`.
- Fonts: the design uses Outfit and Nunito Sans, but the project currently uses Unity's default font.

## Working with the Editor

- A running Editor can be driven with the Unity CLI: `unity status`, `unity recompile`, `unity command eval '<C#>'`, `unity command capture game_view -- --source screen --save_path ...` (the path is relative to `Assets/`, so delete what you capture), `unity command editor.playmode start|stop`, `unity command console get -- --tail 20 --level warn`, and `unity command editor invoke_menu --path "Tools/ExamCards/Setup PickOne Scene"`.
- Tests: `unity command test run --mode EditMode --filter ExamCards`, or Window ▸ General ▸ Test Runner. The EditMode tests are in `Assets/Tests/EditMode/` (asmdef `ExamCards.Exam.Tests`). `ContractTests` parse real backend responses from `Assets/Tests/EditMode/Contract/*.json`; those files are written by the backend's `tests/test_contract.py` (`UPDATE_CONTRACT=1`), which also fails when they drift, so don't edit them by hand.
- An EditMode test runs on the main thread: blocking on a task (`GetAwaiter().GetResult()`) that awaits a delay deadlocks the Editor, so use `new MockQuestionService(latencyMs: 0)` there.
- The active input handler is the Input System only (`Keyboard.current`), not legacy `Input`.

## Conventions for editing

- Every file and folder under `Assets/` has a paired `.meta` file holding its GUID. Never delete or recreate a file without its `.meta`. Move or rename files so the `.meta` goes with them, ideally inside the Editor.
- `Library/`, `Temp/`, `obj/`, `Logs/`, and `UserSettings/` are generated and should not be touched or committed.
