# AIVES Question Bank APIs Documentation

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Overview

This document describes the Question Bank APIs, including CRUD operations, rubric management, and authorization rules.

---

## 2. Entity Relationships

### 2.1 QuestionBank Entity

```csharp
public partial class QuestionBank
{
    public int QuestionId { get; set; }
    public int CourseId { get; set; }
    public int? DocumentId { get; set; }           // Source document (optional)
    public string QuestionText { get; set; }       // Question content
    public string? Topic { get; set; }            // Topic tag
    public string? SampleAnswer { get; set; }      // Sample answer
    public string? LearningOutcome { get; set; }   // Learning outcome
    public float? MaxScore { get; set; }           // Max score
    public int Level { get; set; }                 // Difficulty (1-3)
    public bool? IsFollowUpAllowed { get; set; }  // Allow follow-up questions
    public DateTime? CreatedAt { get; set; }
    
    public virtual Course Course { get; set; }
    public virtual MarkdownDocument? Document { get; set; }
    public virtual ICollection<QuestionRubricCriterion> Rubric { get; set; }
    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; }
}
```

### 2.2 QuestionRubricCriterion Entity

```csharp
public class QuestionRubricCriterion
{
    public int CriterionId { get; set; }
    public int QuestionId { get; set; }
    public int Position { get; set; }
    public string Description { get; set; }
    public decimal MaxScore { get; set; }
    
    public virtual QuestionBank Question { get; set; }
}
```

### 2.3 Relationship Diagram

```
Course
   │
   └── QuestionBank
          │
          ├── Document (source)
          │
          └── Rubric (criteria)
                 │
                 └── QuestionRubricCriterion
```

---

## 3. API Endpoints

### 3.1 Question CRUD

| Endpoint | Method | Auth | Roles | Description |
|----------|--------|------|-------|-------------|
| `/api/courses/{courseId}/questions` | GET | JWT | STAFF, ADMIN | Get questions with pagination |
| `/api/courses/{courseId}/questions/{questionId}` | GET | JWT | STAFF, ADMIN | Get question details |
| `/api/courses/{courseId}/questions` | POST | JWT | STAFF, ADMIN | Create new question |
| `/api/courses/{courseId}/questions/{questionId}` | PUT | JWT | STAFF, ADMIN | Update question |
| `/api/courses/{courseId}/questions/{questionId}` | DELETE | JWT | STAFF, ADMIN | Delete question |

---

## 4. Authorization Rules

### 4.1 Role-Based Access

| Role | Access |
|------|--------|
| ADMIN | All questions in all courses |
| STAFF | Questions in assigned courses only |
| EXAMINER | View only (if authorized) |
| STUDENT | No access |

### 4.2 Ownership Validation

**STAFF access validation:**
- Get course by `courseId`
- Check `Course.TeacherId == currentUserId`
- If not match → `403 Forbidden`

---

## 5. Request/Response Samples

### 5.1 Create Question

```
POST /api/courses/1/questions
Authorization: Bearer <jwt>

Request:
{
  "questionText": "Explain microservices architecture",
  "topic": "Architecture",
  "sampleAnswer": "Microservices are independently deployable services...",
  "learningOutcome": "LO1: Explain microservices",
  "rubric": [
    { "description": "Definition", "maxScore": 4 },
    { "description": "Examples", "maxScore": 3 }
  ],
  "level": 2,
  "maxScore": 10,
  "isFollowUpAllowed": true
}

Response 201 Created:
{
  "questionId": 1,
  "courseId": 1,
  "documentId": null,
  "createdAt": "2026-10-16T10:00:00Z",
  "isInUse": false,
  "questionText": "Explain microservices architecture",
  "topic": "Architecture",
  "sampleAnswer": "Microservices are independently deployable services...",
  "learningOutcome": "LO1: Explain microservices",
  "rubric": [
    { "description": "Definition", "maxScore": 4 },
    { "description": "Examples", "maxScore": 3 }
  ],
  "maxScore": 10,
  "level": 2,
  "isFollowUpAllowed": true
}
```

