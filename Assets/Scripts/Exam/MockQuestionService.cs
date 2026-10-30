using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExamCards.Exam
{
    /// <summary>
    /// Stand-in for the backend API: returns a fixed set of questions after a short fake latency.
    /// Image fields hold local image keys (e.g. "sea_otter") instead of URLs.
    /// </summary>
    public sealed class MockQuestionService : IQuestionService
    {
        readonly int _latencyMs;

        public MockQuestionService(int latencyMs = 300)
        {
            _latencyMs = latencyMs;
        }

        public async Task<IReadOnlyList<Question>> GetQuestionsAsync(CancellationToken cancellationToken = default)
        {
            if (_latencyMs > 0)
                await Task.Delay(_latencyMs, cancellationToken);
            return BuildQuestions();
        }

        static IReadOnlyList<Question> BuildQuestions()
        {
            return new[]
            {
                new Question
                {
                    Id = "q1",
                    Category = "Wildlife & Ocean Habitats",
                    XpReward = 250,
                    ChallengeLabel = "Card Identification Challenge",
                    Prompt = "Which remarkable creature is known to sleep while floating on water, often holding paws to prevent drifting apart?",
                    Hint = "Choose the matching specimen card below or press hotkeys [1-4] to lock in your streak!",
                    ClueImage = "sea_otter",
                    ClueText = "Habitat Clue: Kelp Canopy",
                    Options = new[] { SeaOtter(), RingedSeal(), Capybara(), GentooPenguin() },
                    CorrectIndex = 0,
                },
                new Question
                {
                    Id = "q2",
                    Category = "Polar Habitats",
                    XpReward = 200,
                    ChallengeLabel = "Card Identification Challenge",
                    Prompt = "Which animal digs snow lairs directly above breathing holes in the sea ice to shelter its pups?",
                    Hint = "Think about who needs to breathe air but lives under the ice.",
                    ClueImage = "ringed_seal",
                    ClueText = "Habitat Clue: Arctic Ice Floe",
                    Options = new[] { GentooPenguin(), Capybara(), RingedSeal(), SeaOtter() },
                    CorrectIndex = 2,
                },
                new Question
                {
                    Id = "q3",
                    Category = "Freshwater Habitats",
                    XpReward = 150,
                    ChallengeLabel = "Card Identification Challenge",
                    Prompt = "Which of these animals is the largest living rodent in the world?",
                    Hint = "It spends its days grazing near rivers and lakes in South America.",
                    ClueImage = "capybara",
                    ClueText = "Habitat Clue: Riverbank",
                    Options = new[] { RingedSeal(), Capybara(), SeaOtter(), GentooPenguin() },
                    CorrectIndex = 1,
                },
                new Question
                {
                    Id = "q4",
                    Category = "Seabirds",
                    XpReward = 250,
                    ChallengeLabel = "Card Identification Challenge",
                    Prompt = "Which bird is the fastest underwater swimmer, reaching speeds of around 36 km/h?",
                    Hint = "It cannot fly, but it is built like a torpedo.",
                    ClueImage = "gentoo_penguin",
                    ClueText = "Habitat Clue: Rocky Shore",
                    Options = new[] { Capybara(), SeaOtter(), RingedSeal(), GentooPenguin() },
                    CorrectIndex = 3,
                },
                new Question
                {
                    Id = "q5",
                    Category = "Marine Mammals",
                    XpReward = 300,
                    ChallengeLabel = "Classification Challenge",
                    Prompt = "Which of these animals belongs to the pinnipeds, the group of fin-footed marine mammals?",
                    Hint = "Pinnipeds include seals, sea lions and walruses.",
                    ClueImage = "ringed_seal",
                    ClueText = "Habitat Clue: Coastal Ice",
                    Options = new[] { SeaOtter(), RingedSeal(), GentooPenguin(), Capybara() },
                    CorrectIndex = 1,
                },
            };
        }

        static AnswerOption SeaOtter() => new AnswerOption
        {
            Title = "Sea Otter",
            Subtitle = "Enhydra lutris",
            Description = "Wraps in giant kelp and interlocks paws with family rafts during sleep to keep from drifting.",
            PrimaryTag = "Marine Mammal",
            SecondaryTag = "Pacific Coast",
            Traits = new[] { "Carnivore", "Social Rafts", "Kelp Bed" },
            Image = "sea_otter",
            Tier = "Tier III",
        };

        static AnswerOption RingedSeal() => new AnswerOption
        {
            Title = "Ringed Seal",
            Subtitle = "Pusa hispida",
            Description = "Builds snow shelters directly over breathing holes in polar ice floes to protect pups.",
            PrimaryTag = "Arctica",
            SecondaryTag = "Marine Ice",
            Traits = new[] { "Pinniped", "Ice Floe", "Solitary" },
            Image = "ringed_seal",
            Tier = "Tier II",
        };

        static AnswerOption Capybara() => new AnswerOption
        {
            Title = "Capybara",
            Subtitle = "Hydrochoerus",
            Description = "The world's largest semi-aquatic rodent that gathers peacefully along warm riparian edges.",
            PrimaryTag = "Aquatic",
            SecondaryTag = "Freshwater",
            Traits = new[] { "Rodentia", "Herbivore", "Wetlands" },
            Image = "capybara",
            Tier = "Tier I",
        };

        static AnswerOption GentooPenguin() => new AnswerOption
        {
            Title = "Gentoo Penguin",
            Subtitle = "Pygoscelis papua",
            Description = "The swiftest swimming seabird on earth, resting in brief upright micro-naps around rocky shores.",
            PrimaryTag = "Oceanic",
            SecondaryTag = "Subantarctic",
            Traits = new[] { "Aves", "Colonial", "Swimmer" },
            Image = "gentoo_penguin",
            Tier = "Tier II",
        };
    }
}
