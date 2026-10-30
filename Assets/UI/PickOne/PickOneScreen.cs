using System;
using System.Collections;
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
    /// handles card picks (click or keys 1-4), shows feedback and the final score.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PickOneScreen : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset _answerCardTemplate;

        [Tooltip("Local images for the mock service; matched by texture name against image keys.")]
        [SerializeField] Texture2D[] _images = Array.Empty<Texture2D>();

        [SerializeField, Min(0f)] float _feedbackSeconds = 1.5f;

        static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };

        readonly List<AnswerCardView> _cards = new();
        IQuestionService _questionService;
        IReadOnlyList<Question> _questions;
        ExamSession _session;
        CancellationTokenSource _loadCancellation;
        bool _acceptingInput;

        Label _categoryLabel;
        Label _xpLabel;
        Label _challengeLabel;
        Label _questionText;
        Label _hintLabel;
        Label _clueLabel;
        VisualElement _clueImage;
        Label _progressLabel;
        Label _progressPercent;
        VisualElement _progressFill;
        VisualElement _cardsRow;
        VisualElement _resultsOverlay;
        Label _resultsScore;
        Button _playAgainButton;

        void OnEnable()
        {
            _questionService ??= new MockQuestionService();

            var root = GetComponent<UIDocument>().rootVisualElement;
            _categoryLabel = root.Q<Label>("categoryLabel");
            _xpLabel = root.Q<Label>("xpLabel");
            _challengeLabel = root.Q<Label>("challengeLabel");
            _questionText = root.Q<Label>("questionText");
            _hintLabel = root.Q<Label>("hintLabel");
            _clueLabel = root.Q<Label>("clueLabel");
            _clueImage = root.Q("clueImage");
            _progressLabel = root.Q<Label>("progressLabel");
            _progressPercent = root.Q<Label>("progressPercent");
            _progressFill = root.Q("progressFill");
            _cardsRow = root.Q("cardsRow");
            _resultsOverlay = root.Q("resultsOverlay");
            _resultsScore = root.Q<Label>("resultsScore");
            _playAgainButton = root.Q<Button>("playAgainButton");

            _playAgainButton.clicked += StartExam;
            _cardsRow.Clear();
            _cards.Clear();

            LoadQuestions();
        }

        void OnDisable()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            StopAllCoroutines();
            _acceptingInput = false;
            if (_playAgainButton != null)
                _playAgainButton.clicked -= StartExam;
        }

        void Update()
        {
            if (!_acceptingInput || Keyboard.current == null)
                return;

            for (var i = 0; i < DigitKeys.Length && i < _cards.Count; i++)
            {
                if (Keyboard.current[DigitKeys[i]].wasPressedThisFrame || Keyboard.current[NumpadKeys[i]].wasPressedThisFrame)
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
            _resultsOverlay.AddToClassList("results--hidden");
            ShowCurrentQuestion();
        }

        void ShowCurrentQuestion()
        {
            var question = _session.Current;

            _categoryLabel.text = question.Category?.ToUpperInvariant();
            _xpLabel.text = $"+{question.XpReward} XP";
            _challengeLabel.text = question.ChallengeLabel?.ToUpperInvariant();
            _questionText.text = question.Prompt;
            _hintLabel.text = question.Hint;
            _clueLabel.text = question.ClueText;
            _clueImage.style.backgroundImage = ToBackground(FindImage(question.ClueImage));

            var number = _session.CurrentIndex + 1;
            var percent = Mathf.RoundToInt(100f * number / _session.Total);
            _progressLabel.text = $"Question {number} of {_session.Total}";
            _progressPercent.text = $"{percent}%";
            _progressFill.style.width = Length.Percent(percent);

            EnsureCardCount(question.Options.Count);
            for (var i = 0; i < _cards.Count; i++)
            {
                var option = question.Options[i];
                _cards[i].Bind(option, FindImage(option.Image));
            }

            _acceptingInput = true;
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
            if (!_acceptingInput || _session == null || _session.IsCurrentAnswered)
                return;
            _acceptingInput = false;

            var result = _session.Answer(index);
            for (var i = 0; i < _cards.Count; i++)
            {
                AnswerCardState state;
                if (i == result.CorrectIndex)
                    state = AnswerCardState.Correct;
                else if (i == index)
                    state = AnswerCardState.Wrong;
                else
                    state = AnswerCardState.Dimmed;
                _cards[i].SetState(state);
            }

            StartCoroutine(AdvanceAfterFeedback());
        }

        IEnumerator AdvanceAfterFeedback()
        {
            yield return new WaitForSeconds(_feedbackSeconds);

            if (_session.MoveNext())
                ShowCurrentQuestion();
            else
                ShowResults();
        }

        void ShowResults()
        {
            _resultsScore.text = $"You scored {_session.Score} / {_session.Total}";
            _resultsOverlay.RemoveFromClassList("results--hidden");
        }

        Texture2D FindImage(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            foreach (var image in _images)
            {
                if (image != null && image.name == key)
                    return image;
            }

            Debug.LogWarning($"No image found for key '{key}'.", this);
            return null;
        }

        static StyleBackground ToBackground(Texture2D texture) =>
            texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
    }
}
