using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ExamCards.Exam;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>Where a search result stands in the running exam; shown as a chip on its row.</summary>
    enum SearchResultState
    {
        /// <summary>Not in the exam yet; choosing it adds it to the round.</summary>
        New,
        InRound,
        Current,
        Answered,
    }

    /// <summary>
    /// The search modal: a text field, a status line and the results. It debounces typing and reports the text
    /// through <see cref="TextChanged"/>; <see cref="PickOneScreen"/> runs the search and decides what a
    /// chosen result does. Results show the question and its answers but never which answer is right.
    /// </summary>
    sealed class SearchPanelView
    {
        const string HiddenClass = "search--hidden";
        const string SelectedClass = "search-result--selected";
        const long DebounceMs = 300;
        const int MaxQuestionPreview = 260;
        const int MaxAnswerPreview = 80;

        static readonly Regex Whitespace = new Regex(@"\s+");
        static readonly char[] LineBreaks = { '\n', '\r' };

        readonly VisualElement _overlay;
        readonly VisualElement _closeButton;
        readonly TextField _input;
        readonly Label _status;
        readonly ScrollView _results;
        readonly List<(Question Question, VisualElement Row)> _rows = new();

        IVisualElementScheduledItem _pendingChange;
        string _lastText;
        int _selected = -1;

        public SearchPanelView(VisualElement root)
        {
            _overlay = root.Q("searchOverlay");
            _closeButton = root.Q("searchCloseButton");
            _input = root.Q<TextField>("searchInput");
            _status = root.Q<Label>("searchStatus");
            _results = root.Q<ScrollView>("searchResults");

            _overlay.RegisterCallback<ClickEvent>(OnOverlayClicked);
            _closeButton.RegisterCallback<ClickEvent>(OnCloseClicked);
            _input.RegisterValueChangedCallback(OnInputChanged);
            _input.RegisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
        }

        /// <summary>The trimmed search text after typing pauses, or null when it is too short to search.</summary>
        public event Action<string> TextChanged;

        public event Action<Question> Chosen;
        public event Action CloseRequested;

        public bool IsOpen => !_overlay.ClassListContains(HiddenClass);

        public void Dispose()
        {
            _pendingChange?.Pause();
            _overlay.UnregisterCallback<ClickEvent>(OnOverlayClicked);
            _closeButton.UnregisterCallback<ClickEvent>(OnCloseClicked);
            _input.UnregisterValueChangedCallback(OnInputChanged);
            _input.UnregisterCallback<KeyDownEvent>(OnInputKeyDown, TrickleDown.TrickleDown);
        }

        /// <summary>Shows the modal with the previous text and results, and focuses the text field.</summary>
        public void Open()
        {
            _overlay.RemoveFromClassList(HiddenClass);
            // Next frame, so the key that opened the modal isn't typed into the field.
            _input.schedule.Execute(() =>
            {
                _input.Focus();
                _input.SelectAll();
            });
        }

        public void Close()
        {
            _pendingChange?.Pause();
            _input.Blur();
            _overlay.AddToClassList(HiddenClass);
        }

        /// <summary>Forgets the text and results, for a new round.</summary>
        public void Reset(string message)
        {
            _pendingChange?.Pause();
            _lastText = null;
            _input.SetValueWithoutNotify(string.Empty);
            ShowResults(Array.Empty<Question>(), _ => SearchResultState.New);
            ShowMessage(message);
        }

        public void ShowMessage(string message) => _status.text = message ?? string.Empty;

        public void ShowResults(IReadOnlyList<Question> results, Func<Question, SearchResultState> stateOf)
        {
            _results.Clear();
            _rows.Clear();
            _selected = -1;
            foreach (var question in results)
            {
                var row = MakeRow(question, stateOf(question), _rows.Count);
                _results.Add(row);
                _rows.Add((question, row));
            }
            Select(0);
            _results.scrollOffset = UnityEngine.Vector2.zero;
        }

        /// <summary>Moves the highlighted result up (-1) or down (+1).</summary>
        public void MoveSelection(int delta)
        {
            if (_rows.Count > 0)
                Select(Math.Clamp(_selected + delta, 0, _rows.Count - 1));
        }

        /// <summary>Reports the highlighted result through <see cref="Chosen"/>. False if there is none.</summary>
        public bool ChooseSelected()
        {
            if (_selected < 0 || _selected >= _rows.Count)
                return false;
            Chosen?.Invoke(_rows[_selected].Question);
            return true;
        }

        void Select(int index)
        {
            if (_selected >= 0 && _selected < _rows.Count)
                _rows[_selected].Row.RemoveFromClassList(SelectedClass);
            _selected = index < _rows.Count ? index : -1;
            if (_selected < 0)
                return;

            var row = _rows[_selected].Row;
            row.AddToClassList(SelectedClass);
            // After layout, so a row that was just added can be scrolled to.
            _results.schedule.Execute(() => _results.ScrollTo(row));
        }

        /// <summary>
        /// The field is multiline only so long text wraps, so it must not act on the keys PickOneScreen.HandleSearchKeys
        /// uses: Enter (which would replace the selected text with a line break) plays the highlighted result, and
        /// ↑/↓ (which would move the cursor between wrapped lines) move the highlight.
        /// </summary>
        static void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is UnityEngine.KeyCode.Return or UnityEngine.KeyCode.KeypadEnter
                    or UnityEngine.KeyCode.UpArrow or UnityEngine.KeyCode.DownArrow
                || evt.character is '\n' or '\r')
                evt.StopImmediatePropagation();
        }

        void OnInputChanged(ChangeEvent<string> evt)
        {
            // Pasted line breaks become spaces (Enter itself never reaches the field, see OnInputKeyDown).
            var value = _input.value ?? string.Empty;
            if (value.IndexOfAny(LineBreaks) >= 0)
            {
                // Same length, so the cursor stays where it was.
                var cursor = _input.cursorIndex;
                _input.SetValueWithoutNotify(value.Replace('\r', ' ').Replace('\n', ' '));
                _input.SelectRange(cursor, cursor);
            }

            _pendingChange?.Pause();
            _pendingChange = _input.schedule.Execute(() =>
            {
                var text = QuestionSearchText.Normalize(_input.value);
                if (text == _lastText)
                    return;
                _lastText = text;
                TextChanged?.Invoke(text);
            }).StartingIn(DebounceMs);
        }

        void OnOverlayClicked(ClickEvent evt)
        {
            // Only a click on the dimmed backdrop itself, not on the panel.
            if (evt.target == _overlay)
                CloseRequested?.Invoke();
        }

        void OnCloseClicked(ClickEvent evt) => CloseRequested?.Invoke();

        VisualElement MakeRow(Question question, SearchResultState state, int index)
        {
            var row = new VisualElement();
            row.AddToClassList("search-result");
            row.EnableInClassList("search-result--answered", state == SearchResultState.Answered);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                Select(index);
                ChooseSelected();
            });

            var meta = new VisualElement();
            meta.AddToClassList("search-result__meta");
            if (!string.IsNullOrEmpty(question.Section))
                meta.Add(MakeChip(question.Section.ToUpperInvariant(), "chip--mint"));
            meta.Add(state switch
            {
                SearchResultState.Current => MakeChip("CURRENT QUESTION", "chip--sand"),
                SearchResultState.InRound => MakeChip("IN THIS ROUND", "chip--peach-2"),
                SearchResultState.Answered => MakeChip("ANSWERED", "chip--dark"),
                _ => MakeChip("NEW", "chip--gold"),
            });
            row.Add(meta);

            row.Add(MakeLabel(Shorten(question.OriginalText, MaxQuestionPreview), "search-result__question"));

            var answers = new VisualElement();
            answers.AddToClassList("search-result__answers");
            for (var i = 0; i < question.Answers.Count; i++)
            {
                var letter = (char)('A' + i);
                answers.Add(MakeLabel($"{letter}  {Shorten(question.Answers[i], MaxAnswerPreview)}",
                    "search-result__answer"));
            }
            row.Add(answers);
            return row;
        }

        static VisualElement MakeChip(string text, string modifier)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("chip");
            chip.AddToClassList(modifier);
            chip.Add(MakeLabel(text, "chip__text"));
            return chip;
        }

        static Label MakeLabel(string text, string className)
        {
            // Exam text can contain literal tags such as <example>.
            var label = new Label(text) { enableRichText = false, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        static string Shorten(string text, int maxLength)
        {
            text = Whitespace.Replace(text ?? string.Empty, " ").Trim();
            return text.Length <= maxLength ? text : text.Substring(0, maxLength).TrimEnd() + "…";
        }
    }
}
