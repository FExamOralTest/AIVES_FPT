using System;
using System.Collections.Generic;

using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class MarkdownDocument
{
    public int DocumentId { get; set; }

    public int CourseId { get; set; }

    public int UploadedBy { get; set; }

    public string FileName { get; set; } = null!;

    public DocType DocType { get; set; }

    public string Content { get; set; } = null!;

    public DateTime? UploadedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();

    public virtual User UploadedByNavigation { get; set; } = null!;
}
