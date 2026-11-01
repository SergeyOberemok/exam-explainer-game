using System;
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
    }

    /// <summary>Binds one AnswerCard.uxml instance to the text of one answer.</summary>
    public sealed class AnswerCardView
    {
        const string InteractiveClass = "answer-card--interactive";
        const string CorrectClass = "answer-card--correct";
        const string WrongClass = "answer-card--wrong";
        const string DimmedClass = "answer-card--dimmed";
        const string HiddenClass = "is-hidden";

        static readonly string[] Letters = { "A", "B", "C", "D", "E", "F" };

        readonly VisualElement _card;
        readonly Label _keyLabel;
        readonly Label _letterBadge;
        readonly Label _answerText;
        readonly Label _feedbackText;
        readonly ScrollView _scroll;
        readonly Label _selectLabel;

        public AnswerCardView(VisualElement root, int index)
        {
            Root = root;
            Index = index;

            _card = root.Q("answerCard");
            _keyLabel = root.Q<Label>("keyLabel");
            _letterBadge = root.Q<Label>("letterBadge");
            _answerText = root.Q<Label>("answerText");
            _feedbackText = root.Q<Label>("feedbackText");
            _scroll = root.Q<ScrollView>("cardScroll");
            _selectLabel = root.Q<Label>("selectLabel");

            root.Q<Button>("selectButton").clicked += () => Selected?.Invoke(Index);

            _keyLabel.text = $"Key [{index + 1}]";
            _letterBadge.text = Letters[index % Letters.Length];
        }

        public event Action<int> Selected;

        public VisualElement Root { get; }
        public int Index { get; }

        /// <summary>Sets the text only, so a reveal can swap it without changing the card's state.</summary>
        public void Bind(string answerText, string feedbackText)
        {
            _answerText.text = answerText;
            _feedbackText.text = feedbackText;
            _scroll.scrollOffset = Vector2.zero;
        }

        public void SetState(AnswerCardState state)
        {
            _card.EnableInClassList(InteractiveClass, state == AnswerCardState.Interactive);
            _card.EnableInClassList(CorrectClass, state == AnswerCardState.Correct);
            _card.EnableInClassList(WrongClass, state == AnswerCardState.Wrong);
            _card.EnableInClassList(DimmedClass, state == AnswerCardState.Dimmed);
            _feedbackText.EnableInClassList(HiddenClass, state == AnswerCardState.Interactive);

            _selectLabel.text = state switch
            {
                AnswerCardState.Correct => "Correct!",
                AnswerCardState.Wrong => "Wrong",
                _ => $"Select Card [{Index + 1}]",
            };
        }
    }
}
