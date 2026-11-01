using System;
using System.Collections.Generic;
using System.Threading;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Drives the PickOne screen: loads questions from the question service, shows them one by one,
    /// and handles card picks (click or keys 1-4), Reveal (click or R), Next (click, Enter or Space)
    /// and the final score.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PickOneScreen : MonoBehaviour
    {
        const string HiddenClass = "is-hidden";
        const string ResultsHiddenClass = "results--hidden";
        const int NoAnswer = -1;

        [SerializeField] VisualTreeAsset _answerCardTemplate;

        static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };
        static readonly Key[] NextKeys = { Key.Enter, Key.NumpadEnter, Key.Space };

        readonly List<AnswerCardView> _cards = new();
        IQuestionService _questionService;
        IReadOnlyList<Question> _questions;
        ExamSession _session;
        CancellationTokenSource _loadCancellation;
        int _answeredIndex = NoAnswer;
        bool _examFinished;

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
        Button _revealButton;
        Button _nextButton;
        VisualElement _resultsOverlay;
        Label _resultsScore;
        Button _playAgainButton;

        void OnEnable()
        {
            _questionService ??= QuestionServices.Create();

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
            _revealButton = root.Q<Button>("revealButton");
            _nextButton = root.Q<Button>("nextButton");
            _resultsOverlay = root.Q("resultsOverlay");
            _resultsScore = root.Q<Label>("resultsScore");
            _playAgainButton = root.Q<Button>("playAgainButton");

            _revealButton.clicked += OnRevealPressed;
            _nextButton.clicked += OnNextPressed;
            _playAgainButton.clicked += StartExam;
            _revealButton.SetEnabled(false);
            _cardsRow.Clear();
            _cards.Clear();

            LoadQuestions();
        }

        void OnDisable()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _session = null;
            if (_revealButton != null)
                _revealButton.clicked -= OnRevealPressed;
            if (_nextButton != null)
                _nextButton.clicked -= OnNextPressed;
            if (_playAgainButton != null)
                _playAgainButton.clicked -= StartExam;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (_session == null || _examFinished || keyboard == null)
                return;

            if (keyboard[Key.R].wasPressedThisFrame)
            {
                OnRevealPressed();
                return;
            }

            foreach (var key in NextKeys)
            {
                if (keyboard[key].wasPressedThisFrame)
                {
                    OnNextPressed();
                    return;
                }
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

        async void LoadQuestions()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;

            try
            {
                _questions = await _questionService.GetQuestionsAsync(token);
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
                _questionText.text = "Could not load questions.";
                return;
            }

            if (token.IsCancellationRequested)
                return;
            StartExam();
        }

        void StartExam()
        {
            if (_questions == null || _questions.Count == 0)
                return;
            _session = new ExamSession(_questions);
            _examFinished = false;
            _resultsOverlay.AddToClassList(ResultsHiddenClass);
            ShowCurrentQuestion();
        }

        void ShowCurrentQuestion()
        {
            _answeredIndex = NoAnswer;

            var question = _session.Current;
            _categoryLabel.text = question.Section?.ToUpperInvariant();

            var number = _session.CurrentIndex + 1;
            var percent = Mathf.RoundToInt(100f * number / _session.Total);
            _progressLabel.text = $"Question {number} of {_session.Total}";
            _progressPercent.text = $"{percent}%";
            _progressFill.style.width = Length.Percent(percent);

            EnsureCardCount(question.Original.Answers.Count);
            RenderPrompt();
        }

        /// <summary>Shows the current text and state. Used for a new question, an answer and a reveal.</summary>
        void RenderPrompt()
        {
            var question = _session.Current;
            var prompt = _session.CurrentPrompt;
            var answered = _session.IsCurrentAnswered;

            _questionText.text = prompt.Question;
            _explanationText.text = prompt.Explanation;
            _explanationSection.EnableInClassList(HiddenClass, !answered);
            _simplifiedBadge.EnableInClassList(HiddenClass, !_session.IsCurrentRevealed);

            for (var i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(prompt.Answers[i], prompt.Feedbacks[i]);
                _cards[i].SetState(GetCardState(i, question.CorrectIndex));
            }

            _revealButton.SetEnabled(question.CanReveal && !_session.IsCurrentRevealed);
            _revealButton.text = _session.IsCurrentRevealed ? "Simplified Shown" : "Reveal Simplified [R]";

            var isLast = _session.CurrentIndex + 1 >= _session.Total;
            _nextButton.text = isLast ? "See Results" : "Next Question";
            _nextButton.EnableInClassList(HiddenClass, !answered);

            ScrollQuestionPanel(answered);
        }

        AnswerCardState GetCardState(int index, int correctIndex)
        {
            if (_answeredIndex == NoAnswer)
                return AnswerCardState.Interactive;
            if (index == correctIndex)
                return AnswerCardState.Correct;
            return index == _answeredIndex ? AnswerCardState.Wrong : AnswerCardState.Dimmed;
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
            if (_session == null || _examFinished || _session.IsCurrentAnswered)
                return;

            _session.Answer(index);
            _answeredIndex = index;
            RenderPrompt();
        }

        void OnRevealPressed()
        {
            if (_session == null || _examFinished)
                return;
            if (_session.Reveal())
                RenderPrompt();
        }

        void OnNextPressed()
        {
            if (_session == null || _examFinished || !_session.IsCurrentAnswered)
                return;

            if (_session.MoveNext())
                ShowCurrentQuestion();
            else
                ShowResults();
        }

        void ShowResults()
        {
            _examFinished = true;
            _resultsScore.text = $"You scored {_session.Score} / {_session.Total}";
            _resultsOverlay.RemoveFromClassList(ResultsHiddenClass);
        }
    }
}
