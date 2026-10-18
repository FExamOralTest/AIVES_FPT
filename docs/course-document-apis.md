# AIVES Course & Document APIs Documentation

**Version:** 1.1  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Overview

This document describes the Course and Document management APIs, including authorization rules and ownership validation.

---

## 1.1 Duplicate Routes Status

**Finding:** Two controllers have overlapping functionality but different routes - **No conflict**.

| Controller | Base Route | Notes |
|------------|------------|-------|
| `DocumentsController` | `/api/courses/{courseId}/documents` | Route-based on courseId |
| `TeacherDocumentController` | `/api/documents` | Standalone document operations |

**Analysis:**
- Routes are different and non-conflicting
- Both implement valid business needs
- No action required - keep both controllers

---

## 2. Entity Relationships

### 2.1 Course Entity

```csharp
public partial class Course
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; }  // Unique
    public string CourseName { get; set; }
    public int? TeacherId { get; set; }    // Assigned STAFF
    public int? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    
    public virtual User? Teacher { get; set; }
    public virtual ICollection<MarkdownDocument> MarkdownDocuments { get; set; }
    public virtual ICollection<Exam> Exams { get; set; }
    public virtual ICollection<QuestionBank> QuestionBanks { get; set; }
}
```

### 2.2 Document Entity

```csharp
public partial class MarkdownDocument
{
    public int DocumentId { get; set; }
    public int CourseId { get; set; }              // FK to Course
    public int UploadedBy { get; set; }            // FK to User
    public string FileName { get; set; }
    public DocType DocType { get; set; }           // CONTENT or LEARNING_OUTCOMES
    public string Content { get; set; }
    public DateTime? UploadedAt { get; set; }
    
    public virtual Course Course { get; set; }
    public virtual User UploadedByNavigation { get; set; }
}
```

### 2.3 Relationship Diagram

```
User (STAFF)
   │
   ├── TeacherId (Course)
   │      │
   │      └── MarkdownDocuments
   │             │
   │             └── CourseId (Course)
   │
   └── UploadedBy (MarkdownDocument)
```

---

## 3. API Endpoints

### 3.1 Course Management (Admin)

| Endpoint | Method | Auth | Roles | Description |
|----------|--------|------|-------|-------------|
| `/api/admin/courses` | GET | JWT | ADMIN | Get all courses |
| `/api/admin/courses/{id}` | GET | JWT | ADMIN | Get course by ID |
| `/api/admin/courses/teacher/{teacherId}` | GET | JWT | ADMIN | Get courses by teacher |
| `/api/admin/courses` | POST | JWT | ADMIN | Create new course |
| `/api/admin/courses/{id}` | PUT | JWT | ADMIN | Update course |
| `/api/admin/courses/{id}/assign-teacher` | PUT | JWT | ADMIN | Assign STAFF to course |
| `/api/admin/courses/{id}` | DELETE | JWT | ADMIN | Delete course |

### 3.2 Course Management (Teacher/Staff)

| Endpoint | Method | Auth | Roles | Description |
|----------|--------|------|-------|-------------|
| `/api/teacher/courses` | GET | JWT | STAFF, ADMIN | Get assigned courses |
| `/api/teacher/courses/{id}` | GET | JWT | STAFF, ADMIN | Get course details (own only) |

### 3.3 Document Management

| Endpoint | Method | Auth | Roles | Description |
|----------|--------|------|-------|-------------|
| `/api/courses/{courseId}/documents` | GET | JWT | STAFF, ADMIN | Get documents for course |
| `/api/courses/{courseId}/documents` | POST | JWT | STAFF, ADMIN | Upload markdown document |
| `/api/courses/{courseId}/documents/{documentId}` | GET | JWT | STAFF, ADMIN | Get document details |
| `/api/courses/{courseId}/documents/{documentId}` | PUT | JWT | STAFF, ADMIN | Update document |
| `/api/courses/{courseId}/documents/{documentId}` | DELETE | JWT | STAFF, ADMIN | Delete document |
| `/api/courses/{courseId}/documents/generate-questions` | POST | JWT | STAFF, ADMIN | Generate AI questions |

