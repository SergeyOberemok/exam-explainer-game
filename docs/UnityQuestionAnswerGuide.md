# Learning Unity through ExamCards: how a question and its answers are presented

A guided reference to the approaches used in this project. Each section gives a short explanation, the file to open, and the Unity concept to look up.

## 1. Big picture

```
IQuestionService ──► Question[] ──► ExamSession ──► PickOneScreen ──► AnswerCardView ×N
 (data source)       (data)        (game logic)     (controller)       (one card each)
                                                         │
                                       PickOne.uxml + PickOne.uss  (what it looks like)
```

Three layers, each with one job:

| Layer | Job | Files |
|---|---|---|
| **Domain** | What a question is, whose turn it is, the score. No Unity code. | `Assets/Scripts/Exam/` |
| **UI markup/style** | Structure (UXML) and look (USS). | `Assets/UI/PickOne/*.uxml`, `*.uss` |
| **UI code** | Fills the markup with data, reacts to clicks. | `PickOneScreen.cs`, `AnswerCardView.cs` |

Why split it? The domain layer can be unit-tested without starting Unity, and the data source can be swapped (mock → real API) without touching the UI.

## 2. Data models: `Question` and `AnswerOption`

- Files: `Assets/Scripts/Exam/Question.cs`, `AnswerOption.cs`
- A `Question` has a prompt, a hint, a clue image, a list of `Options`, and `CorrectIndex` (the position of the right answer in the list).
- An `AnswerOption` is one card: title, subtitle, description, tags, traits, tier, image.
- They are plain C# classes with properties (`{ get; set; }`), shaped so a JSON library can later fill them from an API response.
- Images are just strings: a **key** for the mock (`"sea_otter"`) or a **URL** for a real server. The UI decides how to resolve them.

Concepts: plain C# classes (POCOs), `[Serializable]`, `IReadOnlyList<T>`.

## 3. Question source: `IQuestionService`

- Files: `IQuestionService.cs`, `MockQuestionService.cs`
- An **interface** is a contract: "something that can give me questions asynchronously". `MockQuestionService` fulfils it with hardcoded data and a fake `Task.Delay(300)` to imitate network latency.
- `PickOneScreen.OnEnable` does `_questionService ??= new MockQuestionService();`. That single line is the only place to change when a real API service exists.
- `CancellationToken` lets the screen abort a pending load if it is disabled/destroyed.

Concepts: interfaces, dependency inversion, `async/await`, `Task<T>`, `CancellationToken`.

## 4. Game logic: `ExamSession`

- File: `ExamSession.cs`
- Holds the question list, `CurrentIndex`, `Score`.
- `Answer(index)` checks the pick against `CorrectIndex` and returns an `AnswerResult` (was it right + which index was right). It refuses a second answer to the same question.
- `MoveNext()` goes to the next question and returns `false` when there are none left.
- Because it has no Unity references (`noEngineReferences: true` in `ExamCards.Exam.asmdef`), tests in `Assets/Tests/EditMode/ExamSessionTests.cs` run instantly.

Concepts: assembly definitions (`.asmdef`), EditMode tests (Test Runner), `readonly struct`.

## 5. UI Toolkit basics (used instead of uGUI)

Unity has two runtime UI systems. This project uses **UI Toolkit**, which works like web development:

| Web | UI Toolkit | In this project |
|---|---|---|
| HTML | **UXML** | `PickOne.uxml`, `AnswerCard.uxml` |
| CSS | **USS** | `PickOne.uss` |
| `document.querySelector` | `root.Q<T>("name")` | `PickOneScreen.OnEnable` |
| classList | `AddToClassList` / `EnableInClassList` | `AnswerCardView.SetState` |
| flexbox | flexbox (Yoga) | `.cards-row`, `.card__header` … |

Key pieces:

- **`UIDocument`** component: a GameObject component that points to a UXML file and shows it on screen. Its `rootVisualElement` is the tree root.
- **`PanelSettings`** asset (`Assets/UI/PanelSettings.asset`): scaling rules (here scale-with-screen, reference 1280×1024) and the theme.
- **`VisualElement`**: the "div" of UI Toolkit. `Label`, `Button` etc. derive from it.
- `name="..."` attributes are for finding elements from code; `class="..."` is for styling.

