using System.Text.Json;
using smart_pet_care_api.Infrastructure.Classifier.Contracts;
using smart_pet_care_api.Modules.ChatModule.DTOs;

namespace smart_pet_care_api.Modules.ChatModule.Tests;

public sealed class SessionMessageResponseDtoTests
{
    [Fact]
    public void FromClassifier_FlattensPredictionForFrontend()
    {
        var response = SessionMessageResponseDto.FromClassifier(
            CreateResponse(
                ClassifierChatMode.Health,
                ClassifierUrgency.ConsultSoon));

        Assert.Equal("answer", response.Answer);
        Assert.Equal(ClassifierUrgency.ConsultSoon, response.Urgency);
        Assert.Equal(["keep fresh water available"], response.HomeAdvice);
        Assert.Equal("disclaimer", response.Disclaimer);
        Assert.False(response.UrgentContactEmergencyVet);

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            "CONSULT_SOON",
            document.RootElement.GetProperty("urgency").GetString());
        Assert.False(document.RootElement.TryGetProperty("prediction", out _));
    }

    [Fact]
    public void FromClassifier_GeneralModeReturnsNoUrgencyOrHomeAdvice()
    {
        var response = SessionMessageResponseDto.FromClassifier(
            new ClassifierChatResponse
            {
                Mode = ClassifierChatMode.General,
                Answer = "general answer",
                SymptomSummary = string.Empty,
                Disclaimer = "disclaimer"
            });

        Assert.Null(response.Urgency);
        Assert.Empty(response.HomeAdvice);
        Assert.False(response.UrgentContactEmergencyVet);
    }

    [Fact]
    public void FromClassifier_EmergencyModeSetsEmergencyFlag()
    {
        var response = SessionMessageResponseDto.FromClassifier(
            CreateResponse(
                ClassifierChatMode.Emergency,
                ClassifierUrgency.Emergency));

        Assert.Equal(ClassifierUrgency.Emergency, response.Urgency);
        Assert.True(response.UrgentContactEmergencyVet);
    }

    private static ClassifierChatResponse CreateResponse(
        ClassifierChatMode mode,
        ClassifierUrgency urgency)
    {
        return new ClassifierChatResponse
        {
            Mode = mode,
            Answer = "answer",
            SymptomSummary = "summary",
            Disclaimer = "disclaimer",
            Prediction = new ClassifierChatPrediction
            {
                PredictedCondition = "condition",
                Confidence = 0.8,
                Urgency = urgency,
                Specialist = "veterinarian",
                DiseaseCategory = "category",
                HomeAdvice = ["keep fresh water available"]
            }
        };
    }
}