### 3.4 Document Management (Alternate Routes)

| Endpoint | Method | Auth | Roles | Description |
|----------|--------|------|-------|-------------|
| `/api/documents/course/{courseId}` | GET | JWT | STAFF, ADMIN | Get documents by course |
| `/api/documents/{id}` | GET | JWT | STAFF, ADMIN | Get document by ID |
| `/api/documents/upload` | POST | JWT | STAFF, ADMIN | Upload document |
| `/api/documents/{id}` | PUT | JWT | STAFF, ADMIN | Replace document |
| `/api/documents/{id}` | DELETE | JWT | STAFF, ADMIN | Delete document |
| `/api/documents/generate-exam` | POST | JWT | STAFF, ADMIN | Generate exam from questions |

---

## 4. Authorization Rules

### 4.1 Role-Based Access

| Role | Course Access | Document Access |
|------|--------------|-----------------|
| ADMIN | All courses | All documents |
| STAFF | Assigned courses only | Documents in assigned courses |
| EXAMINER | None | None |
| STUDENT | None | None |

### 4.2 Ownership Validation

**Course Ownership:**
- ADMIN: Can access all courses
- STAFF: Can only access courses where `Course.TeacherId == currentUserId`

**Document Ownership:**
- ADMIN: Can access all documents
- STAFF: Can only access documents where `Document.Course.TeacherId == currentUserId`

### 4.3 Validation Flow

```
1. User calls API with JWT token
2. Extract userId from token claims
3. Get user's role from token
4. For STAFF:
   a. Get course by courseId
   b. Check if course.TeacherId == userId
   c. If not match → 403 Forbidden
5. For documents:
   a. Get course from document
   b. Check course.TeacherId == userId
   c. If not match → 403 Forbidden
```

---

## 5. Request/Response Samples

### 5.1 Create Course (Admin)

```
POST /api/admin/courses
Authorization: Bearer <jwt>

Request:
{
  "courseCode": "SWD392",
  "courseName": "Software Architecture",
  "teacherId": 5  // Optional
}

Response 201 Created:
{
  "courseId": 1,
  "courseCode": "SWD392",
  "courseName": "Software Architecture",
  "teacherId": 5,
  "teacherName": "Dr. Smith",
  "createdBy": 1,
  "createdAt": "2026-10-16T10:00:00Z"
}
```

### 5.2 Get Staff's Courses

```
GET /api/teacher/courses
Authorization: Bearer <jwt> (STAFF role)

Response 200 OK:
[
  {
    "courseId": 1,
    "courseCode": "SWD392",
    "courseName": "Software Architecture",
    "teacherId": 5,
    "teacherName": "Dr. Smith",
    "createdBy": 1,
    "createdAt": "2026-10-16T10:00:00Z"
  }
]
```

### 5.3 Upload Document

```
POST /api/courses/1/documents
Authorization: Bearer <jwt> (STAFF role)
Content-Type: multipart/form-data

Form Fields:
- file: <markdown file>
- title: "Chapter 1"
- docType: 0  // 0 = CONTENT, 1 = LEARNING_OUTCOMES

Response 201 Created:
{
  "documentId": 1,
  "courseId": 1,
  "fileName": "chapter1.md",
  "docType": "CONTENT",
  "content": "# Chapter 1\n\n...",
  "uploadedAt": "2026-10-16T10:00:00Z"
}
```

### 5.4 Access Denied (STAFF accessing unassigned course)

```
GET /api/courses/999/documents
Authorization: Bearer <jwt> (STAFF, but course 999 assigned to different teacher)

Response 403 Forbidden:
{
  "message": "Not your course."
}
```

---

## 6. Error Responses

