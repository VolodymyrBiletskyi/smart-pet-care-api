using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Models;
using smart_pet_care_api.Modules.NutritionModule.DTOs.Responses;
using static smart_pet_care_api.Models.Enums;

namespace smart_pet_care_api.Modules.NutritionModule.Mapper
{
    public static class NutritionAnalysisMapper
    {
        public static NutritionAnalysisResponseDto ToDto(this NutritionAnalysis analysis) => new()
        {
            Id = analysis.Id,
            PetId = analysis.PetId,
            Date = analysis.Date,
            UtcOffsetMinutes = analysis.UtcOffsetMinutes,
            Grade = analysis.Grade,
            Score = analysis.Score,
            Summary = analysis.Summary,
            Advice = [.. analysis.Advice],
            Disclaimer = analysis.Disclaimer,
            MealCount = analysis.MealCount,
            TotalCalories = analysis.TotalCalories,
            CreatedAt = analysis.CreatedAt
        };

        public static NutritionAnalysis ToEntity(
            ClassifierNutritionResponse response,
            DailyNutritionSummaryResponseDto summary) => new()
            {
                PetId = summary.PetId,
                Date = summary.Date,
                UtcOffsetMinutes = summary.UtcOffsetMinutes,
                Grade = ToGrade(response.Grade),
                Score = response.Score,
                Summary = response.Summary,
                Advice = [.. response.Advice],
                Disclaimer = response.Disclaimer,
                MealCount = summary.MealCount,
                TotalCalories = summary.TotalCalories,
                CreatedAt = DateTime.UtcNow
            };

        /// <summary>
        /// Builds an analysis from the two classifier routes that exist today:
        /// <c>wellness</c> supplies the graded figures and <c>chat</c> the prose.
        /// Used until the classifier implements the dedicated
        /// <c>nutrition-analysis</c> route, which returns all of it in one call.
        /// </summary>
        public static NutritionAnalysis ToEntity(
            ClassifierWellnessResponse wellness,
            ClassifierChatResponse chat,
            DailyNutritionSummaryResponseDto summary)
        {
            var score = ToScore(wellness.Breakdown.Diet.Evidence?.CalorieRatio);
            var (chatSummary, advice) = SplitAnswer(chat.Answer);

            return new NutritionAnalysis
            {
                PetId = summary.PetId,
                Date = summary.Date,
                UtcOffsetMinutes = summary.UtcOffsetMinutes,
                Grade = ToGrade(score),
                Score = score,
                Summary = chatSummary,
                Advice = advice,
                Disclaimer = chat.Disclaimer,
                MealCount = summary.MealCount,
                TotalCalories = summary.TotalCalories,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Grades how close the day's calories landed to the target the
        /// classifier derived from the pet's body data. A ratio of 1.0 scores
        /// 100 and falls off linearly, reaching 0 at double or zero intake.
        /// </summary>
        /// <remarks>
        /// The wellness diet dimension is deliberately not used as the score.
        /// Its 0-20 points also reward how many days of history were logged, so
        /// a single-day analysis tops out around 15/20 even when the pet was fed
        /// perfectly — a flawless day would grade C.
        /// </remarks>
        public static int ToScore(double? calorieRatio)
        {
            if (calorieRatio is not { } ratio || !double.IsFinite(ratio))
                return 0;

            var accuracy = 1.0 - Math.Abs(ratio - 1.0);
            return (int)Math.Round(Math.Clamp(accuracy, 0.0, 1.0) * 100, MidpointRounding.AwayFromZero);
        }

        /// <summary>Thresholds match the ones documented for the dedicated route.</summary>
        public static NutritionGrade ToGrade(int score) => score switch
        {
            >= 90 => NutritionGrade.A,
            >= 75 => NutritionGrade.B,
            >= 60 => NutritionGrade.C,
            >= 40 => NutritionGrade.D,
            _ => NutritionGrade.F
        };

        /// <summary>
        /// Splits a chat answer into its lead-in sentences and its bullet list.
        /// The chat route returns user-facing prose, so the advice list has to be
        /// recovered from the text; an answer with no bullets becomes the summary
        /// with no advice rather than being force-split.
        /// </summary>
        public static (string Summary, List<string> Advice) SplitAnswer(string answer)
        {
            var summaryLines = new List<string>();
            var advice = new List<string>();

            foreach (var raw in (answer ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var bullet = StripBullet(raw);
                if (bullet is null)
                    summaryLines.Add(raw);
                else if (bullet.Length > 0)
                    advice.Add(bullet);
            }

            var summary = string.Join(' ', summaryLines).Trim();

            // An answer that is nothing but bullets still needs a summary, and a
            // trailing "consider these tips:" lead-in reads badly on its own.
            if (summary.Length == 0)
                summary = advice.Count > 0 ? advice[0] : (answer ?? string.Empty).Trim();

            return (summary, advice);
        }

        /// <summary>Returns the text after a list marker, or null if the line is not a list item.</summary>
        private static string? StripBullet(string line)
        {
            if (line.Length < 2)
                return null;

            var marker = line[0];
            if (marker is '-' or '*' or '•' or '–' or '—')
                return line[1..].Trim().Trim('*').Trim();

            // "1. text" or "2) text"
            var digits = 0;
            while (digits < line.Length && char.IsAsciiDigit(line[digits]))
                digits++;

            if (digits == 0 || digits + 1 >= line.Length || line[digits] is not ('.' or ')'))
                return null;

            return line[(digits + 1)..].Trim().Trim('*').Trim();
        }

        public static NutritionGrade ToGrade(ClassifierNutritionGrade grade) => grade switch
        {
            ClassifierNutritionGrade.A => NutritionGrade.A,
            ClassifierNutritionGrade.B => NutritionGrade.B,
            ClassifierNutritionGrade.C => NutritionGrade.C,
            ClassifierNutritionGrade.D => NutritionGrade.D,
            _ => NutritionGrade.F
        };

        public static ClassifierPortionUnit ToClassifierPortionUnit(PortionUnit unit) => unit switch
        {
            PortionUnit.Gram => ClassifierPortionUnit.Gram,
            PortionUnit.Milliliter => ClassifierPortionUnit.Milliliter,
            PortionUnit.Cup => ClassifierPortionUnit.Cup,
            _ => ClassifierPortionUnit.Piece
        };
    }
}
