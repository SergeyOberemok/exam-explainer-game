using System;
using System.Collections.Generic;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    public enum AnswerCardState
    {
        Interactive,
        Correct,
        Wrong,
        Dimmed,
        Eliminated,
    }

    /// <summary>Binds one AnswerCard.uxml instance to the text of one answer.</summary>
    public sealed class AnswerCardView
    {
        const string InteractiveClass = "answer-card--interactive";
        const string CorrectClass = "answer-card--correct";
        const string WrongClass = "answer-card--wrong";
        const string DimmedClass = "answer-card--dimmed";
        const string EliminatedClass = "answer-card--eliminated";
        const string HiddenClass = "is-hidden";

        /// <summary>The backend sends at most this many per answer.</summary>
        const int MaxEntities = 4;

        readonly VisualElement _card;
        readonly Label _keyLabel;
        readonly Label _letterBadge;
        readonly Label _answerText;
        readonly Label _feedbackText;
        readonly Label _reasonText;
        readonly Label _metText;
        readonly Label _gapsText;
        readonly ScrollView _scroll;
        readonly Label _selectLabel;
        readonly VisualElement _icon;
        readonly EntityStripView _entities;

        public AnswerCardView(VisualElement root, int index)
        {
            Root = root;
            Index = index;

            _card = root.Q("answerCard");
            _keyLabel = root.Q<Label>("keyLabel");
            _letterBadge = root.Q<Label>("letterBadge");
            _answerText = root.Q<Label>("answerText");
            _feedbackText = root.Q<Label>("feedbackText");
            _reasonText = root.Q<Label>("reasonText");
            _metText = root.Q<Label>("metText");
            _gapsText = root.Q<Label>("gapsText");
            _scroll = root.Q<ScrollView>("cardScroll");
            _selectLabel = root.Q<Label>("selectLabel");
            _icon = root.Q("cardIcon");
            _entities = new EntityStripView(root.Q("cardEntities"), compact: false, maxShown: MaxEntities);

            root.Q<Button>("selectButton").clicked += () => Selected?.Invoke(Index);

            _keyLabel.text = $"Key [{index + 1}]";
            _letterBadge.text = ((char)('A' + index)).ToString();
        }

        public event Action<int> Selected;

        public VisualElement Root { get; }
        public int Index { get; }

        AnswerCardState _state;
        bool _hasEvaluation;

        /// <summary>Sets the text only, so a reveal can swap it without changing the card's state.</summary>
        public void Bind(string answerText, string feedbackText)
        {
            _answerText.text = answerText;
            _feedbackText.text = feedbackText;
            _scroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Shows the large icon for what the answer is about; null shows the default one.</summary>
        public void BindIcon(string iconKey) => EntityIcons.Show(_icon, iconKey);

        /// <summary>Shows the answer's entity icons; null or empty hides them. Doesn't touch the card's state.</summary>
        public void BindEntities(IReadOnlyList<VisualEntity> entities) => _entities.Bind(entities);

        /// <summary>
        /// Shows, once answered, why the answer is right or wrong, how many of the question's requirements it
        /// meets and what it misses. With an evaluation these take the source feedback's place on the card (the
        /// debrief lists that feedback); null keeps the feedback. Doesn't touch the card's state.
        /// </summary>
        /// <param name="met">Requirements the answer meets.</param>
        /// <param name="total">Requirements of the question; 0 hides the count.</param>
        public void BindResolution(OptionEvaluation option, int met, int total)
        {
            _hasEvaluation = option != null;
            _reasonText.text = option?.Reason ?? string.Empty;
            _metText.text = option == null || total == 0 ? string.Empty : $"Meets {met} of {total} requirements";
            _gapsText.text = option == null || option.IsCorrect || option.Gaps.Count == 0
                ? string.Empty
                : "Missing: " + string.Join("; ", option.Gaps);
            UpdateReviewVisibility();
        }

        public void SetState(AnswerCardState state)
        {
            _card.EnableInClassList(InteractiveClass, state == AnswerCardState.Interactive);
            _card.EnableInClassList(CorrectClass, state == AnswerCardState.Correct);
            _card.EnableInClassList(WrongClass, state == AnswerCardState.Wrong);
            _card.EnableInClassList(DimmedClass, state == AnswerCardState.Dimmed);
            _card.EnableInClassList(EliminatedClass, state == AnswerCardState.Eliminated);
            _state = state;
            UpdateReviewVisibility();

            _selectLabel.text = state switch
            {
                AnswerCardState.Eliminated => "Eliminated",
                AnswerCardState.Correct => "Correct!",
                AnswerCardState.Wrong => "Wrong",
                AnswerCardState.Dimmed => "Not Selected",
                _ => $"Select Card [{Index + 1}]",
            };
        }

        /// <summary>
        /// Feedback (without an evaluation), reason, count and gaps show only once the question is answered, and
        /// only when they have text.
        /// </summary>
        void UpdateReviewVisibility()
        {
            var answered = _state != AnswerCardState.Interactive && _state != AnswerCardState.Eliminated;
            _feedbackText.EnableInClassList(HiddenClass, !answered || _hasEvaluation || string.IsNullOrEmpty(_feedbackText.text));
            _reasonText.EnableInClassList(HiddenClass, !answered || string.IsNullOrEmpty(_reasonText.text));
            _metText.EnableInClassList(HiddenClass, !answered || string.IsNullOrEmpty(_metText.text));
            _gapsText.EnableInClassList(HiddenClass, !answered || string.IsNullOrEmpty(_gapsText.text));
        }
    }
}
