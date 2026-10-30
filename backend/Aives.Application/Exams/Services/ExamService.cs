using System.ComponentModel.DataAnnotations;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

public class ExamService(
    IExamRepository exams, IQuestionBankRepository questions,
    ICourseRepository courses, IExamUnitOfWork unitOfWork) : IExamService
{
    public Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            Validate(request);
            await EnsureAssignedAsync(request.CourseId, teacherId);
            var items = request.Questions ?? [];
            if (items.Any(q => q is null))
                throw new ArgumentException("Questions must not contain null items.");
            foreach (var item in items)
            {
                Validate(item);
                if (!float.IsFinite(item.QuestionScore))
                    throw new ArgumentException("Question scores must be finite.");
            }

            var isAutoPartition = request.AutoGenerateSlotPapers || (request.GeneratedSubPapers != null && request.GeneratedSubPapers.Count > 0);
            var maxQuestions = isAutoPartition ? Math.Max(request.MaxQuestions, items.Count) : request.MaxQuestions;

            if (items.Count > maxQuestions)
                throw new ArgumentException("Question count exceeds MaxQuestions.");
            var ids = items.Select(q => q.QuestionId).ToList();
            if (ids.Distinct().Count() != ids.Count)
                throw new ArgumentException("Duplicate question IDs are not allowed.");
            var selected = await questions.GetByIdsForCourseAsync(request.CourseId, ids);
            if (selected.Count != ids.Count)
                throw new ArgumentException("Every question must exist and belong to the exam course.");
            if (selected.Any(q => string.IsNullOrWhiteSpace(q.QuestionText) || q.Level is < 1 or > 3
                || !float.IsFinite(q.MaxScore ?? 1) || (q.MaxScore ?? 1) < 0.01f || (q.MaxScore ?? 1) > 100))
                throw new ArgumentException("The selected question bank contains invalid questions.");
            if (selected.Any(q => q.ApprovalStatus != QuestionApprovalStatus.APPROVED))
                throw new QuestionBankConflictException("Only approved questions can be added to an exam.");

            if (request.SlotTemplates != null && request.SlotTemplates.Count > 0)
            {
                ValidateSlotTemplates(request.CourseId, 0, request.SlotTemplates);
            }

            var exam = new Exam
            {
                Title = request.Title.Trim(),
                CourseId = request.CourseId,
                CreatedBy = teacherId,
                ExamCode = await GenerateUniqueExamCodeAsync(),
                DurationMinutes = request.DurationMinutes,
                MaxQuestions = maxQuestions,
                ExamStatus = ExamStatus.DRAFT,
                CreatedAt = DateTime.UtcNow,
                ExamQuestions = items.Select(q => new ExamQuestion
                {
                    QuestionId = q.QuestionId,
                    QuestionScore = q.QuestionScore
                }).ToList()
            };
            var created = await exams.CreateAsync(exam);

            var slotExamsMap = new Dictionary<int, List<int>>();

            // 1. Create sub-papers if GeneratedSubPapers provided (Mode B)
            if (request.GeneratedSubPapers != null && request.GeneratedSubPapers.Count > 0)
            {
                foreach (var subPaperInput in request.GeneratedSubPapers)
                {
                    var subCode = !string.IsNullOrWhiteSpace(subPaperInput.ExamCode)
                        ? subPaperInput.ExamCode.Trim()
                        : $"{created.ExamCode}-S{subPaperInput.SlotIndex}";

                    var subSlot = request.SlotTemplates?.FirstOrDefault(st => st.SlotIndex == subPaperInput.SlotIndex);
                    var subDuration = subSlot?.DurationMinutes ?? request.DurationMinutes;

                    var subExam = new Exam
                    {
                        Title = subPaperInput.Title.Trim(),
                        CourseId = created.CourseId,
                        CreatedBy = teacherId,
                        ExamCode = subCode,
                        DurationMinutes = subDuration,
                        MaxQuestions = subPaperInput.Questions.Count,
                        ExamStatus = ExamStatus.DRAFT,
                        CreatedAt = DateTime.UtcNow,
                        ExamQuestions = subPaperInput.Questions.Select(q => new ExamQuestion
                        {
                            QuestionId = q.QuestionId,
                            QuestionScore = q.QuestionScore
                        }).ToList()
                    };
                    var createdSubExam = await exams.CreateAsync(subExam);
                    if (!slotExamsMap.TryGetValue(subPaperInput.SlotIndex, out var paperIds))
                    {
                        paperIds = new List<int>();
                        slotExamsMap[subPaperInput.SlotIndex] = paperIds;
                    }
                    paperIds.Add(createdSubExam.ExamId);
                }
            }

            // 2. Set slot templates & merge with any manual exam paper ids (Mode A)
            if (request.SlotTemplates != null && request.SlotTemplates.Count > 0)
            {
                ValidateSlotTemplates(request.CourseId, created.ExamId, request.SlotTemplates);
                var slotEntities = request.SlotTemplates.Select(t => new ExamSlotTemplate
                {
                    SlotIndex = t.SlotIndex,
                    SlotName = t.SlotName.Trim(),
                    DurationMinutes = t.DurationMinutes,
                    MaxCapacity = t.MaxCapacity
                }).ToList();

                foreach (var t in request.SlotTemplates)
                {
                    if (t.ExamPaperIds != null && t.ExamPaperIds.Count > 0)
                    {
                        if (!slotExamsMap.TryGetValue(t.SlotIndex, out var pids))
                        {
                            pids = new List<int>();
                            slotExamsMap[t.SlotIndex] = pids;
                        }
                        pids.AddRange(t.ExamPaperIds.Where(id => !pids.Contains(id)));
                    }
                }

                await exams.SetSlotTemplatesAsync(created.ExamId, slotEntities, slotExamsMap);
            }

            var fullExam = await exams.GetByIdWithQuestionsAsync(created.ExamId)
                ?? throw new InvalidOperationException("Exam was created but could not be retrieved.");
            return MapToExamDto(fullExam);
        });

    public async Task<ExamDto> GetExamByIdAsync(int examId, int teacherId)
    {
        var exam = await exams.GetByIdWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");
        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException("You do not have permission to view this exam.");
        await EnsureAssignedAsync(exam.CourseId, teacherId);
        return MapToExamDto(exam);
    }

    public async Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId)
        => (await exams.GetByTeacherIdAsync(teacherId)).Select(MapToExamDto);

    public async Task<SuggestedQuestionsDto> GetSuggestedQuestionsAsync(SuggestQuestionsRequest request, int teacherId)
    {
        Validate(request);
        await EnsureAssignedAsync(request.CourseId, teacherId);
        var selected = await questions.GetSuggestionsAsync(request.CourseId, request.FocusTopic, request.MaxSuggestions);
        return new SuggestedQuestionsDto
        {
            SuggestedQuestions = selected.Select(q => new ExamQuestionDto
            {
                QuestionId = q.QuestionId,
                QuestionText = q.QuestionText,
                QuestionScore = q.MaxScore ?? 1,
                MaxScore = q.MaxScore,
                IsFollowUpAllowed = q.IsFollowUpAllowed,
                Level = q.Level
            }).ToList(),
            SelectionMethod = "keyword",
            Rationale = string.IsNullOrWhiteSpace(request.FocusTopic)
                ? "Selected by maximum score, then newest question."
                : $"Ranked by keyword matches in question text for: {request.FocusTopic.Trim()}."
        };
    }

    public Task<ExamDto> UpdateExamAsync(int examId, UpdateExamRequest request, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var isAutoPartition = request.AutoGenerateSlotPapers || (request.GeneratedSubPapers != null && request.GeneratedSubPapers.Count > 0);
            var maxQuestions = isAutoPartition ? Math.Max(request.MaxQuestions, request.Questions.Count) : request.MaxQuestions;

            Validate(request);
            var exam = await GetEditableExamAsync(examId, teacherId);
            await ValidateQuestionsAsync(exam.CourseId, request.Questions, maxQuestions);
            exam.Title = request.Title.Trim();
            exam.DurationMinutes = request.DurationMinutes;
            exam.MaxQuestions = maxQuestions;
            await ReplaceQuestionsAsync(examId, request.Questions);

            var slotExamsMap = new Dictionary<int, List<int>>();

            // 1. Create sub-papers if GeneratedSubPapers provided (Mode B)
            if (request.GeneratedSubPapers != null && request.GeneratedSubPapers.Count > 0)
            {
                foreach (var subPaperInput in request.GeneratedSubPapers)
                {
                    var subCode = !string.IsNullOrWhiteSpace(subPaperInput.ExamCode)
                        ? subPaperInput.ExamCode.Trim()
                        : $"{exam.ExamCode}-S{subPaperInput.SlotIndex}";

                    var subSlot = request.SlotTemplates?.FirstOrDefault(st => st.SlotIndex == subPaperInput.SlotIndex);
                    var subDuration = subSlot?.DurationMinutes ?? request.DurationMinutes;

                    var subExam = new Exam
                    {
                        Title = subPaperInput.Title.Trim(),
                        CourseId = exam.CourseId,
                        CreatedBy = teacherId,
                        ExamCode = subCode,
                        DurationMinutes = subDuration,
                        MaxQuestions = subPaperInput.Questions.Count,
                        ExamStatus = ExamStatus.DRAFT,
                        CreatedAt = DateTime.UtcNow,
                        ExamQuestions = subPaperInput.Questions.Select(q => new ExamQuestion
                        {
                            QuestionId = q.QuestionId,
                            QuestionScore = q.QuestionScore
                        }).ToList()
                    };
                    var createdSubExam = await exams.CreateAsync(subExam);
                    if (!slotExamsMap.TryGetValue(subPaperInput.SlotIndex, out var paperIds))
                    {
                        paperIds = new List<int>();
                        slotExamsMap[subPaperInput.SlotIndex] = paperIds;
                    }
                    paperIds.Add(createdSubExam.ExamId);
                }
            }

            // 2. Set slot templates & merge with any manual exam paper ids (Mode A)
            if (request.SlotTemplates != null)
            {
                ValidateSlotTemplates(exam.CourseId, examId, request.SlotTemplates);
                var slotEntities = request.SlotTemplates.Select(t => new ExamSlotTemplate
                {
                    SlotIndex = t.SlotIndex,
                    SlotName = t.SlotName.Trim(),
                    DurationMinutes = t.DurationMinutes,
                    MaxCapacity = t.MaxCapacity
                }).ToList();

                foreach (var t in request.SlotTemplates)
                {
                    if (t.ExamPaperIds != null && t.ExamPaperIds.Count > 0)
                    {
                        if (!slotExamsMap.TryGetValue(t.SlotIndex, out var pids))
                        {
                            pids = new List<int>();
                            slotExamsMap[t.SlotIndex] = pids;
                        }
                        pids.AddRange(t.ExamPaperIds.Where(id => !pids.Contains(id)));
                    }
                }

                await exams.SetSlotTemplatesAsync(examId, slotEntities, slotExamsMap);
            }

            return MapToExamDto(await exams.GetByIdWithQuestionsAsync(examId)
                ?? throw new KeyNotFoundException("Exam not found."));
        });

    public async Task<AutoPartitionPreviewResponse> PreviewPartitionSlotPapersAsync(AutoPartitionSlotPapersRequest request, int teacherId)
    {
        if (request.CourseId <= 0)
            throw new ArgumentException("CourseId is required.");
        if (request.SlotCount < 1)
            throw new ArgumentException("SlotCount must be at least 1.");
        if (request.QuestionsPerSlot < 1)
            throw new ArgumentException("QuestionsPerSlot must be at least 1.");

        await EnsureAssignedAsync(request.CourseId, teacherId);

        var totalNeeded = request.SlotCount * request.QuestionsPerSlot;
        var distinctIds = request.QuestionIds?.Distinct().ToList() ?? [];
        if (distinctIds.Count < totalNeeded)
        {
            throw new ArgumentException(
                $"Selected pool of {distinctIds.Count} questions is insufficient for {request.SlotCount} slots requiring {request.QuestionsPerSlot} questions each (minimum {totalNeeded} required).");
        }

        var questionEntities = await questions.GetByIdsForCourseAsync(request.CourseId, distinctIds);
        if (questionEntities.Count != distinctIds.Count)
        {
            throw new ArgumentException("Every question in the pool must exist and belong to the specified course.");
        }

        // Stratified partitioning by difficulty (Level: 1=Easy, 2=Medium, 3=Hard)
        var easyPool = questionEntities.Where(q => q.Level == 1).OrderBy(_ => Random.Shared.Next()).ToList();
        var mediumPool = questionEntities.Where(q => q.Level == 2).OrderBy(_ => Random.Shared.Next()).ToList();
        var hardPool = questionEntities.Where(q => q.Level >= 3 || q.Level < 1).OrderBy(_ => Random.Shared.Next()).ToList();

        int s = request.SlotCount;
        int k = request.QuestionsPerSlot;

        int easyQuota = easyPool.Count / s;
        int mediumQuota = mediumPool.Count / s;
        int hardQuota = hardPool.Count / s;

        var slotsQuestions = new List<List<QuestionBank>>();
        for (int i = 0; i < s; i++)
        {
            slotsQuestions.Add(new List<QuestionBank>());
        }

        // Allocate base quota per slot
        for (int i = 0; i < s; i++)
        {
            var slotList = slotsQuestions[i];

            int takeEasy = Math.Min(easyQuota, k - slotList.Count);
            if (takeEasy > 0 && easyPool.Count > 0)
            {
                var takeCount = Math.Min(takeEasy, easyPool.Count);
                slotList.AddRange(easyPool.Take(takeCount));
                easyPool.RemoveRange(0, takeCount);
            }

            int takeMed = Math.Min(mediumQuota, k - slotList.Count);
            if (takeMed > 0 && mediumPool.Count > 0)
            {
                var takeCount = Math.Min(takeMed, mediumPool.Count);
                slotList.AddRange(mediumPool.Take(takeCount));
                mediumPool.RemoveRange(0, takeCount);
            }

            int takeHard = Math.Min(hardQuota, k - slotList.Count);
            if (takeHard > 0 && hardPool.Count > 0)
            {
                var takeCount = Math.Min(takeHard, hardPool.Count);
                slotList.AddRange(hardPool.Take(takeCount));
                hardPool.RemoveRange(0, takeCount);
            }
        }

        // Top up any slots that haven't reached K questions using remaining available questions
        var remainingPool = new List<QuestionBank>();
        remainingPool.AddRange(easyPool);
        remainingPool.AddRange(mediumPool);
        remainingPool.AddRange(hardPool);

        for (int i = 0; i < s; i++)
        {
            var slotList = slotsQuestions[i];
            while (slotList.Count < k && remainingPool.Count > 0)
            {
                slotList.Add(remainingPool[0]);
                remainingPool.RemoveAt(0);
            }
        }

        // Build preview DTOs
        var baseTitle = string.IsNullOrWhiteSpace(request.ParentExamTitle) ? "Exam" : request.ParentExamTitle.Trim();
        var baseCode = string.IsNullOrWhiteSpace(request.ParentExamCode) ? "EXAM" : request.ParentExamCode.Trim();

        var previewSlots = new List<SlotPartitionPreviewDto>();
        for (int i = 0; i < s; i++)
        {
            var template = request.SlotTemplates?.FirstOrDefault(t => t.SlotIndex == i + 1);
            var slotIndex = template?.SlotIndex ?? (i + 1);
            var slotName = !string.IsNullOrWhiteSpace(template?.SlotName) ? template.SlotName.Trim() : $"Slot {slotIndex}";
            var subPaperCode = $"{baseCode}-S{slotIndex}";
            var subPaperTitle = $"{baseTitle} - {slotName}";

            var slotQs = slotsQuestions[i];
            previewSlots.Add(new SlotPartitionPreviewDto
            {
                SlotIndex = slotIndex,
                SlotName = slotName,
                SubPaperCode = subPaperCode,
                SubPaperTitle = subPaperTitle,
                EasyCount = slotQs.Count(q => q.Level == 1),
                MediumCount = slotQs.Count(q => q.Level == 2),
                HardCount = slotQs.Count(q => q.Level >= 3 || q.Level < 1),
                Questions = slotQs.Select(q => new ExamQuestionDto
                {
                    QuestionId = q.QuestionId,
                    QuestionText = q.QuestionText,
                    QuestionScore = q.MaxScore ?? 1.0f,
                    MaxScore = q.MaxScore,
                    IsFollowUpAllowed = q.IsFollowUpAllowed,
                    Level = q.Level
                }).ToList()
            });
        }

        var reserveQuestions = remainingPool.Select(q => new ExamQuestionDto
        {
            QuestionId = q.QuestionId,
            QuestionText = q.QuestionText,
            QuestionScore = q.MaxScore ?? 1.0f,
            MaxScore = q.MaxScore,
            IsFollowUpAllowed = q.IsFollowUpAllowed,
            Level = q.Level
        }).ToList();

        return new AutoPartitionPreviewResponse
        {
            Slots = previewSlots,
            ReserveQuestions = reserveQuestions,
            TotalSelected = distinctIds.Count,
            TotalAllocated = previewSlots.Sum(s => s.Questions.Count),
            TotalReserve = reserveQuestions.Count,
            Summary = $"Partitioned {previewSlots.Sum(s => s.Questions.Count)} questions across {s} slots ({k} per slot). {reserveQuestions.Count} surplus questions remain in reserve."
        };
    }

    public Task<ExamDto> UpdateExamQuestionsAsync(int examId, List<ExamQuestionItem> newQuestions, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var exam = await GetEditableExamAsync(examId, teacherId);
            await ValidateQuestionsAsync(exam.CourseId, newQuestions, exam.MaxQuestions);
            await ReplaceQuestionsAsync(examId, newQuestions);
            return MapToExamDto(await exams.GetByIdWithQuestionsAsync(examId)
                ?? throw new KeyNotFoundException("Exam not found."));
        });

    public async Task DeleteExamAsync(int examId, int teacherId)
        => await unitOfWork.ExecuteAsync(async () =>
        {
            var exam = await GetEditableExamAsync(examId, teacherId);
            exams.Remove(exam);
            await exams.SaveChangesAsync();
            return true;
        });

    private async Task<Exam> GetEditableExamAsync(int examId, int teacherId)
    {
        var exam = await exams.GetByIdWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException("Exam not found.");
        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException("You do not have permission to edit this exam.");
        await EnsureAssignedAsync(exam.CourseId, teacherId);
        if (exam.ExamStatus != ExamStatus.DRAFT || await exams.HasSessionsAsync(examId))
            throw new QuestionBankConflictException("Only draft exams without sessions can be changed or deleted.");
        return exam;
    }

    private async Task ValidateQuestionsAsync(int courseId, List<ExamQuestionItem>? items, int maxQuestions)
    {
        if (items is null || items.Any(q => q is null))
            throw new ArgumentException("Questions must not be null or contain null items.");
        foreach (var item in items)
        {
            Validate(item);
            if (!float.IsFinite(item.QuestionScore))
                throw new ArgumentException("Question scores must be finite.");
        }
        if (items.Count > maxQuestions)
            throw new ArgumentException("Question count exceeds MaxQuestions.");
        var ids = items.Select(q => q.QuestionId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("Duplicate question IDs are not allowed.");
        var selected = await questions.GetByIdsForCourseAsync(courseId, ids);
        if (selected.Count != ids.Count)
            throw new ArgumentException("Every question must exist and belong to the exam course.");
        if (selected.Any(q => string.IsNullOrWhiteSpace(q.QuestionText) || q.Level is < 1 or > 3
            || !QuestionRubricValidator.IsValidMaxScore(q.MaxScore ?? 1)))
            throw new ArgumentException("The selected question bank contains invalid questions.");
        if (selected.Any(q => q.ApprovalStatus != QuestionApprovalStatus.APPROVED))
            throw new QuestionBankConflictException("Only approved questions can be added to an exam.");
    }

    private static void ValidateSlotTemplates(int courseId, int examId, List<CreateExamSlotTemplateInput>? templates)
    {
        if (templates == null || templates.Count == 0) return;

        var indices = templates.Select(t => t.SlotIndex).ToList();
        if (indices.Any(idx => idx < 1))
            throw new ArgumentException("Slot index (SlotIndex) must be 1 or greater.");
        if (indices.Distinct().Count() != indices.Count)
            throw new ArgumentException("Slot indices (SlotIndex) cannot contain duplicates.");

        foreach (var tpl in templates)
        {
            if (string.IsNullOrWhiteSpace(tpl.SlotName))
                throw new ArgumentException("Slot name cannot be empty.");
            if (tpl.DurationMinutes <= 0 || tpl.DurationMinutes > 480)
                throw new ArgumentException("Slot duration must be between 1 and 480 minutes.");
            if (tpl.MaxCapacity <= 0 || tpl.MaxCapacity > 500)
                throw new ArgumentException("Slot maximum capacity must be between 1 and 500 students.");

            // Edge Case #1: Validate not containing parent examId itself
            if (tpl.ExamPaperIds != null && examId > 0 && tpl.ExamPaperIds.Contains(examId))
            {
                throw new ArgumentException("An exam cannot assign itself as a sub-paper of its own slot template.");
            }
        }
    }

    private Task ReplaceQuestionsAsync(int examId, List<ExamQuestionItem> items)
        => exams.ReplaceQuestionsAsync(examId, items.Select(q => new ExamQuestion
        {
            ExamId = examId,
            QuestionId = q.QuestionId,
            QuestionScore = q.QuestionScore
        }));

    private async Task EnsureAssignedAsync(int courseId, int teacherId)
    {
        var course = await courses.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("You are not assigned to this course.");
    }

    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ArgumentException("Invalid exam or suggestion fields.");
    }

    private async Task<string> GenerateUniqueExamCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = "EX-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
            if (!await exams.ExamCodeExistsAsync(code))
                return code;
        }
        throw new InvalidOperationException("Unable to generate a unique Exam Code. Please try again.");
    }

    private static ExamDto MapToExamDto(Exam exam) => new()
    {
        CanEdit = exam.ExamStatus == ExamStatus.DRAFT && !exam.ExamSessions.Any(),
        ExamId = exam.ExamId,
        Title = exam.Title,
        ExamCode = exam.ExamCode,
        CourseId = exam.CourseId,
        CourseName = exam.Course?.CourseName,
        CreatedBy = exam.CreatedBy,
        CreatedByName = exam.CreatedByNavigation?.FullName,
        DurationMinutes = exam.DurationMinutes,
        MaxQuestions = exam.MaxQuestions,
        ExamStatus = exam.ExamStatus?.ToString(),
        CreatedAt = exam.CreatedAt,
        Questions = exam.ExamQuestions.Select(eq => new ExamQuestionDto
        {
            QuestionId = eq.QuestionId,
            QuestionText = eq.Question?.QuestionText ?? string.Empty,
            QuestionScore = eq.QuestionScore,
            MaxScore = eq.Question?.MaxScore,
            IsFollowUpAllowed = eq.Question?.IsFollowUpAllowed,
            Level = eq.Question?.Level
        }).ToList(),
        SlotTemplates = exam.SlotTemplates?.OrderBy(st => st.SlotIndex).Select(st => new ExamSlotTemplateDto
        {
            SlotTemplateId = st.SlotTemplateId,
            SlotIndex = st.SlotIndex,
            SlotName = st.SlotName,
            DurationMinutes = st.DurationMinutes,
            MaxCapacity = st.MaxCapacity,
            ExamPaperIds = st.TemplateExams.Select(te => te.ExamPaperId).ToList(),
            ExamPapers = st.TemplateExams.Select(te => new SessionExamSummaryDto
            {
                ExamId = te.ExamPaperId,
                ExamCode = te.ExamPaper?.ExamCode ?? "",
                Title = te.ExamPaper?.Title ?? "",
                DurationMinutes = te.ExamPaper?.DurationMinutes ?? 0,
                TotalQuestions = te.ExamPaper?.ExamQuestions?.Count ?? te.ExamPaper?.MaxQuestions ?? 0
            }).ToList()
        }).ToList() ?? new List<ExamSlotTemplateDto>()
    };
}
