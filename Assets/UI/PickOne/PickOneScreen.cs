using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Drives the PickOne screen: starts a round from the question service, shows its questions one by one,
    /// and handles card picks (click or keys 1-4), Reveal (click or R), 50/50 (click or F), Next, Play Again
    /// and Retry (click, Enter or Space) and the final score. Answers and 50/50 are checked by the round,
    /// so they run asynchronously; a failed request leaves the question as it was and shows a message.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PickOneScreen : MonoBehaviour
    {
        const string HiddenClass = "is-hidden";
        const string ResultsHiddenClass = "results--hidden";
        const string LoadingText = "Loading question…";
        const string LoadFailedText = "Could not load questions.";
        const string NoQuestionsText = "No questions are available.";
        const string AnswerFailedText = "Could not check that answer. Pick again to retry.";
        const string FiftyFiftyFailedText = "50/50 is unavailable right now. Try again.";

        [SerializeField] VisualTreeAsset _answerCardTemplate;

        // One key per answer, up to Question.MaxAnswers.
        static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };
        static readonly Key[] NextKeys = { Key.Enter, Key.NumpadEnter, Key.Space };

        readonly List<AnswerCardView> _cards = new();
        IQuestionService _questionService;
        ExamSession _session;

        // Cancels the round load and any answer or 50/50 request; replaced for every new round.
        CancellationTokenSource _roundCancellation;
        bool _loadFailed;

        Label _categoryLabel;
        VisualElement _simplifiedBadge;
        ScrollView _questionScroll;
        Label _questionText;
        VisualElement _explanationSection;
        Label _explanationText;
        Label _progressLabel;
        Label _progressPercent;
        VisualElement _progressFill;
        VisualElement _cardsRow;
        VisualElement _fiftyFiftyButton;
        Label _fiftyFiftyCount;
        Label _statusText;
        Button _revealButton;
        Button _nextButton;
        Button _retryButton;
        VisualElement _resultsOverlay;
        Label _resultsScore;
        Button _playAgainButton;
        VisualElement _exitButton;
        List<VisualElement> _hearts;

        static void OnExitClicked(ClickEvent evt)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _categoryLabel = root.Q<Label>("categoryLabel");
            _simplifiedBadge = root.Q("simplifiedBadge");
            _questionScroll = root.Q<ScrollView>("questionScroll");
            _questionText = root.Q<Label>("questionText");
            _explanationSection = root.Q("explanationSection");
            _explanationText = root.Q<Label>("explanationText");
            _progressLabel = root.Q<Label>("progressLabel");
            _progressPercent = root.Q<Label>("progressPercent");
            _progressFill = root.Q("progressFill");
            _cardsRow = root.Q("cardsRow");
            _fiftyFiftyButton = root.Q("fiftyFiftyButton");
            _fiftyFiftyCount = root.Q<Label>("fiftyFiftyCount");
            _statusText = root.Q<Label>("statusText");
            _revealButton = root.Q<Button>("revealButton");
            _nextButton = root.Q<Button>("nextButton");
            _retryButton = root.Q<Button>("retryButton");
            _resultsOverlay = root.Q("resultsOverlay");
            _resultsScore = root.Q<Label>("resultsScore");
            _playAgainButton = root.Q<Button>("playAgainButton");

            _exitButton = root.Q("exitButton");
            _hearts = root.Q("hearts").Children().ToList();

            _exitButton.RegisterCallback<ClickEvent>(OnExitClicked);
            _fiftyFiftyButton.RegisterCallback<ClickEvent>(OnFiftyFiftyClicked);
            _revealButton.clicked += OnRevealPressed;
            _nextButton.clicked += OnNextPressed;
            _retryButton.clicked += LoadRound;
            _playAgainButton.clicked += LoadRound;
            _cardsRow.Clear();
            _cards.Clear();

            LoadRound();
        }

        void OnDisable()
        {
            CancelRound();
            _session = null;
            _exitButton?.UnregisterCallback<ClickEvent>(OnExitClicked);
            _fiftyFiftyButton?.UnregisterCallback<ClickEvent>(OnFiftyFiftyClicked);
            if (_revealButton != null)
                _revealButton.clicked -= OnRevealPressed;
            if (_nextButton != null)
                _nextButton.clicked -= OnNextPressed;
            if (_retryButton != null)
            {
                _retryButton.clicked -= LoadRound;
                _playAgainButton.clicked -= LoadRound;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (_session == null)
            {
                if (_loadFailed && WasNextKeyPressed(keyboard))
                    LoadRound();
                return;
            }

            if (_session.IsFinished)
            {
                if (WasNextKeyPressed(keyboard))
                    LoadRound();
                return;
            }

            if (keyboard[Key.R].wasPressedThisFrame)
            {
                OnRevealPressed();
                return;
            }

            if (keyboard[Key.F].wasPressedThisFrame)
            {
                OnFiftyFiftyPressed();
                return;
            }

            if (WasNextKeyPressed(keyboard))
            {
                OnNextPressed();
                return;
            }

            for (var i = 0; i < DigitKeys.Length && i < _cards.Count; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                {
                    OnCardSelected(i);
                    return;
                }
            }
        }

        static bool WasNextKeyPressed(Keyboard keyboard)
        {
            foreach (var key in NextKeys)
            {
                if (keyboard[key].wasPressedThisFrame)
                    return true;
            }
            return false;
        }

        void CancelRound()
        {
            _roundCancellation?.Cancel();
            _roundCancellation?.Dispose();
            _roundCancellation = null;
        }

        /// <summary>Starts a new round. A round's answers are recorded where it is held, so it can't be replayed.</summary>
        async void LoadRound()
        {
            _session = null;
            _loadFailed = false;
            _resultsOverlay.AddToClassList(ResultsHiddenClass);
            _retryButton.AddToClassList(HiddenClass);
            _nextButton.AddToClassList(HiddenClass);
            _explanationSection.AddToClassList(HiddenClass);
            _fiftyFiftyButton.SetEnabled(false);
            _revealButton.SetEnabled(false);
            _cardsRow.Clear();
            _cards.Clear();
            ShowStatus(null);
            _questionText.text = LoadingText;

            CancelRound();
            _roundCancellation = new CancellationTokenSource();
            var token = _roundCancellation.Token;

            IExamRound round;
            try
            {
                // Created here rather than in OnEnable, so bad settings end in the Retry message too.
                _questionService ??= QuestionServices.Create();
                round = await _questionService.StartRoundAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogException(e, this);
                ShowLoadFailure(LoadFailedText);
                return;
            }

            if (token.IsCancellationRequested)
                return;

            if (round.Questions.Count == 0)
            {
                Debug.LogWarning("The question service returned no questions.", this);
                ShowLoadFailure(NoQuestionsText);
                return;
            }

            _session = new ExamSession(round);
            ShowCurrentQuestion();
        }

        void ShowLoadFailure(string message)
        {
            _loadFailed = true;
            _questionText.text = message;
            _retryButton.RemoveFromClassList(HiddenClass);
        }

        void ShowStatus(string message)
        {
            _statusText.text = message ?? string.Empty;
            _statusText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(message));
        }

        void ShowCurrentQuestion()
        {
            var question = _session.Current;
            _categoryLabel.text = question.Section?.ToUpperInvariant();
            _progressLabel.text = $"Question {_session.CurrentIndex + 1} of {_session.Total}";

            ShowStatus(null);
            EnsureCardCount(question.Original.Answers.Count);
            RenderPrompt();
        }

        /// <summary>Shows the current text and state. Used for a new question, an answer, a 50/50 and a reveal.</summary>
        void RenderPrompt()
        {
            var question = _session.Current;
            var prompt = _session.CurrentPrompt;
            var review = _session.CurrentPromptReview;
            var answered = _session.IsCurrentAnswered;

            var percent = Mathf.RoundToInt(100f * _session.AnsweredCount / _session.Total);
            _progressPercent.text = $"{percent}%";
            _progressFill.style.width = Length.Percent(percent);

            _questionText.text = prompt.Question;
            _explanationText.text = review?.Explanation;
            _explanationSection.EnableInClassList(HiddenClass, !answered);
            _simplifiedBadge.EnableInClassList(HiddenClass, !_session.IsCurrentRevealed);

            for (var i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(prompt.Answers[i], review?.Feedbacks[i]);
                _cards[i].SetState(GetCardState(i));
            }

            for (var i = 0; i < _hearts.Count; i++)
                _hearts[i].EnableInClassList("icon--heart-used", i >= _session.Lives);

            _fiftyFiftyCount.text = _session.FiftyFiftyRemaining.ToString();
            _fiftyFiftyButton.SetEnabled(_session.CanEliminate);
            _revealButton.SetEnabled(question.CanReveal && !_session.IsCurrentRevealed);
            _revealButton.text = _session.IsCurrentRevealed ? "Simplified Shown" : "Reveal Simplified [R]";

            var isLast = _session.CurrentIndex + 1 >= _session.Total || _session.Lives <= 0;
            _nextButton.text = isLast ? "See Results" : "Next Question";
            _nextButton.EnableInClassList(HiddenClass, !answered);

            ScrollQuestionPanel(answered);
        }

        AnswerCardState GetCardState(int index)
        {
            if (!_session.IsCurrentAnswered)
                return _session.IsEliminated(index) ? AnswerCardState.Eliminated : AnswerCardState.Interactive;
            if (index == _session.CurrentReview.CorrectIndex)
                return AnswerCardState.Correct;
            return index == _session.AnsweredIndex ? AnswerCardState.Wrong : AnswerCardState.Dimmed;
        }

        void ScrollQuestionPanel(bool toExplanation)
        {
            // Runs on the next panel update, because the new text has to be laid out first.
            _questionScroll.schedule.Execute(() =>
            {
                if (toExplanation)
                    _questionScroll.ScrollTo(_explanationSection);
                else
                    _questionScroll.scrollOffset = Vector2.zero;
            });
        }

        void EnsureCardCount(int count)
        {
            while (_cards.Count > count)
            {
                var last = _cards[_cards.Count - 1];
                last.Selected -= OnCardSelected;
                last.Root.RemoveFromHierarchy();
                _cards.RemoveAt(_cards.Count - 1);
            }

            while (_cards.Count < count)
            {
                var instance = _answerCardTemplate.Instantiate();
                instance.AddToClassList("answer-card-slot");
                _cardsRow.Add(instance);

                var view = new AnswerCardView(instance, _cards.Count);
                view.Selected += OnCardSelected;
                _cards.Add(view);
            }
        }

        void OnCardSelected(int index)
        {
            var session = _session;
            if (session == null || session.IsBusy || session.IsFinished || session.IsCurrentAnswered
                || session.IsEliminated(index))
                return;

            RunRoundRequest(session, token => session.AnswerAsync(index, token), AnswerFailedText);
        }

        void OnFiftyFiftyClicked(ClickEvent evt) => OnFiftyFiftyPressed();

        void OnFiftyFiftyPressed()
        {
            var session = _session;
            if (session == null || !session.CanEliminate)
                return;

            RunRoundRequest(session, token => session.EliminateAsync(token), FiftyFiftyFailedText);
        }

        /// <summary>
        /// Runs an answer or 50/50 request and redraws when it ends. Results for a round that has since been
        /// replaced are dropped; a failure keeps the question as it was and shows <paramref name="failureText"/>.
        /// </summary>
        async void RunRoundRequest(ExamSession session, Func<CancellationToken, Task> request, string failureText)
        {
            var token = _roundCancellation.Token;
            ShowStatus(null);

            Task pending;
            try
            {
                pending = request(token);
                RenderPrompt(); // The session is busy now, so 50/50 shows as unavailable.
                await pending;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested || session != _session)
                    return;
                Debug.LogException(e, this);
                ShowStatus(failureText);
                RenderPrompt();
                return;
            }

            if (session == _session)
                RenderPrompt();
        }

        void OnRevealPressed()
        {
            if (_session == null)
                return;
            if (_session.Reveal())
                RenderPrompt();
        }

        void OnNextPressed()
        {
            if (_session == null || _session.IsFinished || !_session.IsCurrentAnswered)
                return;

            if (_session.MoveNext())
                ShowCurrentQuestion();
            else
                ShowResults();
        }

        void ShowResults()
        {
            var outOfLives = _session.Lives <= 0;
            _resultsScore.text = $"{(outOfLives ? "Out of lives! " : "")}You scored {_session.Score} / {_session.Total}";
            _resultsOverlay.RemoveFromClassList(ResultsHiddenClass);
        }
    }
}
