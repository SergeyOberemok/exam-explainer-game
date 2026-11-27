using System;
using System.Collections.Generic;
using System.Linq;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>One label of a <see cref="TypingSequence"/>, and the row it sits in, if any.</summary>
    public readonly struct TypingStep
    {
        public TypingStep(Label label, string text, VisualElement row = null)
        {
            Label = label;
            Text = text ?? string.Empty;
            Row = row;
        }

        public Label Label { get; }
        public string Text { get; }

        /// <summary>
        /// Hidden (<c>question-part--pending</c>) until its turn and marked <c>question-part--typing</c> while
        /// its text types; null for a label that stands alone.
        /// </summary>
        public VisualElement Row { get; }
    }

    /// <summary>
    /// Types several labels one after another, like someone writing the player instructions line by line (see
    /// <see cref="TypewriterSequence"/>). The labels must have rich text off, so cutting the text can't split a tag.
    /// </summary>
    public sealed class TypingSequence
    {
        public const string PendingClass = "question-part--pending";
        public const string TypingClass = "question-part--typing";

        readonly VisualElement _scheduler;
        readonly TypewriterOptions _options;
        readonly float _gapSeconds;
        IVisualElementScheduledItem _tick;
        TypingStep[] _steps = Array.Empty<TypingStep>();
        string[] _texts = Array.Empty<string>();
        float _startTime;
        int _index;
        int _length;

        public TypingSequence(VisualElement scheduler, TypewriterOptions options, float gapSeconds)
        {
            _scheduler = scheduler;
            _options = options;
            _gapSeconds = gapSeconds;
        }

        /// <summary>A step started typing; its row (or label) has just become visible.</summary>
        public event Action<TypingStep> StepStarted;

        public bool IsPlaying { get; private set; }

        /// <summary>The last step's text: what the sequence ends with (the ask).</summary>
        public string FinalText => _steps.Length == 0 ? null : _steps[_steps.Length - 1].Text;

        /// <summary>Empties every label, hides every row and types them in order.</summary>
        public void Play(IReadOnlyList<TypingStep> steps)
        {
            Stop();
            _steps = steps?.ToArray() ?? Array.Empty<TypingStep>();
            _texts = _steps.Select(step => step.Text).ToArray();
            foreach (var step in _steps)
            {
                step.Label.text = string.Empty;
                step.Row?.AddToClassList(PendingClass);
                step.Row?.RemoveFromClassList(TypingClass);
            }

            IsPlaying = true;
            _startTime = Time.realtimeSinceStartup;
            _index = -1;
            _length = -1;
            _tick ??= _scheduler.schedule.Execute(Tick).Every(16);
            _tick.Resume();
            Tick();
        }

        /// <summary>Shows every step whole now; does nothing when not playing.</summary>
        public void Complete()
        {
            if (!IsPlaying)
                return;
            Stop();
            foreach (var step in _steps)
            {
                step.Label.text = step.Text;
                step.Row?.RemoveFromClassList(PendingClass);
                step.Row?.RemoveFromClassList(TypingClass);
            }
        }

        /// <summary>Stops where it is, leaving the labels as they are (they are about to be replaced).</summary>
        public void Stop()
        {
            IsPlaying = false;
            _tick?.Pause();
        }

        void Tick()
        {
            if (!IsPlaying)
                return;
            var position = TypewriterSequence.At(_texts, Time.realtimeSinceStartup - _startTime, _options, _gapSeconds);
            if (position.Index >= _steps.Length)
            {
                Complete();
                return;
            }

            if (position.Index != _index)
            {
                for (var i = 0; i < _steps.Length; i++)
                {
                    var step = _steps[i];
                    if (i < position.Index)
                        step.Label.text = step.Text;
                    else if (i > position.Index)
                        step.Label.text = string.Empty;
                    step.Row?.EnableInClassList(PendingClass, i > position.Index);
                    step.Row?.EnableInClassList(TypingClass, i == position.Index);
                }
                _index = position.Index;
                _length = -1;
                StepStarted?.Invoke(_steps[_index]);
            }

            if (position.Length == _length)
                return;
            _length = position.Length;
            var current = _steps[_index];
            current.Label.text = current.Text.Substring(0, Math.Min(_length, current.Text.Length));
            // Its cast icons come in once the text is typed, during the gap before the next step.
            current.Row?.EnableInClassList(TypingClass, _length < current.Text.Length);
        }
    }
}
