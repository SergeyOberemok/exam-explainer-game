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

    /// <summary>
    /// Binds one AnswerCard.uxml instance to one answer. The front shows the answer, its icon and cast; once
    /// answered the card flips to its back, which shows what the answer leads to, and a click flips it again.
    /// </summary>
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
        const string FlipOutClass = "answer-card--flip-out";
        const string BackClass = "answer-card--back";

        /// <summary>Half a flip: the time the card takes to squash to its edge (the USS transition-duration).</summary>
        const long FlipHalfMs = 140;

        static readonly AnswerImpact[] Impacts = { AnswerImpact.Success, AnswerImpact.Partial, AnswerImpact.Failure };

        /// <summary>Answer-only cast members a card shows; more would crowd out the text.</summary>
        const int MaxEntities = 4;

        readonly VisualElement _card;
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
        readonly VisualElement _front;
        readonly VisualElement _back;
        readonly Label _selectLabel;
        readonly VisualElement _icon;
        readonly EntityStripView _entities;

        public AnswerCardView(VisualElement root, int index)
        {
            Root = root;
            Index = index;

            _card = root.Q("answerCard");
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
            _front = root.Q("cardFront");
            _back = root.Q("cardBack");
            _selectLabel = root.Q<Label>("selectLabel");
            _icon = root.Q("cardIcon");
            _entities = new EntityStripView(root.Q("cardEntities"), compact: false, maxShown: MaxEntities);

            root.Q<Button>("selectButton").clicked += () => Selected?.Invoke(Index);
            // Noted before the button sees the press, so the click that answers doesn't also flip the card back.
            _card.RegisterCallback<PointerDownEvent>(_ => _pressedWhileAnswered = IsAnswered(_state), TrickleDown.TrickleDown);
            _card.RegisterCallback<ClickEvent>(OnCardClicked);

            _letterBadge.text = ((char)('A' + index)).ToString();
        }

        public event Action<int> Selected;

        public VisualElement Root { get; }
        public int Index { get; }

        /// <summary>Whether the back (what the answer leads to) shows, or will once a running flip ends.</summary>
        public bool IsBackShown => _targetBack;

        AnswerCardState _state;
        AnswerOutcome _outcome;
        bool _backShown;
        bool _targetBack;
        bool _flipping;
        bool _pressedWhileAnswered;
        IVisualElementScheduledItem _flipStep;

        /// <summary>Sets the text only; doesn't change the card's state. Another answer puts the card on its front.</summary>
        public void Bind(string answerText, string feedbackText)
        {
            if (_answerText.text != answerText)
            {
                // Another question: the reused card starts on its front, unanswered.
                _scroll.scrollOffset = Vector2.zero;
                _state = AnswerCardState.Interactive;
                ShowFace(false, animate: false);
            }
            _answerText.text = answerText;
            _feedbackText.text = feedbackText;
        }

        /// <summary>Shows the large icon for what the answer proposes; null shows the default one.</summary>
        public void BindIcon(string iconKey) => EntityIcons.Show(_icon, iconKey);

        /// <summary>Shows the answer's cast chips; null or empty hides them. Doesn't touch the card's state.</summary>
        public void BindEntities(IReadOnlyList<CastMember> members) => _entities.Bind(members);

        /// <summary>
        /// Fills the back with what the answer leads to (an impact chip and the story line), why it is right or
        /// wrong, how many of the question's requirements it meets and what it misses. The back also shows the
        /// source feedback for the answer, above the outcome. A partial answer also tints the card. Doesn't touch the
        /// card's state.
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

        /// <summary>
        /// Sets the card's state. Once answered the card flips to its back after <paramref name="flipDelayMs"/>,
        /// so the cards can flip one after another; the same state again leaves the face alone.
        /// </summary>
        public void SetState(AnswerCardState state, long flipDelayMs = 0)
        {
            var wasAnswered = IsAnswered(_state);
            _card.EnableInClassList(InteractiveClass, state == AnswerCardState.Interactive);
            _card.EnableInClassList(CorrectClass, state == AnswerCardState.Correct);
            _card.EnableInClassList(WrongClass, state == AnswerCardState.Wrong);
            _card.EnableInClassList(DimmedClass, state == AnswerCardState.Dimmed);
            _card.EnableInClassList(EliminatedClass, state == AnswerCardState.Eliminated);
            _state = state;
            UpdateReviewVisibility();

            var answered = IsAnswered(state);
            if (!answered)
                ShowFace(false, animate: false);
            else if (!wasAnswered)
                ShowFace(HasBack, animate: true, flipDelayMs);

            _selectLabel.text = state switch
            {
                AnswerCardState.Eliminated => "Eliminated",
                AnswerCardState.Correct => "Correct!",
                AnswerCardState.Wrong => _outcome?.Impact == AnswerImpact.Partial ? "Partly works" : "Wrong",
                AnswerCardState.Dimmed => "Not Selected",
                _ => "Select Card",
            };
        }

        /// <summary>Turns an answered card to its other side; does nothing before answering.</summary>
        public void Toggle()
        {
            if (IsAnswered(_state) && HasBack)
                ShowFace(!_targetBack, animate: true);
        }

        static bool IsAnswered(AnswerCardState state) =>
            state != AnswerCardState.Interactive && state != AnswerCardState.Eliminated;

        /// <summary>The back has something to show: an outcome, or else the source feedback.</summary>
        bool HasBack => _outcome != null || !string.IsNullOrEmpty(_feedbackText.text);

        void OnCardClicked(ClickEvent evt)
        {
            // A click on the scroll bar scrolls; it isn't a flip.
            if (evt.target is VisualElement target && (target is Scroller || target.GetFirstAncestorOfType<Scroller>() != null))
                return;
            if (_pressedWhileAnswered)
                Toggle();
        }

        /// <summary>
        /// Shows one face. Animated, the card squashes to its edge (after <paramref name="delayMs"/>), swaps
        /// faces and opens again; asking again while it flips only changes the face it lands on.
        /// </summary>
        void ShowFace(bool back, bool animate, long delayMs = 0)
        {
            _targetBack = back;
            if (!animate)
            {
                _flipStep?.Pause();
                _flipStep = null;
                _flipping = false;
                _card.RemoveFromClassList(FlipOutClass);
                ApplyFace(back);
                return;
            }
            if (_flipping || back == _backShown)
                return;

            _flipping = true;
            _flipStep = _card.schedule.Execute(() =>
            {
                if (_targetBack == _backShown)
                {
                    _flipping = false;
                    _flipStep = null;
                    return;
                }
                _card.AddToClassList(FlipOutClass);
                _flipStep = _card.schedule.Execute(() =>
                {
                    ApplyFace(_targetBack);
                    _card.RemoveFromClassList(FlipOutClass);
                    _flipping = false;
                    _flipStep = null;
                }).StartingIn(FlipHalfMs);
            }).StartingIn(delayMs);
        }

        void ApplyFace(bool back)
        {
            if (_backShown != back)
                _scroll.scrollOffset = Vector2.zero;
            _backShown = back;
            _front.EnableInClassList(HiddenClass, back);
            _back.EnableInClassList(HiddenClass, !back);
            _card.EnableInClassList(BackClass, back);
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
        /// The feedback and the outcome show only once the question is answered, and each line only when it
        /// has text.
        /// </summary>
        void UpdateReviewVisibility()
        {
            var answered = IsAnswered(_state);
            var hasOutcome = _outcome != null;
            _feedbackText.EnableInClassList(HiddenClass, !answered || string.IsNullOrEmpty(_feedbackText.text));
            _outcomeSection.EnableInClassList(HiddenClass, !answered || !hasOutcome);
            _impactChip.EnableInClassList(HiddenClass, !answered || !hasOutcome);
            _consequenceText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_consequenceText.text));
            _reasonText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_reasonText.text));
            _metText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_metText.text));
            _gapsText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_gapsText.text));
        }
    }
}