Concepts: `UIDocument`, `VisualElement`, `Q<T>()`, USS selectors and variables (`:root { --ink: ... }`, `var(--ink)`).

## 6. Showing the question

- Files: `PickOne.uxml` (the `question-section`), `PickOneScreen.ShowCurrentQuestion`
- The UXML has empty placeholder labels (`questionText`, `hintLabel`, `categoryLabel`, `xpLabel`…).
- On `OnEnable` the controller looks them up once and caches them in fields.
- For each question, `ShowCurrentQuestion()` just assigns `.text`. The progress bar is data-driven too: `_progressFill.style.width = Length.Percent(percent)`.
- Images are set from code with `style.backgroundImage = new StyleBackground(texture)`. Only values that come from data are styled inline; everything else lives in USS.

Concepts: `Label.text`, inline `style`, `StyleBackground`, `Length.Percent`.

## 7. Showing the answers: the card template

- Files: `AnswerCard.uxml`, `AnswerCardView.cs`, `PickOneScreen.EnsureCardCount`
- `AnswerCard.uxml` is a **template** for one card. It is a `VisualTreeAsset` assigned in the Inspector to `_answerCardTemplate`.
- The number of cards is not hardcoded. `EnsureCardCount(n)` calls `_answerCardTemplate.Instantiate()` and adds the result to `cardsRow` until there are `n` cards (or removes extras). So a question with 3 or 4 options just works.
- Each instance is wrapped by an **`AnswerCardView`**, a small plain-C# class (not a MonoBehaviour) that:
  - caches its own child elements,
  - `Bind(option, image)` writes an `AnswerOption` into the labels, image and trait chips,
  - raises a `Selected(index)` **event** when its button is clicked.
- The controller subscribes (`view.Selected += OnCardSelected`) and never touches the card's internals. This is the "view" pattern: the screen owns the flow, the card owns its own widgets.
- Traits are created dynamically: `new VisualElement()` + `new Label(...)` added into `traitsRow`.

Concepts: `VisualTreeAsset.Instantiate()`, templates, C# `event`/`Action<int>`, view classes, `Clear()` / `Add()`.

## 8. Input: clicks and keys 1–4

- Click: `Button.clicked` in `AnswerCardView` → `Selected` event → `PickOneScreen.OnCardSelected(index)`.
- Keyboard: `PickOneScreen.Update()` polls `Keyboard.current[Key.Digit1].wasPressedThisFrame` (Input System package; legacy `Input` is disabled in this project).
- A bool `_acceptingInput` guards both paths so you cannot answer twice while feedback is playing.

Concepts: `MonoBehaviour.Update`, Input System `Keyboard.current`, input guarding.

## 9. Feedback: states through USS classes

- Files: `AnswerCardView.SetState`, `PickOne.uss` (`.answer-card--*` rules)
- After an answer, the controller asks the session for the result and sets each card to one of: `Correct`, `Wrong`, `Dimmed` (and `Interactive` when a new question loads).
- `SetState` doesn't change colors itself. It toggles a class (`answer-card--correct`…) with `EnableInClassList`, and USS rules such as `.answer-card--correct > .card__box { background-color: var(--mint-soft); }` do the visuals.
- Hover only applies to `.answer-card--interactive`, so answered cards stop reacting to the mouse.

Why: **C# says *what state*, USS says *how it looks*.** Designers can restyle without touching code.

Concepts: BEM class naming (`block__element--modifier`), `EnableInClassList`, USS descendant (` `) and child (`>`) selectors, pseudo-class `:hover`.

## 10. Flow and timing: coroutine

- File: `PickOneScreen.AdvanceAfterFeedback`
- After feedback, a **coroutine** waits `_feedbackSeconds` (`yield return new WaitForSeconds(...)`) and then calls `MoveNext()` → next question, or the results overlay.
- `OnDisable` calls `StopAllCoroutines()` and cancels the pending load so nothing runs on a dead screen.
- Loading uses `async void LoadQuestions()` with try/catch for cancel and error. `async void` is acceptable here because it is an event-style entry point.

