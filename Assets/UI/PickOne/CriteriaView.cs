using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// The criteria modal: what a correct answer has to achieve, as a checklist. Before the question is
    /// answered every box is empty; afterwards the boxes of the criteria the answer that counts meets are
    /// ticked. <see cref="PickOneScreen"/> decides what to bind.
    /// </summary>
    sealed class CriteriaView
    {
        const string HiddenClass = "map--hidden";

        readonly VisualElement _overlay;
        readonly VisualElement _closeButton;
        readonly Label _title;
        readonly Label _caption;
        readonly VisualElement _list;

        public CriteriaView(VisualElement root)
        {
            _overlay = root.Q("criteriaOverlay");
            _closeButton = root.Q("criteriaCloseButton");
            _title = root.Q<Label>("criteriaTitle");
            _caption = root.Q<Label>("criteriaCaption");
            _list = root.Q("criteriaList");

            _overlay.RegisterCallback<ClickEvent>(OnOverlayClicked);
            _closeButton.RegisterCallback<ClickEvent>(OnCloseClicked);
        }

        public event Action CloseRequested;

        public bool IsOpen => !_overlay.ClassListContains(HiddenClass);

        public void Open() => _overlay.RemoveFromClassList(HiddenClass);

        public void Close() => _overlay.AddToClassList(HiddenClass);

        public void Dispose()
        {
            _overlay.UnregisterCallback<ClickEvent>(OnOverlayClicked);
            _closeButton.UnregisterCallback<ClickEvent>(OnCloseClicked);
        }

        /// <param name="criteria">What a correct answer has to achieve.</param>
        /// <param name="met">Whether the answer that counts meets each one, aligned with <paramref name="criteria"/>
        /// (shorter counts as not met); null while there is no verdict, which leaves every box empty.</param>
        public void Bind(IReadOnlyList<string> criteria, IReadOnlyList<bool> met)
        {
            _list.Clear();
            for (var i = 0; criteria != null && i < criteria.Count; i++)
            {
                bool? verdict = met == null ? null : i < met.Count && met[i];
                _list.Add(RequirementsView.BuildRow(criteria[i], verdict));
            }

            var count = criteria?.Count ?? 0;
            if (met == null)
            {
                _title.text = "Criteria";
                _caption.text = "What a correct answer has to achieve. Answer to see which ones yours meets.";
            }
            else
            {
                var metCount = met.Take(count).Count(m => m);
                _title.text = $"Criteria · yours meets {metCount} of {count}";
                _caption.text = "What a correct answer had to achieve; ticked are the ones your answer meets.";
            }
        }

        void OnOverlayClicked(ClickEvent evt)
        {
            // Only a click on the dimmed backdrop itself, not on the panel.
            if (evt.target == _overlay)
                CloseRequested?.Invoke();
        }

        void OnCloseClicked(ClickEvent evt) => CloseRequested?.Invoke();
    }
}
