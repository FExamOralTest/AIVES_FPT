namespace Aives.Application.Questions.DTOs;

public record QuestionDto(
    int    QuestionId,
    int    CourseId,
    string QuestionText,
    int    Level,
    float? MaxScore,
    bool?  IsFollowUpAllowed
);

public record GenerateExamRequest(
    int CourseId,
    int EasyCount,
    int MediumCount,
    int HardCount
);

public record GenerateExamResponse(
    int               CourseId,
    string            CourseName,
    int               TotalQuestions,
    int               EasyCount,
    int               MediumCount,
    int               HardCount,
    List<QuestionDto> Questions
);