| Status | Condition |
|--------|-----------|
| 401 Unauthorized | Missing or invalid JWT token |
| 403 Forbidden | Valid token but insufficient permissions |
| 404 Not Found | Course or Document doesn't exist |
| 409 Conflict | Duplicate course code or document type |

### 6.1 Error Response Samples

```json
// 401 Unauthorized
{ "message": "Invalid token." }

// 403 Forbidden (ownership)
{ "message": "Not your course." }

// 404 Not Found
{ "message": "Course 999 not found." }

// 409 Conflict
{ "message": "Course code 'SWD392' already exists." }
```

---

## 7. Security Implementation

### 7.1 Controller Layer

**AdminCourseController:**
- `[Authorize(Roles = "ADMIN")]` on class
- No ownership check needed (ADMIN has full access)

**TeacherCourseController:**
- `[Authorize(Roles = "STAFF,ADMIN")]` on class
- Ownership check in `GetById()`:
```csharp
if (!isAdmin && course.TeacherId != teacherId.Value) return Forbid();
```

**DocumentsController:**
- `[Authorize(Roles = "STAFF,ADMIN")]` on class
- Passes `teacherId` to service for ownership validation

### 7.2 Service Layer

**CourseService:**
- `GetByTeacherIdAsync(teacherId)`: Filters by teacher
- No authorization check (handled by controller)

**DocumentService:**
- Validates `course.TeacherId == teacherId` for every operation:
  - `UploadMarkdownAsync`
  - `GetDocumentsByCourseAsync`
  - `GetDocumentByIdAsync`
  - `UpdateDocumentAsync`
  - `DeleteDocumentAsync`
  - `GenerateQuestionsAsync`

### 7.3 Repository Layer

**CourseRepository:**
- `GetByTeacherIdAsync(int teacherId)`: Filters courses by teacher

**MarkdownDocumentRepository:**
- `GetByCourseIdAsync(int courseId)`: Gets documents for course
- `ExistsByTypeAsync()`: Checks for duplicate document types

---

## 8. Business Rules

### 8.1 Course Rules

1. Course code must be unique
2. Course can have at most one assigned STAFF
3. Only ADMIN can create courses
4. Only ADMIN can delete courses
5. Only ADMIN can assign STAFF to courses
6. STAFF can only see/edit their assigned courses

### 8.2 Document Rules

1. Each course can have at most one CONTENT document
2. Each course can have at most one LEARNING_OUTCOMES document
3. Only assigned STAFF can upload documents
4. Only assigned STAFF can edit/delete documents
5. Document content must be valid markdown (max 5MB)
6. Generate questions requires both CONTENT and LEARNING_OUTCOMES

---

## 9. Swagger Testing Guide

### 9.1 Test Cases

| # | Scenario | Endpoint | Expected |
|---|----------|----------|----------|
| 1 | ADMIN creates course | POST /api/admin/courses | 201 Created |
| 2 | ADMIN lists all courses | GET /api/admin/courses | 200 OK |
| 3 | ADMIN assigns teacher | PUT /api/admin/courses/1/assign-teacher | 200 OK |
| 4 | STAFF views assigned courses | GET /api/teacher/courses | 200 OK |
| 5 | STAFF views unassigned course | GET /api/teacher/courses/999 | 403 Forbidden |
| 6 | STAFF uploads document | POST /api/courses/1/documents | 201 Created |
| 7 | STAFF views documents | GET /api/courses/1/documents | 200 OK |
| 8 | STAFF accesses wrong course docs | GET /api/courses/999/documents | 403 Forbidden |
| 9 | STUDENT tries access | GET /api/admin/courses | 403 Forbidden |
| 10 | Anonymous access | GET /api/admin/courses | 401 Unauthorized |

### 9.2 Testing Steps

1. **Login as ADMIN**
   ```
   POST /api/auth/login
   Body: { "identifier": "admin", "password": "admin123" }
   ```