### 5.2 Get Questions (Paginated)

```
GET /api/courses/1/questions?page=1&pageSize=20&sortBy=createdAt&sortDirection=desc
Authorization: Bearer <jwt>

Response 200 OK:
{
  "items": [...],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20,
  "totalPages": 5
}
```

### 5.3 Access Denied

```
GET /api/courses/999/questions
Authorization: Bearer <jwt> (STAFF, but course 999 assigned to different teacher)

Response 403 Forbidden:
{
  "message": "You are not assigned to this course."
}
```

---

## 6. Rubric Management

### 6.1 Rubric Structure

- **RubricCriterionDto:**
  - `description`: Criterion description
  - `maxScore`: Maximum score (decimal)

### 6.2 Rubric Validation

- Each criterion must have description and maxScore
- MaxScore must be between 0.01 and 100
- Total rubric score must not exceed question's MaxScore
- Position is auto-assigned by order in list

### 6.3 Rubric Update Rules

- Rubric is replaced entirely on update (not merged)
- Cannot update rubric for questions in use (via ExamQuestion)
- Validation runs before save

---

## 7. Expected Keywords

**Status:** NOT IMPLEMENTED

The current QuestionBank entity does NOT have an `ExpectedKeywords` field.

- Expected keywords are not stored in the database
- AI Evaluation Service handles keyword matching internally
- If future implementation is needed, it should be added as a separate entity or JSON field

---

## 8. Question Status

**Status:** NOT IMPLEMENTED

The current system does NOT have a question status field (Draft, PendingReview, Approved, Rejected).

- Questions are directly available after creation
- Question bank is NOT subject to approval workflow
- AI-generated questions are immediately available

**Note:** This may need to be revisited if business requirements change.

---

## 9. Validation Rules

### 9.1 Question Validation

| Field | Rule |
|-------|------|
| QuestionText | Required, max 10000 chars |
| Topic | Optional, max 1000 chars |
| SampleAnswer | Optional, max 10000 chars |
| LearningOutcome | Required, max 2000 chars |
| Level | Range 1-3, default 1 |
| MaxScore | Range 0.01-100, default 1 |
| IsFollowUpAllowed | Default true |
| Rubric | Optional, list of criteria |

### 9.2 Rubric Validation

| Field | Rule |
|-------|------|
| Description | Required |
| MaxScore | Range 0.01-100 |

### 9.3 Error Responses

| Status | Condition |
|--------|-----------|
| 401 Unauthorized | Missing/invalid token |
| 403 Forbidden | Not assigned to course |
| 404 Not Found | Course/question not found |
| 409 Conflict | Question in use, cannot delete/update |

---

## 10. Security Implementation

### 10.1 Controller Layer

```csharp
[ApiController]
[Route("api/courses/{courseId}/questions")]
[Authorize(Roles = "STAFF,ADMIN")]
public class QuestionBankController : ControllerBase
```

### 10.2 Service Layer

```csharp
private async Task EnsureAssignedAsync(int courseId, int teacherId)
{
    var course = await courses.GetByIdAsync(courseId)
        ?? throw new KeyNotFoundException("Course not found.");
    if (course.TeacherId != teacherId)
        throw new UnauthorizedAccessException("You are not assigned to this course.");
}
```

### 10.3 Ownership Check Flow

```
1. User calls API with JWT
2. Extract userId from token
3. Get course by courseId
4. Check Course.TeacherId == userId (for STAFF)
5. If not match → 403 Forbidden
```

---

## 11. Business Rules

### 11.1 Create Question

- Must be assigned to course
- Validates all fields
- Creates question with empty rubric or provided rubric
- Question is immediately available

### 11.2 Update Question

- Must be assigned to course
- Must not be in use (via ExamQuestion)
- Replaces entire rubric

### 11.3 Delete Question

- Must be assigned to course
- Must not be in use (via ExamQuestion)
- Returns 409 if in use

