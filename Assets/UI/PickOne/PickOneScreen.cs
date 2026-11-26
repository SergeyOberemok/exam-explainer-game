using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExamCards.Exam;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ExamCards.UI
{
    /// <summary>
    /// Drives the PickOne screen: starts a round from the question service, shows its questions one by one,
    /// and handles card picks (click or keys 1-4), Reveal (click or R), 50/50 (click or F), Next, Play Again
    /// and Retry (click, Enter or Space) and the final score. When the loaded questions are answered, Next loads
    /// more for the same round (none it has had), so play goes on until the lives or the questions run out.
    /// Answers and 50/50 are checked by the round,
    /// so they run asynchronously; a failed request leaves the question as it was and shows a message.
    /// The search modal (header button or /) finds questions by text; choosing one adds it to the round and
    /// plays it right away.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PickOneScreen : MonoBehaviour
    {
        const string HiddenClass = "is-hidden";
        const string ActiveTabClass = "panel-tab--active";
        /// <summary>Icons shown for the question; the rest would crowd out the text.</summary>
        const int MaxQuestionEntities = 8;
        const string ResultsHiddenClass = "results--hidden";
        const string LoadingText = "Loading question…";
        const string LoadFailedText = "Could not load questions.";
        const string NoQuestionsText = "No questions are available.";
        const string AnswerFailedText = "Could not check that answer. Pick again to retry.";
        const string FiftyFiftyFailedText = "50/50 is unavailable right now. Try again.";
        const string SearchHintText = "Type at least 2 characters to search all questions.";
        const string SearchingText = "Searching…";
        const string SearchFailedText = "Search failed. Check the connection and try again.";
        const string SearchUnavailableText = "Search is unavailable.";
        const string AddingText = "Adding the question to this round…";
        const string AddFailedText = "Could not add that question. Choose it again to retry.";
        const string AlreadyAnsweredText = "You have already answered that question in this round.";
        const string NoRoundText = "Wait for the round to load, then choose a question.";
        const string ExamOverText = "No lives left, so no more questions can be added to this round.";
        const string LoadingMoreText = "Loading more questions…";
        const string LoadMoreFailedText = "Could not load more questions. Press Next to retry.";

        [SerializeField] VisualTreeAsset _answerCardTemplate;

        // One key per answer, up to Question.MaxAnswers.
        static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4 };
        static readonly Key[] NextKeys = { Key.Enter, Key.NumpadEnter, Key.Space };

        readonly List<AnswerCardView> _cards = new();
        IQuestionService _questionService;
        ExamSession _session;

        // Cancels the round load and any answer, 50/50 or play-now request; replaced for every new round.
        CancellationTokenSource _roundCancellation;
        bool _loadFailed;

        // Cancels the running search; replaced for every search.
        CancellationTokenSource _searchCancellation;
        IReadOnlyList<Question> _searchResults = Array.Empty<Question>();
        SearchPanelView _search;
        VisualElement _searchButton;

        Label _categoryLabel;
        VisualElement _chapterChip;
        VisualElement _devIdChip;
        Label _devIdLabel;
        Label _chapterLabel;
        Label _storyBeat;
        Label _storyOutcome;
        Label _resolutionRule;
        QuestionPartsView _questionParts;
        VisualElement _difficultyChip;
        Label _difficultyLabel;
        VisualElement _objectiveChip;
        Label _objectiveLabel;
        VisualElement _panelTabs;
        VisualElement _debriefTab;
        VisualElement _questionTab;
        VisualElement _misconceptionSection;
        Label _misconceptionText;
        Label _requirementsTitle;
        RequirementsView _requirements;

        /// <summary>The player asked to see the scenario paragraph of the current question again.</summary>
        bool _showScenario;

        /// <summary>
        /// The answered question's panel shows the question instead of its debrief; reset per question, so an
        /// answer opens the debrief.
        /// </summary>
        bool _showQuestionAfterAnswer;
        VisualElement _simplifiedBadge;
        VisualElement _questionIcon;
        EntityStripView _questionEntities;
        ScrollView _questionScroll;
        Label _questionText;
        TypewriterLabel _questionTypewriter;
        VisualElement _explanationSection;
        Label _explanationText;
        Label _progressLabel;
        Label _progressPercent;
        VisualElement _progressFill;
        VisualElement _cardsRow;
        VisualElement _fiftyFiftyButton;
        Label _fiftyFiftyCount;
        Label _statusText;
        Button _revealButton;
        Button _nextButton;
        Button _retryButton;
        VisualElement _resultsOverlay;
        Label _resultsScore;
        Button _playAgainButton;
        VisualElement _exitButton;
        List<VisualElement> _hearts;

        static void OnExitClicked(ClickEvent evt)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _categoryLabel = root.Q<Label>("categoryLabel");
            _chapterChip = root.Q("chapterChip");
            _chapterLabel = root.Q<Label>("chapterLabel");
            _storyBeat = root.Q<Label>("storyBeat");
            _storyOutcome = root.Q<Label>("storyOutcome");
            _resolutionRule = root.Q<Label>("resolutionRule");
            _questionParts = new QuestionPartsView(root.Q("questionParts"));
            _difficultyChip = root.Q("difficultyChip");
            _difficultyLabel = root.Q<Label>("difficultyLabel");
            _objectiveChip = root.Q("objectiveChip");
            _objectiveLabel = root.Q<Label>("objectiveLabel");
            _panelTabs = root.Q("panelTabs");
            _debriefTab = root.Q("debriefTab");
            _questionTab = root.Q("questionTab");
            _debriefTab.RegisterCallback<ClickEvent>(OnDebriefTabClicked);
            _questionTab.RegisterCallback<ClickEvent>(OnQuestionTabClicked);
            _misconceptionSection = root.Q("misconceptionSection");
            _misconceptionText = root.Q<Label>("misconceptionText");
            _requirementsTitle = root.Q<Label>("requirementsTitle");
            _requirements = new RequirementsView(root);
            _chapterChip.RegisterCallback<ClickEvent>(OnChapterClicked);
            _devIdChip = root.Q("devIdChip");
            _devIdLabel = root.Q<Label>("devIdLabel");
            _devIdChip.RegisterCallback<ClickEvent>(OnDevIdClicked);
            _simplifiedBadge = root.Q("simplifiedBadge");
            _questionIcon = root.Q("questionIcon");
            _questionEntities = new EntityStripView(root.Q("questionEntities"), compact: false, maxShown: MaxQuestionEntities);
            _questionScroll = root.Q<ScrollView>("questionScroll");
            _questionText = root.Q<Label>("questionText");
            _questionTypewriter = new TypewriterLabel(_questionText);
            _explanationSection = root.Q("explanationSection");
            _explanationText = root.Q<Label>("explanationText");
            _progressLabel = root.Q<Label>("progressLabel");
            _progressPercent = root.Q<Label>("progressPercent");
            _progressFill = root.Q("progressFill");
            _cardsRow = root.Q("cardsRow");
            _fiftyFiftyButton = root.Q("fiftyFiftyButton");
            _fiftyFiftyCount = root.Q<Label>("fiftyFiftyCount");
            _statusText = root.Q<Label>("statusText");
            _revealButton = root.Q<Button>("revealButton");
            _nextButton = root.Q<Button>("nextButton");
            _retryButton = root.Q<Button>("retryButton");
            _resultsOverlay = root.Q("resultsOverlay");
            _resultsScore = root.Q<Label>("resultsScore");
            _playAgainButton = root.Q<Button>("playAgainButton");

            _exitButton = root.Q("exitButton");
            _searchButton = root.Q("searchButton");
            _hearts = root.Q("hearts").Children().ToList();
            _search = new SearchPanelView(root);

            _exitButton.RegisterCallback<ClickEvent>(OnExitClicked);
            _searchButton.RegisterCallback<ClickEvent>(OnSearchClicked);
            _search.TextChanged += OnSearchTextChanged;
            _search.Chosen += OnSearchResultChosen;
            _search.CloseRequested += CloseSearch;
            _fiftyFiftyButton.RegisterCallback<ClickEvent>(OnFiftyFiftyClicked);
            _revealButton.clicked += OnRevealPressed;
            _nextButton.clicked += OnNextPressed;
            _retryButton.clicked += LoadRound;
            _playAgainButton.clicked += LoadRound;
            _cardsRow.Clear();
            _cards.Clear();

            LoadRound();
        }

        void OnDisable()
        {
            CancelRound();
            CancelSearch();
            _questionTypewriter?.Complete();
            _session = null;
            _exitButton?.UnregisterCallback<ClickEvent>(OnExitClicked);
            _chapterChip?.UnregisterCallback<ClickEvent>(OnChapterClicked);
            _debriefTab?.UnregisterCallback<ClickEvent>(OnDebriefTabClicked);
            _questionTab?.UnregisterCallback<ClickEvent>(OnQuestionTabClicked);
            _devIdChip?.UnregisterCallback<ClickEvent>(OnDevIdClicked);
            _searchButton?.UnregisterCallback<ClickEvent>(OnSearchClicked);
            if (_search != null)
            {
                _search.TextChanged -= OnSearchTextChanged;
                _search.Chosen -= OnSearchResultChosen;
                _search.CloseRequested -= CloseSearch;
                _search.Dispose();
                _search = null;
            }
            _fiftyFiftyButton?.UnregisterCallback<ClickEvent>(OnFiftyFiftyClicked);
            if (_revealButton != null)
                _revealButton.clicked -= OnRevealPressed;
            if (_nextButton != null)
                _nextButton.clicked -= OnNextPressed;
            if (_retryButton != null)
            {
                _retryButton.clicked -= LoadRound;
                _playAgainButton.clicked -= LoadRound;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            // While the modal is open, keys belong to it and to its text field, not to the question.
            if (_search.IsOpen)
            {
                HandleSearchKeys(keyboard);
                return;
            }

            if (keyboard[Key.Slash].wasPressedThisFrame && CanOpenSearch)
            {
                OpenSearch();
                return;
            }

            if (_session == null)
            {
                if (_loadFailed && WasNextKeyPressed(keyboard))
                    LoadRound();
                return;
            }

            if (_session.IsFinished)
            {
                if (WasNextKeyPressed(keyboard))
                    LoadRound();
                return;
            }

            if (keyboard[Key.R].wasPressedThisFrame)
            {
                OnRevealPressed();
                return;
            }

            if (keyboard[Key.F].wasPressedThisFrame)
            {
                OnFiftyFiftyPressed();
                return;
            }

            if (keyboard[Key.Q].wasPressedThisFrame)
            {
                ShowQuestionAfterAnswer(!_showQuestionAfterAnswer);
                return;
            }

            if (WasNextKeyPressed(keyboard))
            {
                OnNextPressed();
                return;
            }

            for (var i = 0; i < DigitKeys.Length && i < _cards.Count; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                {
                    OnCardSelected(i);
                    return;
                }
            }
        }

        static bool WasNextKeyPressed(Keyboard keyboard)
        {
            foreach (var key in NextKeys)
            {
                if (keyboard[key].wasPressedThisFrame)
                    return true;
            }
            return false;
        }

        void CancelRound()
        {
            _roundCancellation?.Cancel();
            _roundCancellation?.Dispose();
            _roundCancellation = null;
        }

        /// <summary>Starts a new round. A round's answers are recorded where it is held, so it can't be replayed.</summary>
        async void LoadRound()
        {
            _session = null;
            _loadFailed = false;
            CloseSearch();
            _searchResults = Array.Empty<Question>();
            _search.Reset(SearchHintText);
            _resultsOverlay.AddToClassList(ResultsHiddenClass);
            _retryButton.AddToClassList(HiddenClass);
            _nextButton.AddToClassList(HiddenClass);
            _explanationSection.AddToClassList(HiddenClass);
            _fiftyFiftyButton.SetEnabled(false);
            _revealButton.SetEnabled(false);
            _cardsRow.Clear();
            _cards.Clear();
            ShowStatus(null);
            _questionTypewriter.ShowImmediately(LoadingText);

            CancelRound();
            _roundCancellation = new CancellationTokenSource();
            var token = _roundCancellation.Token;

            IExamRound round;
            try
            {
                // Created here rather than in OnEnable, so bad settings end in the Retry message too.
                if (_questionService == null)
                {
#if !UNITY_EDITOR
                    // In a player the API assembly always registers itself; mock questions there mean a broken build.
                    if (QuestionServices.Factory == null)
                        Debug.LogError("No question service is registered; the game is showing mock questions.", this);
#endif
                    _questionService = QuestionServices.Create();
                }
                round = await _questionService.StartRoundAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (NoQuestionsException e)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogWarning(e.Message, this);
                ShowLoadFailure(NoQuestionsText);
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogException(e, this);
                ShowLoadFailure(LoadFailedText);
                return;
            }

            if (token.IsCancellationRequested)
                return;

            if (round.Questions.Count == 0)
            {
                Debug.LogWarning("The question service returned no questions.", this);
                ShowLoadFailure(NoQuestionsText);
                return;
            }

            _session = new ExamSession(round);
            ShowCurrentQuestion();
        }

        void ShowLoadFailure(string message)
        {
            _loadFailed = true;
            _questionTypewriter.ShowImmediately(message);
            _retryButton.RemoveFromClassList(HiddenClass);
        }

        void ShowStatus(string message)
        {
            _statusText.text = message ?? string.Empty;
            _statusText.EnableInClassList(HiddenClass, string.IsNullOrEmpty(message));
        }

        void ShowCurrentQuestion()
        {
            var question = _session.Current;
            _categoryLabel.text = question.Section?.ToUpperInvariant();
            _progressLabel.text = $"Question {_session.CurrentIndex + 1} of {_session.Total}";

            ShowStatus(null);
            _showScenario = false;
            _showQuestionAfterAnswer = false;
            EnsureCardCount(question.Original.Answers.Count);
            RenderPrompt(animateQuestion: true);
        }

        /// <summary>
        /// The original text normally shows without its scenario paragraph (the chapter stands for it); the
        /// chapter chip brings the paragraph back for the current question.
        /// </summary>
        // Dev only: shows the question id so it can be looked up in the database. Hidden in release builds.
        void RenderDevId(string id)
        {
            // Debug.isDebugBuild is true in the Editor and development builds.
            var show = Debug.isDebugBuild && !string.IsNullOrEmpty(id);
            if (show)
                _devIdLabel.text = $"ID {id}";
            _devIdChip.EnableInClassList(HiddenClass, !show);
        }

        void OnDevIdClicked(ClickEvent evt)
        {
            var id = _session?.Current?.Id;
            if (!Debug.isDebugBuild || string.IsNullOrEmpty(id))
                return;
            GUIUtility.systemCopyBuffer = id;
            _devIdLabel.text = "COPIED";
            _devIdChip.schedule.Execute(() => _devIdLabel.text = $"ID {_session?.Current?.Id}").StartingIn(1200);
        }

        void OnChapterClicked(ClickEvent evt)
        {
            if (_session?.Current == null || _session.IsFinished)
                return;
            _showScenario = !_showScenario;
            // The text it changes is on the question tab.
            if (_session.IsCurrentAnswered)
                _showQuestionAfterAnswer = true;
            RenderPrompt();
        }

        void OnDebriefTabClicked(ClickEvent evt) => ShowQuestionAfterAnswer(false);

        void OnQuestionTabClicked(ClickEvent evt) => ShowQuestionAfterAnswer(true);

        /// <summary>Switches an answered question's panel between its debrief and the question (Q).</summary>
        void ShowQuestionAfterAnswer(bool show)
        {
            if (_session?.Current == null || _session.IsFinished || !_session.IsCurrentAnswered)
                return;
            _showQuestionAfterAnswer = show;
            RenderPrompt();
        }

        /// <summary>"dev-tools" and "incident" as "DEV TOOLS · INCIDENT".</summary>
        static string ChapterText(Question question)
        {
            var chapter = question.Scenario.Replace('-', ' ').ToUpperInvariant();
            return string.IsNullOrEmpty(question.Stage)
                ? chapter
                : $"{chapter} · {question.Stage.Replace('-', ' ').ToUpperInvariant()}";
        }

        /// <summary>
        /// Shows the current text and state. Used for a new question, an answer, a 50/50 and a reveal.
        /// A new question types its text out; the other redraws leave a running animation alone, except an
        /// answer or a different text, which show the text at once.
        /// </summary>
        void RenderPrompt(bool animateQuestion = false)
        {
            var question = _session.Current;
            var prompt = _session.CurrentPrompt;
            var review = _session.CurrentPromptReview;
            var answered = _session.IsCurrentAnswered;

            var percent = Mathf.RoundToInt(100f * _session.AnsweredCount / _session.Total);
            _progressPercent.text = $"{percent}%";
            _progressFill.style.width = Length.Percent(percent);

            // The simplified text has the scenario woven in; only the original has a stem to show instead, or,
            // when the backend decomposed it, its ask with the scenario's facts as parts under it.
            var fullText = _session.IsCurrentRevealed || _showScenario;
            var ask = fullText ? null : question.Ask;
            var text = fullText ? prompt.Question : ask ?? question.OriginalText;
            // Once answered, the panel shows the debrief or, on the question tab, the question without it.
            var debrief = answered && !_showQuestionAfterAnswer;
            _questionParts.Bind(ask != null && !debrief ? question.Parts : null);
            _questionText.EnableInClassList(HiddenClass, debrief);
            _panelTabs.EnableInClassList(HiddenClass, !answered);
            _debriefTab.EnableInClassList(ActiveTabClass, debrief);
            _questionTab.EnableInClassList(ActiveTabClass, answered && !debrief);
            if (animateQuestion && !answered)
                _questionTypewriter.Type(text);
            else if (answered || !_questionTypewriter.IsTyping || _questionTypewriter.Text != text)
                _questionTypewriter.ShowImmediately(text);
            _explanationText.text = review?.Explanation;

            _chapterLabel.text = ChapterText(question);
            _chapterChip.EnableInClassList(HiddenClass, string.IsNullOrEmpty(question.Scenario));
            RenderDevId(question.Id);
            _storyBeat.text = question.Beat;
            _storyBeat.EnableInClassList(HiddenClass, debrief || string.IsNullOrEmpty(question.Beat));
            _difficultyLabel.text = QuestionLabels.Difficulty(question.Difficulty);
            _difficultyChip.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_difficultyLabel.text));
            // The objective can name the answer ("sessions"), so it is part of the debrief.
            _objectiveLabel.text = answered && question.Objectives.Count > 0 ? QuestionLabels.Objective(question.Objectives[0]) : string.Empty;
            _objectiveChip.EnableInClassList(HiddenClass, string.IsNullOrEmpty(_objectiveLabel.text));
            var outcome = answered ? _session.CurrentReview?.Outcome : null;
            _storyOutcome.text = outcome ?? string.Empty;
            _storyOutcome.EnableInClassList(HiddenClass, string.IsNullOrEmpty(outcome));
            var resolution = answered ? _session.CurrentReview?.Resolution : null;
            _resolutionRule.text = resolution?.Rule ?? string.Empty;
            _resolutionRule.EnableInClassList(HiddenClass, string.IsNullOrEmpty(resolution?.Rule));
            _explanationSection.EnableInClassList(HiddenClass, !debrief);
            _explanationSection.EnableInClassList("explanation--alone", debrief);
            RenderDebrief(answered ? _session.CurrentReview : null, review);
            _simplifiedBadge.EnableInClassList(HiddenClass, !_session.IsCurrentRevealed);

            // The large icons and entity chips say what the question and each answer are about, so they stay
            // the same for both texts (a reveal swaps only the text); the backend sends the same ones with both.
            var entities = question.Entities;
            EntityIcons.Show(_questionIcon, entities.QuestionIcon);
            _questionEntities.Bind(entities.Question);

            for (var i = 0; i < _cards.Count; i++)
            {
                _cards[i].Bind(prompt.Answers[i], review?.Feedbacks[i]);
                _cards[i].BindIcon(entities.IconForAnswer(i));
                // The question's strip already shows the entities every answer shares.
                _cards[i].BindEntities(entities.ForAnswerOnly(i));
                _cards[i].BindResolution(
                    resolution?.ForAnswer(i), resolution?.MetCount(i) ?? 0, resolution?.AnswerRequirements.Count ?? 0);
                _cards[i].SetState(GetCardState(i));
            }

            for (var i = 0; i < _hearts.Count; i++)
                _hearts[i].EnableInClassList("icon--heart-used", i >= _session.Lives);

            _fiftyFiftyCount.text = _session.FiftyFiftyRemaining.ToString();
            _fiftyFiftyButton.SetEnabled(_session.CanEliminate);
            _revealButton.SetEnabled(question.CanReveal);
            _revealButton.text = _session.IsCurrentRevealed ? "Show Original [R]" : "Show Simplified [R]";

            _nextButton.text = _session.IsLastQuestion ? "See Results" : "Next Question";
            _nextButton.EnableInClassList(HiddenClass, !answered);

            ScrollQuestionPanel();
        }

        /// <summary>
        /// The debrief's lists: why the answer that counts was tempting (when wrong), what the answer had to do
        /// with a mark for each requirement it met, and every answer's feedback. A null review clears them.
        /// </summary>
        void RenderDebrief(AnswerReview review, PromptReview promptReview)
        {
            var counted = review == null ? null : _session.AnsweredIndex;
            var wrong = review != null && counted != review.CorrectIndex;
            var misconception = wrong ? review.Misconception : null;
            _misconceptionText.text = misconception ?? string.Empty;
            _misconceptionSection.EnableInClassList(HiddenClass, string.IsNullOrEmpty(misconception));

            var resolution = review?.Resolution;
            var requirements = resolution?.AnswerRequirements;
            var met = counted is int index ? resolution?.RequirementsMetBy(index) : null;
            var metCount = met?.Count(m => m) ?? 0;
            _requirementsTitle.text = requirements == null || met == null || met.Count == 0
                ? "What the answer had to do"
                : $"What the answer had to do · yours meets {metCount} of {requirements.Count}";
            _requirements.BindRequirements(requirements, met);
            _requirements.BindAnswerNotes(review == null ? null : promptReview?.Feedbacks, review?.CorrectIndex ?? -1, counted);
        }

        AnswerCardState GetCardState(int index)
        {
            if (!_session.IsCurrentAnswered)
                return _session.IsEliminated(index) ? AnswerCardState.Eliminated : AnswerCardState.Interactive;
            if (index == _session.CurrentReview.CorrectIndex)
                return AnswerCardState.Correct;
            return index == _session.AnsweredIndex ? AnswerCardState.Wrong : AnswerCardState.Dimmed;
        }

        /// <summary>
        /// Scrolls the panel to its top: the question, or the debrief, which starts with the story's outcome.
        /// Runs on the next panel update, because the new text has to be laid out first.
        /// </summary>
        void ScrollQuestionPanel() =>
            _questionScroll.schedule.Execute(() => _questionScroll.scrollOffset = Vector2.zero);

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
            var session = _session;
            if (session == null || session.IsBusy || session.IsFinished || session.IsCurrentAnswered
                || session.IsEliminated(index))
                return;

            RunRoundRequest(session, token => session.AnswerAsync(index, token), AnswerFailedText);
        }

        void OnFiftyFiftyClicked(ClickEvent evt) => OnFiftyFiftyPressed();

        void OnFiftyFiftyPressed()
        {
            var session = _session;
            if (session == null || !session.CanEliminate)
                return;

            RunRoundRequest(session, token => session.EliminateAsync(token), FiftyFiftyFailedText);
        }

        /// <summary>
        /// Runs an answer or 50/50 request and redraws when it ends. Results for a round that has since been
        /// replaced are dropped; a failure keeps the question as it was and shows <paramref name="failureText"/>.
        /// </summary>
        async void RunRoundRequest(ExamSession session, Func<CancellationToken, Task> request, string failureText)
        {
            // Null after OnDisable; a late input then has no round to act on.
            if (_roundCancellation == null)
                return;
            var token = _roundCancellation.Token;
            ShowStatus(null);

            Task pending;
            try
            {
                pending = request(token);
                RenderPrompt(); // The session is busy now, so 50/50 shows as unavailable.
                await pending;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested || session != _session)
                    return;
                Debug.LogException(e, this);
                ShowStatus(failureText);
                RenderPrompt();
                return;
            }

            if (session == _session)
                RenderPrompt();
        }

        void OnRevealPressed()
        {
            if (_session == null)
                return;
            if (_session.ToggleSimplified())
            {
                // The text it swaps is on the question tab.
                if (_session.IsCurrentAnswered)
                    _showQuestionAfterAnswer = true;
                RenderPrompt();
            }
        }

        void OnNextPressed()
        {
            if (_session == null || _session.IsFinished || !_session.IsCurrentAnswered || _session.IsBusy)
                return;

            if (_session.NeedsMore)
            {
                LoadMoreAndAdvance(_session);
                return;
            }
            Advance();
        }

        void Advance()
        {
            if (_session.MoveNext())
                ShowCurrentQuestion();
            else
                ShowResults();
        }

        /// <summary>
        /// Loads the round's next questions, then moves on: to the first new one, or to the results when the round
        /// has none left. A failure keeps the answered question and shows a message; Next tries again.
        /// </summary>
        async void LoadMoreAndAdvance(ExamSession session)
        {
            if (_roundCancellation == null)
                return;
            var token = _roundCancellation.Token;
            ShowStatus(LoadingMoreText);

            try
            {
                var pending = session.LoadMoreAsync(token);
                RenderPrompt(); // Busy now, so 50/50 and Next stay inert.
                await pending;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested || session != _session)
                    return;
                Debug.LogException(e, this);
                ShowStatus(LoadMoreFailedText);
                RenderPrompt();
                return;
            }

            if (session != _session)
                return;
            ShowStatus(null);
            Advance();
        }

        // ---------- search ----------

        /// <summary>Not over the results screen: a finished exam can't take new questions.</summary>
        bool CanOpenSearch => _session == null || !_session.IsFinished;

        void OnSearchClicked(ClickEvent evt)
        {
            if (CanOpenSearch)
                OpenSearch();
        }

        void OpenSearch()
        {
            // The exam may have moved on since the last search, so refresh the rows' state chips.
            _search.ShowResults(_searchResults, GetSearchResultState);
            _search.Open();
        }

        void CloseSearch()
        {
            CancelSearch();
            _search.Close();
        }

        void CancelSearch()
        {
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
        }

        void HandleSearchKeys(Keyboard keyboard)
        {
            if (keyboard[Key.Escape].wasPressedThisFrame)
                CloseSearch();
            else if (keyboard[Key.UpArrow].wasPressedThisFrame)
                _search.MoveSelection(-1);
            else if (keyboard[Key.DownArrow].wasPressedThisFrame)
                _search.MoveSelection(1);
            else if (keyboard[Key.Enter].wasPressedThisFrame || keyboard[Key.NumpadEnter].wasPressedThisFrame)
                _search.ChooseSelected();
        }

        void OnSearchTextChanged(string text)
        {
            CancelSearch();
            if (text == null)
            {
                ShowSearchResults(Array.Empty<Question>());
                _search.ShowMessage(SearchHintText);
                return;
            }
            RunSearch(text);
        }

        async void RunSearch(string text)
        {
            if (_questionService is not IQuestionSearch search)
            {
                _search.ShowMessage(SearchUnavailableText);
                return;
            }

            _searchCancellation = new CancellationTokenSource();
            var token = _searchCancellation.Token;
            _search.ShowMessage(SearchingText);

            IReadOnlyList<Question> results;
            try
            {
                results = await search.SearchAsync(text, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                    return;
                Debug.LogException(e, this);
                _search.ShowMessage(SearchFailedText);
                return;
            }

            if (token.IsCancellationRequested)
                return;
            ShowSearchResults(results);
            _search.ShowMessage(results.Count == 0
                ? $"No playable questions match \u201c{text}\u201d."
                : $"{results.Count} {(results.Count == 1 ? "question" : "questions")} found. Choose one to play it now.");
        }

        void ShowSearchResults(IReadOnlyList<Question> results)
        {
            _searchResults = results;
            _search.ShowResults(results, GetSearchResultState);
        }

        SearchResultState GetSearchResultState(Question question)
        {
            var index = _session?.IndexOf(question.Id) ?? -1;
            if (index < 0)
                return SearchResultState.New;
            if (index < _session.CurrentIndex || (index == _session.CurrentIndex && _session.IsCurrentAnswered))
                return SearchResultState.Answered;
            return index == _session.CurrentIndex ? SearchResultState.Current : SearchResultState.InRound;
        }

        /// <summary>
        /// Plays the chosen question now: the session adds it to the round if needed and makes it current. A
        /// failure keeps the exam as it was and the modal open, so the player can choose again.
        /// </summary>
        async void OnSearchResultChosen(Question question)
        {
            var session = _session;
            if (session == null)
            {
                _search.ShowMessage(NoRoundText);
                return;
            }
            if (session.IsBusy)
                return;
            if (!session.CanPlayNow)
            {
                _search.ShowMessage(ExamOverText);
                return;
            }

            if (_roundCancellation == null)
                return;
            var token = _roundCancellation.Token;
            _search.ShowMessage(AddingText);

            PlayNowOutcome outcome;
            try
            {
                // The player found it by its exam wording; play it in the game's simplified text.
                outcome = await session.PlayNowAsync(question.Id, reveal: true, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested || session != _session)
                    return;
                Debug.LogException(e, this);
                _search.ShowMessage(AddFailedText);
                return;
            }

            if (session != _session)
                return;
            if (outcome == PlayNowOutcome.AlreadyAnswered)
            {
                _search.ShowMessage(AlreadyAnsweredText);
                return;
            }

            CloseSearch();
            ShowCurrentQuestion();
        }

        void ShowResults()
        {
            var outOfLives = _session.Lives <= 0;
            // Out of answered questions: a round that keeps loading batches has no fixed total.
            _resultsScore.text = $"{(outOfLives ? "Out of lives! " : "")}You scored {_session.Score} / {_session.AnsweredCount}";
            _resultsOverlay.RemoveFromClassList(ResultsHiddenClass);
        }
    }
}
