using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExamCards.Exam
{
    /// <summary>
    /// Picks a Lucide icon (https://lucide.dev, by its file name, e.g. "square-terminal") for a
    /// <see cref="VisualEntity"/>. The icons ship with the game, so every name it returns is in
    /// <see cref="KnownIcons"/>. First match wins:
    /// <list type="number">
    /// <item>the whole normalized name, then an alias, in <see cref="ExactNames"/>;</item>
    /// <item>a word of the name, then of an alias, in <see cref="Keywords"/> (actions read from the first word,
    /// which is usually the verb; actors and objects from the last, usually the head noun);</item>
    /// <item>the icon for the entity's kind.</item>
    /// </list>
    /// To add an icon: put its SVG in <c>Assets/Resources/EntityIcons/</c>, use it here, and list it in KnownIcons.
    /// </summary>
    public static class EntityIconResolver
    {
        public const string ActorIcon = "user";
        public const string ObjectIcon = "box";
        public const string ActionIcon = "zap";

        /// <summary>Normalized whole names (see <see cref="Normalize"/>) of things the exam talks about often.</summary>
        static readonly Dictionary<string, string> ExactNames = new Dictionary<string, string>
        {
            ["claude code"] = "square-terminal",
            ["claude api"] = "plug",
            ["messages api"] = "plug",
            ["claude agent sdk"] = "boxes",
            ["agent sdk"] = "boxes",
            ["claude"] = "sparkles",
            ["llm"] = "sparkles",
            ["mcp"] = "server",
            ["mcp server"] = "server",
            ["mcp client"] = "plug",
            ["subagent"] = "bot",
            ["main agent"] = "brain-circuit",
            ["orchestrator"] = "network",
            ["coordinator"] = "network",
            ["plan mode"] = "map",
            ["accept edits"] = "pencil",
            ["stop reason"] = "ban",
            ["tool use"] = "wrench",
            ["tool result"] = "clipboard-list",
            ["system prompt"] = "scroll-text",
            ["context window"] = "layers",
            ["cache control"] = "hard-drive",
            ["prompt caching"] = "hard-drive",
            ["api key"] = "key-round",
            ["claude md"] = "file-text",
            ["settings json"] = "file-braces",
            ["git diff"] = "git-compare",
            ["git revert"] = "undo-2",
            ["git reset hard"] = "rotate-ccw",
            ["force push"] = "upload",
            ["pull request"] = "git-pull-request",
            ["escalate to human"] = "headset",
            ["human"] = "user-round",
            ["cors"] = "shield-check",
            ["dns"] = "globe",
            ["read replica"] = "copy",
        };

        /// <summary>Single normalized words; a trailing "s" is also tried without it.</summary>
        static readonly Dictionary<string, string> Keywords = BuildKeywords(new (string Icon, string Words)[]
        {
            ("bot", "agent subagent assistant"),
            ("sparkles", "model claude llm ai"),
            ("user", "user customer client player student"),
            ("user-cog", "engineer developer admin administrator operator"),
            ("user-round", "human person account profile reviewer"),
            ("users", "team people group"),
            ("network", "coordinator orchestrator network"),
            ("server", "server mcp backend host"),
            ("database", "database db sql table store storage"),
            ("file-text", "file md markdown document doc docs readme"),
            ("file-braces", "json yaml yml toml"),
            ("folder", "folder directory repo repository project workspace"),
            ("wrench", "tool tooling"),
            ("plug", "api endpoint integration interface"),
            ("boxes", "sdk library package batch"),
            ("key-round", "key secret credential"),
            ("coins", "token cost price budget"),
            ("lock", "permission auth authorization authentication login access"),
            ("shield", "security safety sandbox"),
            ("shield-check", "guardrail guard policy"),
            ("message-square", "prompt message instruction response reply"),
            ("messages-square", "chat conversation thread"),
            ("mail", "email mail"),
            ("bell", "notification alert"),
            ("git-branch", "git branch"),
            ("git-commit-horizontal", "commit"),
            ("git-merge", "merge"),
            ("git-compare", "diff compare"),
            ("git-pull-request", "pr"),
            ("map", "plan roadmap"),
            ("list-todo", "task todo todos"),
            ("list-checks", "checklist list criteria"),
            ("list-ordered", "step steps index sequence"),
            ("settings", "setting settings config configuration option flag mode"),
            ("sliders-horizontal", "parameter param temperature"),
            ("variable", "variable env environment"),
            ("square-function", "function method"),
            ("braces", "schema structured"),
            ("code", "code script snippet"),
            ("square-terminal", "terminal cli shell bash command console"),
            ("flask-conical", "test tests eval evaluation experiment"),
            ("bug", "bug error exception"),
            ("hard-drive", "cache disk"),
            ("memory-stick", "memory"),
            ("layers", "context window layer"),
            ("workflow", "workflow pipeline chain"),
            ("webhook", "hook webhook event"),
            ("package", "order product item shipment delivery"),
            ("receipt", "refund invoice receipt"),
            ("credit-card", "payment billing charge card"),
            ("shopping-cart", "cart purchase checkout"),
            ("arrow-right-left", "transfer transaction exchange"),
            ("calculator", "balance calculation total"),
            ("clock", "time timeout latency session schedule deadline"),
            ("hourglass", "wait delay"),
            ("timer", "timer"),
            ("gauge", "rate limit quota status performance"),
            ("search", "search find lookup query"),
            ("scan-search", "scan inspect analyze analysis"),
            ("eye", "read view review observe"),
            ("pencil", "write edit update modify change"),
            ("trash", "delete remove drop"),
            ("repeat", "retry repeat loop iterate"),
            ("undo-2", "undo revert rollback"),
            ("rotate-ccw", "reset restart"),
            ("upload", "upload push deploy publish"),
            ("download", "download fetch pull install"),
            ("globe", "web browser page website url internet http"),
            ("link", "link connection"),
            ("cloud", "cloud"),
            ("cpu", "cpu compute gpu"),
            ("image", "image screenshot picture"),
            ("chart-bar", "chart report metric metrics dashboard"),
            ("scroll-text", "log logs transcript history"),
            ("send", "send request call invoke"),
            ("badge-check", "approve approval validate validation verify"),
            ("headset", "escalate escalation support"),
            ("route", "route routing"),
            ("split", "split parallel fork"),
            ("funnel", "filter"),
            ("lightbulb", "idea hint tip"),
            ("wand-sparkles", "generate generation create"),
            ("puzzle", "plugin extension skill"),
            ("text-cursor-input", "input field form"),
            ("gavel", "rule"),
            ("handshake", "handoff delegate delegation"),
            ("play", "run execute start launch"),
            ("ban", "stop cancel block deny"),
            ("circle-check", "success pass done"),
            ("circle-x", "fail failure reject"),
            ("monitor", "monitor screen display"),
            ("notebook-pen", "note notes notebook"),
            ("book-open", "book lecture guide documentation"),
            ("building", "company organization enterprise"),
            ("clipboard-list", "result output"),
            ("copy", "copy replica duplicate"),
        });

        /// <summary>Every icon this class can return; each must ship as <c>Resources/EntityIcons/&lt;name&gt;.svg</c>.</summary>
        public static readonly IReadOnlyCollection<string> KnownIcons = new SortedSet<string>(
            ExactNames.Values.Concat(Keywords.Values).Append(ActorIcon).Append(ObjectIcon).Append(ActionIcon),
            StringComparer.Ordinal);

        public static string Resolve(VisualEntity entity)
        {
            if (entity == null)
                return ObjectIcon;

            var aliases = entity.Aliases ?? Array.Empty<string>();
            var texts = new[] { entity.Name }.Concat(aliases).Select(Normalize).Where(text => text.Length > 0).ToArray();

            foreach (var text in texts)
            {
                if (ExactNames.TryGetValue(text, out var icon))
                    return icon;
            }

            foreach (var text in texts)
            {
                var words = text.Split(' ');
                if (entity.Kind != EntityKind.Action)
                    Array.Reverse(words);
                foreach (var word in words)
                {
                    if (TryKeyword(word, out var icon))
                        return icon;
                }
            }

            return KindIcon(entity.Kind);
        }

        public static string KindIcon(EntityKind kind) => kind switch
        {
            EntityKind.Actor => ActorIcon,
            EntityKind.Action => ActionIcon,
            _ => ObjectIcon,
        };

        /// <summary>
        /// Lower-case words separated by single spaces: camelCase is split ("acceptEdits" → "accept edits"),
        /// and anything that isn't a letter or digit separates words ("`stop_reason`" → "stop reason").
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length + 4);
            var previous = '\0';
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && char.IsLower(previous))
                        builder.Append(' ');
                    builder.Append(char.ToLowerInvariant(c));
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != ' ')
                {
                    builder.Append(' ');
                }
                previous = c;
            }
            return builder.ToString().TrimEnd();
        }

        static bool TryKeyword(string word, out string icon)
        {
            if (Keywords.TryGetValue(word, out icon))
                return true;
            return word.Length > 3 && word.EndsWith("s", StringComparison.Ordinal)
                && Keywords.TryGetValue(word.Substring(0, word.Length - 1), out icon);
        }

        static Dictionary<string, string> BuildKeywords((string Icon, string Words)[] groups)
        {
            var keywords = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (icon, words) in groups)
            {
                foreach (var word in words.Split(' '))
                {
                    if (!keywords.TryAdd(word, icon))
                        throw new InvalidOperationException($"Keyword \"{word}\" is mapped twice.");
                }
            }
            return keywords;
        }
    }
}
