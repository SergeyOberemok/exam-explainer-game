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

    /// <summary>Binds one AnswerCard.uxml instance to one answer: its text, icon, cast and, once answered, its outcome.</summary>
    public sealed class AnswerCardView
    {
        const string InteractiveClass = "answer-card--interactive";
        const string CorrectClass = "answer-card--correct";
        const string WrongClass = "answer-card--wrong";
        const string DimmedClass = "answer-card--dimmed";
        const string EliminatedClass = "answer-card--eliminated";
        const string ImpactCardClass = "answer-card--impact-";
        const string ImpactChipClass = "card__impact--";
        const string HiddenClass = "is-hidden";

        static readonly AnswerImpact[] Impacts = { AnswerImpact.Success, AnswerImpact.Partial, AnswerImpact.Failure };

        /// <summary>Answer-only cast members a card shows; more would crowd out the text.</summary>
        const int MaxEntities = 4;

        readonly VisualElement _card;
        readonly Label _keyLabel;
        readonly Label _letterBadge;
        readonly Label _answerText;
        readonly Label _feedbackText;
        readonly VisualElement _outcomeSection;
        readonly VisualElement _impactChip;
        readonly Label _impactLabel;
        readonly Label _consequenceText;
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
            _outcomeSection = root.Q("outcomeSection");
            _impactChip = root.Q("impactChip");
            _impactLabel = root.Q<Label>("impactLabel");
            _consequenceText = root.Q<Label>("consequenceText");
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
        AnswerOutcome _outcome;

        /// <summary>Sets the text only; doesn't change the card's state.</summary>
        public void Bind(string answerText, string feedbackText)
        {
            if (_answerText.text != answerText)
                _scroll.scrollOffset = Vector2.zero;
            _answerText.text = answerText;
            _feedbackText.text = feedbackText;
        }

        /// <summary>Shows the large icon for what the answer proposes; null shows the default one.</summary>
        public void BindIcon(string iconKey) => EntityIcons.Show(_icon, iconKey);

        /// <summary>Shows the answer's cast chips; null or empty hides them. Doesn't touch the card's state.</summary>
        public void BindEntities(IReadOnlyList<CastMember> members) => _entities.Bind(members);

        /// <summary>
        /// Shows, once answered, what the answer leads to (an impact chip and the story line), why it is right or
        /// wrong, how many of the question's requirements it meets and what it misses. With an outcome these
        /// take the source feedback's place on the card (the debrief lists that feedback); null keeps the
        /// feedback. A partial answer also tints the card. Doesn't touch the card's state.
        /// </summary>
        /// <param name="met">Requirements the answer meets.</param>
        /// <param name="total">Requirements to count against; 0 hides the count.</param>
        public void BindResolution(AnswerOutcome outcome, int met, int total)
        {
            _outcome = outcome;
            foreach (var impact in Impacts)
            {
                var name = ImpactName(impact);
                var on = outcome != null && outcome.Impact == impact;
                _card.EnableInClassList(ImpactCardClass + name, on);
                _impactChip.EnableInClassList(ImpactChipClass + name, on);
            }
            _impactLabel.text = outcome == null ? string.Empty : ImpactText(outcome.Impact);
            _consequenceText.text = outcome?.Consequence ?? string.Empty;
            _reasonText.text = outcome?.Reason ?? string.Empty;
            _metText.text = outcome == null || total == 0 ? string.Empty : $"Meets {met} of {total} requirements";
            _gapsText.text = outcome == null || outcome.IsCorrect || outcome.Gaps.Count == 0
                ? string.Empty
                : "Missing: " + string.Join("; ", outcome.Gaps);
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
                AnswerCardState.Wrong => _outcome?.Impact == AnswerImpact.Partial ? "Partly works" : "Wrong",
                AnswerCardState.Dimmed => "Not Selected",
                _ => $"Select Card [{Index + 1}]",
            };
        }

        static string ImpactName(AnswerImpact impact) => impact switch
        {
            AnswerImpact.Success => "success",
            AnswerImpact.Partial => "partial",
            _ => "failure",
        };

        static string ImpactText(AnswerImpact impact) => impact switch
        {
            AnswerImpact.Success => "WORKS",
            AnswerImpact.Partial => "PARTLY WORKS",
            _ => "FAILS",
        };

        /// <summary>
        /// The feedback (without an outcome) or the outcome shows only once the question is answered, and each
        /// line only when it has text.
        /// </summary>
        void UpdateReviewVisibility()
        {
            var answered = _state != AnswerCardState.Interactive && _state != AnswerCardState.Eliminated;
            var hasOutcome = _outcome != null;
            _feedbackText.EnableInClassList(HiddenClass, !answered || hasOutcome || string.IsNullOrEmpty(_feedbackText.text));
            _outcomeSection.EnableInClassList(HiddenClass, !answered || !hasOutcome);
            _consequenceText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_consequenceText.text));
            _reasonText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_reasonText.text));
            _metText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_metText.text));
            _gapsText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_gapsText.text));
        }
    }
}