Concepts: coroutines vs `async/await`, `OnEnable`/`OnDisable` lifecycle, cleaning up event subscriptions (`-=`).

## 11. Results overlay

- `resultsOverlay` is part of `PickOne.uxml` and hidden by the class `results--hidden` (USS: hides it).
- `ShowResults()` removes the class; `StartExam()` (the "Play Again" button) adds it back and starts a fresh `ExamSession`.

Concepts: toggling visibility with classes instead of enabling/disabling objects.

## 12. Styling tricks you will see (USS limits)

USS has no `box-shadow`, `gap` or gradients, so the project works around it:

- **Hard "neo-brutal" shadow**: wrap the element in `.brutal`; its first child `.brutal__shadow` is absolutely positioned and `translate`d 3–4 px under the real `.box`. See `PickOne.uss` lines ~33–53.
- **Gradients**: `GradientElement.cs`, a custom control marked `[UxmlElement]` that overrides `generateVisualContent` and builds a mesh with per-vertex colors. Used in UXML as `<ec:GradientElement start-color="…" />`.
- **Spacing**: margins instead of `gap` (`.answer-card-slot { margin: 0 10px }`).
- **Icons**: SVG files imported as `VectorImage`, referenced with `url("project://database/Assets/...")`.

Concepts: `generateVisualContent`, `MeshGenerationContext`, `[UxmlElement]` / `[UxmlAttribute]`, custom UXML namespaces (`xmlns:ec="ExamCards.UI"`).

## 13. Editor automation: the setup menu

- File: `Assets/Editor/PickOneSceneSetup.cs`, menu **Tools ▸ ExamCards ▸ Setup PickOne Scene**
- An editor script (in an `Editor` folder, so it is not in builds) that creates `PanelSettings`, adds `UIDocument` + `PickOneScreen` to a GameObject, and fills the serialized fields (`_answerCardTemplate`, `_images`) via `SerializedObject`.
- Edit the scene through this tool, not the scene YAML.

Concepts: `[MenuItem]`, `AssetDatabase`, `SerializedObject` / `SerializedProperty`, `EditorSceneManager`.

## 14. Suggested reading order (30 minutes)

1. `Question.cs` and `AnswerOption.cs`: the data.
2. `ExamSession.cs`: the rules.
3. `AnswerCard.uxml`: structure of one card.
4. `AnswerCardView.cs`: binding and state classes.
5. `PickOneScreen.cs`: top to bottom (`OnEnable` → `LoadQuestions` → `ShowCurrentQuestion` → `OnCardSelected` → `AdvanceAfterFeedback`).
6. `PickOne.uss`: search for `answer-card--` and `.brutal`.
7. `GradientElement.cs`: custom drawing.

## 15. Official docs to bookmark

- UI Toolkit overview: <https://docs.unity3d.com/Manual/UIElements.html>
- UXML: <https://docs.unity3d.com/Manual/UIE-UXML.html>
- USS and selectors: <https://docs.unity3d.com/Manual/UIE-USS.html>
- UIDocument component: <https://docs.unity3d.com/Manual/UIE-create-ui-document.html>
- Custom controls (`[UxmlElement]`): <https://docs.unity3d.com/Manual/UIE-create-custom-controls.html>
- Generate visual content (meshes): <https://docs.unity3d.com/Manual/UIE-generate-visual-content.html>
- Coroutines: <https://docs.unity3d.com/Manual/Coroutines.html>
- Input System: <https://docs.unity3d.com/Packages/com.unity.inputsystem@latest>
- Assembly definitions: <https://docs.unity3d.com/Manual/assembly-definition-files.html>
- Test Framework: <https://docs.unity3d.com/Packages/com.unity.test-framework@latest>
- Editor scripting (`MenuItem`, `SerializedObject`): <https://docs.unity3d.com/Manual/editor-CustomEditors.html>

(Links point to Unity's documentation; section paths can shift between versions, so use the search box if one moves.)