### 11.4 Question In Use

Question is considered "in use" if:
- It is referenced by any ExamQuestion
- It has any Answers associated

---

## 12. Swagger Testing Guide

### 12.1 Test Cases

| # | Scenario | Endpoint | Expected |
|---|----------|----------|----------|
| 1 | ADMIN creates question | POST /api/courses/1/questions | 201 Created |
| 2 | ADMIN lists questions | GET /api/courses/1/questions | 200 OK |
| 3 | STAFF creates in assigned course | POST /api/courses/1/questions | 201 Created |
| 4 | STAFF creates in unassigned course | POST /api/courses/999/questions | 403 Forbidden |
| 5 | STAFF updates own question | PUT /api/courses/1/questions/1 | 200 OK |
| 6 | STAFF deletes question in use | DELETE /api/courses/1/questions/1 | 409 Conflict |
| 7 | STUDENT tries access | GET /api/courses/1/questions | 403 Forbidden |
| 8 | Anonymous access | GET /api/courses/1/questions | 401 Unauthorized |

### 12.2 Testing Steps

1. **Login as ADMIN**
   ```
   POST /api/auth/login
   Body: { "identifier": "admin", "password": "admin123" }
   ```

2. **Create Course and Assign Teacher**
   ```
   POST /api/admin/courses
   Header: Authorization: Bearer <admin_token>
   Body: { "courseCode": "SWD392", "courseName": "Software Architecture" }

   PUT /api/admin/courses/1/assign-teacher
   Header: Authorization: Bearer <admin_token>
   Body: { "teacherId": 5 }
   ```

3. **Login as STAFF**
   ```
   POST /api/auth/login
   Body: { "identifier": "teacher1", "password": "teacher123" }
   ```

4. **Create Question**
   ```
   POST /api/courses/1/questions
   Header: Authorization: Bearer <staff_token>
   Body: {
     "questionText": "What is microservices?",
     "topic": "Architecture",
     "sampleAnswer": "Architecture style...",
     "learningOutcome": "LO1",
     "rubric": [{"description": "Definition", "maxScore": 5}],
     "level": 1,
     "maxScore": 10
   }
   ```

5. **Try Unassigned Course**
   ```
   GET /api/courses/999/questions
   Header: Authorization: Bearer <staff_token>
   Expected: 403 Forbidden
   ```

---

## 13. Database Schema

### 13.1 Tables

**QuestionBank:**
- QuestionId (PK)
- CourseId (FK)
- DocumentId (FK, nullable)
- QuestionText
- Topic
- SampleAnswer
- LearningOutcome
- MaxScore
- Level
- IsFollowUpAllowed
- CreatedAt

**QuestionRubricCriterion:**
- CriterionId (PK)
- QuestionId (FK)
- Position
- Description
- MaxScore

### 13.2 Relationships

- QuestionBank.CourseId → Course.CourseId
- QuestionBank.DocumentId → MarkdownDocument.DocumentId (nullable)
- QuestionRubricCriterion.QuestionId → QuestionBank.QuestionId
- QuestionBank → ExamQuestion → Exam

### 13.3 Migration

**No migration required** - Schema already supports all required functionality.

---

## 14. Known Issues

### 14.1 Expected Keywords

- NOT IMPLEMENTED - No field in entity
- AI evaluation handles keywords internally

### 14.2 Question Status

- NOT IMPLEMENTED - No status field (Draft/Approved/Rejected)
- Questions available immediately after creation
- May need implementation if business requires approval workflow

---

## 15. Dependencies

### 15.1 External Dependencies

- AI Question Generation (`IAIQuestionService`)
- AI Evaluation (`IAiServiceClient`)
- Document Management (`IDocumentService`)

### 15.2 Related Modules

- Exam Management (uses questions)
- Document Management (source documents)
- AI Services (generation and evaluation)

---

## 16. Revision History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-10-16 | 1.0 | Initial documentation | Hưng |
