namespace Aives.Domain.Entities;

public class QuestionRubricCriterion
{
    public int CriterionId { get; set; }
    public int QuestionId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; } = null!;
    public decimal MaxScore { get; set; }
    public virtual QuestionBank Question { get; set; } = null!;
}
