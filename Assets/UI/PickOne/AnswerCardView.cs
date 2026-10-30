using System;
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
    }

    /// <summary>Binds one AnswerCard.uxml instance to an <see cref="AnswerOption"/>.</summary>
    public sealed class AnswerCardView
    {
        const string InteractiveClass = "answer-card--interactive";
        const string CorrectClass = "answer-card--correct";
        const string WrongClass = "answer-card--wrong";
        const string DimmedClass = "answer-card--dimmed";

        static readonly string[] Letters = { "A", "B", "C", "D", "E", "F" };

        readonly VisualElement _card;
        readonly Label _primaryTag;
        readonly Label _secondaryTag;
        readonly Label _keyLabel;
        readonly VisualElement _artwork;
        readonly Label _letterBadge;
        readonly Label _tierLabel;
        readonly Label _titleLabel;
        readonly Label _latinLabel;
        readonly Label _descriptionLabel;
        readonly VisualElement _traitsRow;
        readonly Label _selectLabel;

        public AnswerCardView(VisualElement root, int index)
        {
            Root = root;
            Index = index;

            _card = root.Q("answerCard");
            _primaryTag = root.Q<Label>("primaryTag");
            _secondaryTag = root.Q<Label>("secondaryTag");
            _keyLabel = root.Q<Label>("keyLabel");
            _artwork = root.Q("artwork");
            _letterBadge = root.Q<Label>("letterBadge");
            _tierLabel = root.Q<Label>("tierLabel");
            _titleLabel = root.Q<Label>("titleLabel");
            _latinLabel = root.Q<Label>("latinLabel");
            _descriptionLabel = root.Q<Label>("descriptionLabel");
            _traitsRow = root.Q("traitsRow");
            _selectLabel = root.Q<Label>("selectLabel");

            root.Q<Button>("selectButton").clicked += () => Selected?.Invoke(Index);

            _keyLabel.text = $"Key [{index + 1}]";
            _letterBadge.text = Letters[index % Letters.Length];
        }

        public event Action<int> Selected;

        public VisualElement Root { get; }
        public int Index { get; }

        public void Bind(AnswerOption option, Texture2D image)
        {
            _primaryTag.text = option.PrimaryTag;
            _secondaryTag.text = option.SecondaryTag;
            _tierLabel.text = option.Tier;
            _titleLabel.text = option.Title;
            _latinLabel.text = option.Subtitle;
            _descriptionLabel.text = option.Description;

            // Image comes from data, so it is set in code rather than USS.
            _artwork.style.backgroundImage = image != null ? new StyleBackground(image) : new StyleBackground(StyleKeyword.None);

            _traitsRow.Clear();
            foreach (var trait in option.Traits)
            {
                var chip = new VisualElement();
                chip.AddToClassList("chip");
                chip.AddToClassList("trait");
                var label = new Label(trait);
                label.AddToClassList("chip__text");
                chip.Add(label);
                _traitsRow.Add(chip);
            }

            SetState(AnswerCardState.Interactive);
        }

        public void SetState(AnswerCardState state)
        {
            _card.EnableInClassList(InteractiveClass, state == AnswerCardState.Interactive);
            _card.EnableInClassList(CorrectClass, state == AnswerCardState.Correct);
            _card.EnableInClassList(WrongClass, state == AnswerCardState.Wrong);
            _card.EnableInClassList(DimmedClass, state == AnswerCardState.Dimmed);

            _selectLabel.text = state switch
            {
                AnswerCardState.Correct => "Correct!",
                AnswerCardState.Wrong => "Wrong",
                _ => $"Select Card [{Index + 1}]",
            };
        }
    }
}
