using System;
using System.Collections.Generic;
using System.Linq;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// The map modal: a question's flow (with its answers) or, after answering, its resolution. Laying out a map
    /// takes two passes. The boxes are built first, hidden, so their wrapped labels get a height. Then
    /// <see cref="FlowLayout"/> places them and the edges. A hidden element gets no layout, so the second pass
    /// waits until the modal is open. <see cref="PickOneScreen"/> decides which map to show.
    /// </summary>
    sealed class FlowMapView
    {
        const string HiddenClass = "map--hidden";
        const string IsHiddenClass = "is-hidden";
        const string MeasuringClass = "map__canvas--measuring";
        const string ActiveTabClass = "map__tab--active";
        const int MaxNodeCast = 3;
        const long LayoutPollMs = 16;
        const int MaxLayoutAttempts = 30;

        static readonly FlowLayoutOptions LayoutOptions = new FlowLayoutOptions();

        // Stand-ins for a box or label that never got a size; the box width matches .flow-node in PickOne.uss.
        static readonly FlowSize FallbackBoxSize = new(210f, 90f);
        static readonly FlowSize FallbackLabelSize = new(120f, 18f);

        readonly VisualElement _overlay;
        readonly VisualElement _closeButton;
        readonly VisualElement _tabs;
        readonly VisualElement _questionTab;
        readonly VisualElement _resolutionTab;
        readonly Label _caption;
        readonly Label _empty;
        readonly Label _hint;
        readonly ScrollView _scroll;
        readonly VisualElement _canvas;
        readonly FlowEdgeLayer _edges = new();
        readonly Dictionary<string, VisualElement> _boxes = new();
        readonly List<(int Edge, Label Label)> _edgeLabels = new();

        FlowMapModel _model;
        string _boundKey;
        bool _laidOut;
        int _layoutAttempts;
        float _canvasWidth;
        IVisualElementScheduledItem _pendingLayout;

        public FlowMapView(VisualElement root)
        {
            _overlay = root.Q("mapOverlay");
            _closeButton = root.Q("mapCloseButton");
            _tabs = root.Q("mapTabs");
            _questionTab = root.Q("mapQuestionTab");
            _resolutionTab = root.Q("mapResolutionTab");
            _caption = root.Q<Label>("mapCaption");
            _empty = root.Q<Label>("mapEmpty");
            _hint = root.Q<Label>("mapHint");
            _scroll = root.Q<ScrollView>("mapScroll");
            _canvas = root.Q("mapCanvas");

            _overlay.RegisterCallback<ClickEvent>(OnOverlayClicked);
            _closeButton.RegisterCallback<ClickEvent>(OnCloseClicked);
            _questionTab.RegisterCallback<ClickEvent>(OnQuestionTabClicked);
            _resolutionTab.RegisterCallback<ClickEvent>(OnResolutionTabClicked);
            _scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportResized);
        }

        public event Action CloseRequested;

        /// <summary>The player clicked a tab.</summary>
        public event Action<FlowMapKind> KindRequested;

        public bool IsOpen => !_overlay.ClassListContains(HiddenClass);

        public void Dispose()
        {
            _pendingLayout?.Pause();
            _overlay.UnregisterCallback<ClickEvent>(OnOverlayClicked);
            _closeButton.UnregisterCallback<ClickEvent>(OnCloseClicked);
            _questionTab.UnregisterCallback<ClickEvent>(OnQuestionTabClicked);
            _resolutionTab.UnregisterCallback<ClickEvent>(OnResolutionTabClicked);
            _scroll.contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportResized);
        }

        public void Open()
        {
            _overlay.RemoveFromClassList(HiddenClass);
            if (!_laidOut)
                ScheduleLayout();
        }

        public void Close()
        {
            _pendingLayout?.Pause();
            _overlay.AddToClassList(HiddenClass);
        }

        /// <summary>
        /// Forgets what is bound, so the next <see cref="Bind"/> builds the map again even under the same key: a
        /// new round can bring a question with the same id and a different map.
        /// </summary>
        public void Reset()
        {
            _pendingLayout?.Pause();
            _boundKey = null;
            _model = null;
        }

        /// <summary>The tabs show only once both maps exist, that is, after answering a resolved question.</summary>
        public void SetTabs(bool hasResolution, FlowMapKind active)
        {
            _tabs.EnableInClassList(IsHiddenClass, !hasResolution);
            _hint.text = hasResolution ? "[M] or [Esc] Close   ·   [←] [→] Switch map" : "[M] or [Esc] Close";
            _questionTab.EnableInClassList(ActiveTabClass, active == FlowMapKind.Question);
            _resolutionTab.EnableInClassList(ActiveTabClass, active == FlowMapKind.Resolution);
        }

        /// <param name="key">What the model shows; the same key again keeps the boxes already laid out.</param>
        public void Bind(FlowMapModel model, string key)
        {
            if (key == _boundKey && _model != null)
                return;
            _boundKey = key;
            _model = model;
            _laidOut = false;
            _pendingLayout?.Pause();

            _canvas.Clear();
            _boxes.Clear();
            _edgeLabels.Clear();
            _edges.SetEdges(null);
            _canvas.Add(_edges);

            _caption.text = model.Caption ?? string.Empty;
            _caption.EnableInClassList(IsHiddenClass, string.IsNullOrEmpty(model.Caption));
            _empty.EnableInClassList(IsHiddenClass, !model.IsEmpty);
            _canvas.EnableInClassList(IsHiddenClass, model.IsEmpty);
            if (model.IsEmpty)
            {
                _laidOut = true;
                return;
            }

            _canvas.AddToClassList(MeasuringClass);
            foreach (var node in model.Nodes)
            {
                if (_boxes.ContainsKey(node.Id))
                    continue;
                var box = MakeBox(node);
                _boxes[node.Id] = box;
                _canvas.Add(box);
            }
            for (var i = 0; i < model.Edges.Count; i++)
            {
                var text = model.Edges[i].Label;
                if (string.IsNullOrEmpty(text))
                    continue;
                var label = new Label(text) { enableRichText = false, pickingMode = PickingMode.Ignore };
                label.AddToClassList("flow-edge-label");
                _edgeLabels.Add((i, label));
                _canvas.Add(label);
            }

            if (IsOpen)
                ScheduleLayout();
        }

        void ScheduleLayout()
        {
            _pendingLayout?.Pause();
            _layoutAttempts = 0;
            _pendingLayout = _canvas.schedule.Execute(TryLayout).Every(LayoutPollMs);
        }

        /// <summary>Places the boxes once every one of them has a size.</summary>
        void TryLayout()
        {
            if (_model == null || _laidOut)
            {
                _pendingLayout?.Pause();
                return;
            }
            if (!IsOpen)
                return;
            // Wait for every box and label to get a size, but not forever: past the limit, the ones without
            // one take a stand-in size, so the map still shows.
            var measured = _boxes.Values.All(HasSize) && _edgeLabels.All(item => HasSize(item.Label));
            if (!measured && ++_layoutAttempts < MaxLayoutAttempts)
                return;

            var layout = _model.Layout(
                id => _boxes.TryGetValue(id, out var box) ? SizeOf(box, FallbackBoxSize) : new FlowSize(1f, 1f),
                LayoutOptions,
                edge =>
                {
                    var item = _edgeLabels.Find(entry => entry.Edge == edge);
                    return item.Label != null ? SizeOf(item.Label, FallbackLabelSize) : default;
                });

            // The positions come from the data, like the progress width: the inline styles here.
            foreach (var (id, rect) in layout.Nodes)
            {
                var box = _boxes[id];
                box.style.left = rect.X;
                box.style.top = rect.Y;
            }
            _canvas.style.width = layout.Width;
            _canvas.style.height = layout.Height;
            _canvasWidth = layout.Width;
            CenterCanvas();
            _edges.SetEdges(_model.Edges.Select((edge, i) => (layout.Edges[i], edge.Style)));
            foreach (var (edge, label) in _edgeLabels)
            {
                // The label's centre: its USS translates it by half its size.
                if (layout.Labels[edge] is not FlowPoint centre)
                {
                    label.AddToClassList(IsHiddenClass);
                    continue;
                }
                label.style.left = centre.X;
                label.style.top = centre.Y;
            }

            _laidOut = true;
            _pendingLayout?.Pause();
            _canvas.RemoveFromClassList(MeasuringClass);
            _scroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Centres the map while it is narrower than the view; a wider one starts at the left and scrolls.</summary>
        void CenterCanvas()
        {
            var viewport = _scroll.contentViewport.layout.width;
            if (float.IsNaN(viewport))
                return;
            _canvas.style.marginLeft = Mathf.Max(0f, (viewport - _canvasWidth) / 2f);
        }

        void OnViewportResized(GeometryChangedEvent evt)
        {
            if (_laidOut)
                CenterCanvas();
        }

        static bool HasSize(VisualElement element) =>
            !float.IsNaN(element.layout.width) && !float.IsNaN(element.layout.height)
            && element.layout.width > 0f && element.layout.height > 0f;

        static FlowSize SizeOf(VisualElement element, FlowSize fallback) =>
            HasSize(element) ? new FlowSize(element.layout.width, element.layout.height) : fallback;

        static VisualElement MakeBox(MapNode node)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("flow-node");
            box.AddToClassList(node.IsAnswer ? "flow-node--answer" : TypeClass(node.Type));
            box.EnableInClassList("flow-node--question", node.IsQuestion);
            box.EnableInClassList("flow-node--added", node.Added);
            box.EnableInClassList("flow-node--inferred", node.Inferred && !node.IsQuestion && !node.Added);
            box.EnableInClassList("flow-node--eliminated", node.Eliminated);

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("flow-node__head");
            var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("flow-node__glyph");
            // The backend's pick when the game ships it; else the answer's default or the node type's glyph.
            var iconKey = node.Icon != null && EntityIcons.Load(node.Icon) != null ? node.Icon : null;
            EntityIcons.Show(glyph, iconKey ?? (node.IsAnswer ? null : TypeIcon(node.Type)));
            head.Add(glyph);
            head.Add(MakeLabel(Tag(node), "flow-node__tag"));
            box.Add(head);

            box.Add(MakeLabel(node.Label, "flow-node__label"));

            if (node.Cast.Count > 0)
            {
                var cast = new VisualElement { pickingMode = PickingMode.Ignore };
                cast.AddToClassList("flow-node__cast");
                new EntityStripView(cast, compact: true, maxShown: MaxNodeCast).Bind(node.Cast);
                box.Add(cast);
            }
            return box;
        }

        static Label MakeLabel(string text, string className)
        {
            // Exam text can contain literal tags such as <example>.
            var label = new Label(text) { enableRichText = false, pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        static string Tag(MapNode node)
        {
            if (node.AnswerIndex is int index)
                return $"ANSWER {(char)('A' + index)}";
            if (node.IsQuestion)
                return "THE QUESTION";
            if (node.Added)
                return "NEW STEP";
            return node.Type switch
            {
                FlowNodeType.Start => "START",
                FlowNodeType.Decision => "DECISION",
                FlowNodeType.Input => "INPUT",
                FlowNodeType.Output => "OUTPUT",
                FlowNodeType.HumanReview => "HUMAN REVIEW",
                FlowNodeType.End => "END",
                _ => "STEP",
            };
        }

        static string TypeClass(FlowNodeType type) => type switch
        {
            FlowNodeType.Start => "flow-node--start",
            FlowNodeType.Decision => "flow-node--decision",
            FlowNodeType.Input => "flow-node--input",
            FlowNodeType.Output => "flow-node--output",
            FlowNodeType.HumanReview => "flow-node--human-review",
            FlowNodeType.End => "flow-node--end",
            _ => "flow-node--process",
        };

        /// <summary>
        /// The glyph for what kind of step a node is, for a node the backend sent no (shipped) icon key for.
        /// </summary>
        static string TypeIcon(FlowNodeType type) => type switch
        {
            FlowNodeType.Start => "run",
            FlowNodeType.Decision => "split",
            FlowNodeType.Input => "input",
            FlowNodeType.Output => "result",
            FlowNodeType.HumanReview => "human-reviewer",
            FlowNodeType.End => "success",
            _ => "steps",
        };

        void OnOverlayClicked(ClickEvent evt)
        {
            // Only a click on the dimmed backdrop itself, not on the panel.
            if (evt.target == _overlay)
                CloseRequested?.Invoke();
        }

        void OnCloseClicked(ClickEvent evt) => CloseRequested?.Invoke();

        void OnQuestionTabClicked(ClickEvent evt) => KindRequested?.Invoke(FlowMapKind.Question);

        void OnResolutionTabClicked(ClickEvent evt) => KindRequested?.Invoke(FlowMapKind.Resolution);
    }
}