2. **Create Course**
   ```
   POST /api/admin/courses
   Header: Authorization: Bearer <admin_token>
   Body: { "courseCode": "SWD392", "courseName": "Software Architecture" }
   ```

3. **Assign Teacher**
   ```
   PUT /api/admin/courses/1/assign-teacher
   Header: Authorization: Bearer <admin_token>
   Body: { "teacherId": 5 }
   ```

4. **Login as STAFF**
   ```
   POST /api/auth/login
   Body: { "identifier": "teacher1", "password": "teacher123" }
   ```

5. **STAFF views courses**
   ```
   GET /api/teacher/courses
   Header: Authorization: Bearer <staff_token>
   ```

6. **STAFF uploads document**
   ```
   POST /api/courses/1/documents
   Header: Authorization: Bearer <staff_token>
   Content-Type: multipart/form-data
   Body: file=@chapter1.md, docType=0
   ```

---

## 10. Migration

### 10.1 Current Schema

The Course and Document tables are already created with proper relationships:

- `Course.TeacherId` → FK to `User.UserId`
- `MarkdownDocument.CourseId` → FK to `Course.CourseId`

### 10.2 No Migration Required

Current schema supports all required functionality:
- ✅ Course-Teacher relationship
- ✅ Document-Course relationship  
- ✅ Role-based access control
- ✅ Ownership validation

---

## 11. Known Issues

### 11.1 Duplicate Routes - RESOLVED

Routes are different and non-conflicting:
- `DocumentsController`: `/api/courses/{courseId}/documents` - Course-centric
- `TeacherDocumentController`: `/api/documents` - Standalone document operations

Both implement valid business needs. No conflict detected.

### 11.2 Missing Endpoints

- No endpoint to list all documents globally (ADMIN only) - **Not in Task 1.7 scope**
- No endpoint to transfer course ownership between teachers - **Not in Task 1.7 scope**

---

## 12. Build Status

**Build Attempt:** Unable to verify - .NET SDK not available in current environment.

**Recommendation:** Run build locally:
```bash
cd backend
dotnet build
```

---

## 13. Verification Checklist

- [x] Course entity has TeacherId relationship
- [x] Document entity has CourseId relationship
- [x] Ownership validation in DocumentService (all 6 methods)
- [x] Ownership validation in TeacherCourseController
- [x] Role-based authorization in all controllers
- [x] No duplicate route conflicts
- [x] ADMIN has full access
- [x] STAFF limited to assigned courses
- [x] EXAMINER and STUDENT excluded from course/document management

---

## 12. Appendix: DTOs

### 12.1 Course DTOs

```csharp
// CreateCourseRequest
{
  "courseCode": "string",
  "courseName": "string",
  "teacherId": 0  // Optional
}

// UpdateCourseRequest
{
  "courseCode": "string",  // Optional
  "courseName": "string"    // Required
}

// AssignTeacherRequest
{
  "teacherId": 0
}

// CourseDto
{
  "courseId": 0,
  "courseCode": "string",
  "courseName": "string",
  "teacherId": 0,
  "teacherName": "string",
  "createdBy": 0,
  "createdAt": "2026-10-16T10:00:00Z"
}
```

### 12.2 Document DTOs

```csharp
// UploadMarkdownRequest (form-data)
{
  "file": "binary",
  "title": "string",
  "docType": 0  // 0=CONTENT, 1=LEARNING_OUTCOMES
}

// UpdateMarkdownRequest
{
  "file": "binary",       // Optional
  "fileName": "string",   // Optional
  "content": "string",    // Optional
  "docType": 0
}

// MarkdownDocumentDto
{
  "documentId": 0,
  "courseId": 0,
  "fileName": "string",
  "docType": "CONTENT",
  "content": "string",
  "uploadedAt": "2026-10-16T10:00:00Z"
}
```

---

## 13. Revision History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-10-16 | 1.0 | Initial documentation | Hưng |
