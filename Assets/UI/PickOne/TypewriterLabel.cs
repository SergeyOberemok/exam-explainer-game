using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Shows a label's text either at once or typed out like a streamed chat message. The label must have rich
    /// text off, so cutting the text can't split a tag.
    /// </summary>
    public sealed class TypewriterLabel
    {
        readonly Label _label;
        readonly TypewriterOptions _options;
        IVisualElementScheduledItem _tick;
        string _text = string.Empty;
        float _startTime;
        int _shown;

        public TypewriterLabel(Label label, TypewriterOptions? options = null)
        {
            _label = label;
            _options = options ?? TypewriterOptions.Default;
        }

        public bool IsTyping { get; private set; }

        /// <summary>The full text being shown or typed.</summary>
        public string Text => _text;

        /// <summary>Types the text from the start.</summary>
        public void Type(string text)
        {
            _text = text ?? string.Empty;
            _shown = 0;
            _label.text = string.Empty;
            if (_text.Length == 0)
            {
                Stop();
                return;
            }

            IsTyping = true;
            _startTime = Time.realtimeSinceStartup;
            _tick ??= _label.schedule.Execute(Tick).Every(16);
            _tick.Resume();
        }

        /// <summary>Shows the whole text now and stops any typing.</summary>
        public void ShowImmediately(string text)
        {
            Stop();
            _text = text ?? string.Empty;
            _label.text = _text;
        }

        /// <summary>Finishes typing at once; does nothing when not typing.</summary>
        public void Complete()
        {
            if (!IsTyping)
                return;
            ShowImmediately(_text);
        }

        void Stop()
        {
            IsTyping = false;
            _tick?.Pause();
        }

        void Tick()
        {
            if (!IsTyping)
                return;
            var length = TypewriterTimeline.VisibleLength(_text, Time.realtimeSinceStartup - _startTime, _options);
            if (length >= _text.Length)
            {
                ShowImmediately(_text);
                return;
            }
            if (length == _shown)
                return;
            _shown = length;
            _label.text = _text.Substring(0, length);
        }
    }
}
