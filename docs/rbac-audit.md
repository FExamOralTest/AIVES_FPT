# AIVES RBAC Audit Report

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Executive Summary

This document audits all API endpoints for proper Role-Based Access Control (RBAC) and ownership authorization.

**Audit Date:** 2026-10-16  
**Auditor:** Hưng (BE1)

---

## 2. Authentication Status

| Component | Status | Notes |
|-----------|--------|-------|
| Password Hashing (BCrypt) | ✅ Secure | Cost factor 10 |
| JWT Generation (HS256) | ✅ Secure | Key min 32 chars |
| Token Expiration | ✅ Configurable | Default 60 min |
| Token Blacklist | ✅ Implemented | For logout |
| Rate Limiting | ✅ Implemented | On login |
| Role Authorization | ✅ Implemented | Via policies |
| Ownership Validation | ⚠️ Partial | Some endpoints missing |

---

## 3. Role Definitions

| Role | Enum Value | Responsibilities |
|------|------------|------------------|
| ADMIN | 0 | Full system access |
| STUDENT | 1 | Take exams, view own results |
| STAFF | 2 | Manage courses, exams, sessions |
| EXAMINER | 3 | Review and publish results |

---

## 4. Endpoint Authorization Matrix

### 4.1 Authentication Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/auth/register` | POST | Anonymous | - | - | ✅ Complete |
| `/api/auth/login` | POST | Anonymous | Rate Limited | - | ✅ Complete |
| `/api/auth/me` | GET | JWT | All | Current user | ✅ Complete |
| `/api/auth/logout` | POST | JWT | All | Current user | ✅ Complete |
| `/api/auth/admin/create-user` | POST | JWT | ADMIN | - | ✅ Complete |
| `/api/auth/admin/users` | GET | JWT | ADMIN | - | ✅ Complete |
| `/api/auth/admin/users/{id}` | PUT | JWT | ADMIN | - | ✅ Complete |

### 4.2 Course Management Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/admin/courses` | GET | JWT | ADMIN | - | ✅ Complete |
| `/api/admin/courses/{id}` | GET | JWT | ADMIN | - | ✅ Complete |
| `/api/admin/courses` | POST | JWT | ADMIN | - | ✅ Complete |
| `/api/admin/courses/{id}` | PUT | JWT | ADMIN | - | ✅ Complete |
| `/api/admin/courses/{id}/assign-teacher` | PUT | JWT | ADMIN | - | ✅ Complete |
| `/api/admin/courses/{id}` | DELETE | JWT | ADMIN | - | ✅ Complete |
| `/api/teacher/courses` | GET | JWT | STAFF, ADMIN | Assigned only | ⚠️ Needs verification |

### 4.3 Document Management Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/documents/upload` | POST | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/documents/course/{courseId}` | GET | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/documents/{id}` | GET | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |
| `/api/documents/{id}` | PUT | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |
| `/api/documents/{id}` | DELETE | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |

### 4.4 Question Bank Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/courses/{courseId}/questions` | GET | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/courses/{courseId}/questions/{questionId}` | GET | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/courses/{courseId}/questions` | POST | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/courses/{courseId}/questions/{questionId}` | PUT | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/courses/{courseId}/questions/{questionId}` | DELETE | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |

### 4.5 Exam Management Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/exams` | POST | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/exams/{examId}` | GET | JWT | STAFF, ADMIN | Owner + course assigned | ⚠️ Needs verification |
| `/api/exams/my` | GET | JWT | STAFF, ADMIN | Owned exams in assigned courses | ⚠️ Needs verification |
| `/api/exams/{examId}` | PUT | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |
| `/api/exams/{examId}` | DELETE | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |
| `/api/exams/suggest-questions` | POST | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |

### 4.6 Exam Session Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/exam-sessions` | POST | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/exam-sessions/{sessionId}` | GET | JWT | STAFF, ADMIN | Course assigned | ⚠️ Needs verification |
| `/api/exam-sessions/teacher/{teacherId}` | GET | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |
| `/api/exam-sessions/{sessionId}/status` | PUT | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |
| `/api/exam-sessions/{sessionId}/monitor` | GET | JWT | STAFF, ADMIN | Owner | ⚠️ Needs verification |

### 4.7 Enrollment Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/enroll/session` | POST | JWT | STUDENT | - | ✅ Complete |
| `/api/student/enrollments` | GET | JWT | STUDENT | Own only | ✅ Complete |

### 4.8 Viva Session Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/student/viva-sessions/{sessionId}/start` | POST | JWT | STUDENT | Enrolled | ✅ Complete |
| `/api/student/viva-sessions/{sessionId}/current-question` | GET | JWT | STUDENT | In session | ✅ Complete |
| `/api/student/viva-sessions/{sessionId}/submit-answer` | POST | JWT | STUDENT | In session | ✅ Complete |
| `/api/student/viva-sessions/{sessionId}/finish` | POST | JWT | STUDENT | In session | ✅ Complete |

### 4.9 Result Review Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/results/pending` | GET | JWT | EXAMINER, ADMIN | Assigned sessions | ✅ Implemented |
| `/api/results/session/{sessionId}` | GET | JWT | EXAMINER, ADMIN | Assigned sessions | ✅ Implemented |
| `/api/results/{resultId}` | GET | JWT | EXAMINER, ADMIN | Assigned sessions | ✅ Implemented |
| `/api/results/{resultId}/review` | PUT | JWT | EXAMINER, ADMIN | Assigned sessions | ✅ Implemented |
| `/api/results/{resultId}/publish` | POST | JWT | EXAMINER, ADMIN | Assigned sessions | ✅ Implemented |

### 4.10 Student Result Endpoints

| Endpoint | Method | Auth Required | Roles Allowed | Ownership Check | Status |
|----------|--------|---------------|---------------|-----------------|--------|
| `/api/student/results/{sessionId}` | GET | JWT | STUDENT | Own + Published only | ✅ Complete |

---

## 5. Ownership Validation Status

### 5.1 STAFF Ownership

| Resource | Validation | Status |
|----------|------------|--------|
| Course | Course.TeacherId == CurrentUserId | ✅ Implemented |
| Document | Document.Course.TeacherId == CurrentUserId | ⚠️ Needs review |
| Question | Question.Course.TeacherId == CurrentUserId | ⚠️ Needs review |
| Exam | Exam.Course.TeacherId == CurrentUserId | ⚠️ Needs review |
| ExamSession | ExamSession.ScheduledBy == CurrentUserId | ⚠️ Needs review |

### 5.2 EXAMINER Ownership

| Resource | Validation | Status |
|----------|------------|--------|
| Result | Result.Session.ScheduledBy == CurrentUserId | ✅ Implemented |
| Result Review | Result.Session.ScheduledBy == CurrentUserId | ✅ Implemented |
| Result Publish | Result.Session.ScheduledBy == CurrentUserId | ✅ Implemented |

### 5.3 STUDENT Ownership

| Resource | Validation | Status |
|----------|------------|--------|
| Enrollment | Enrollment.StudentId == CurrentUserId | ✅ Implemented |
| Result | Result.StudentId == CurrentUserId | ✅ Implemented |
| Answer | Answer.Result.StudentId == CurrentUserId | ✅ Implemented |

---

## 6. Authorization Issues

### 6.1 High Priority

| Issue | Endpoint | Description | Fix Required |
|-------|----------|-------------|--------------|
| Missing ownership check | POST /api/exams | STAFF can create exam for any course | Add course ownership validation |
| Missing ownership check | POST /api/exam-sessions | STAFF can create session for any exam | Add exam ownership validation |
| Missing ownership check | All document endpoints | No course ownership validation | Add ownership validation |

### 6.2 Medium Priority

| Issue | Endpoint | Description | Fix Required |
|-------|----------|-------------|--------------|
| Inconsistent role comments | ResultsController | Comments still say TEACHER | Updated to EXAMINER ✅ |

---

## 7. Authorization Code Audit

### 7.1 Services with Ownership Validation

| Service | Ownership Check | Status |
|---------|-----------------|--------|
| ResultService | Session.ScheduledBy | ✅ Implemented |
| ExamSessionService | Exam.Course.TeacherId | ⚠️ Needs review |
| VivaSessionService | Enrollment.StudentId | ✅ Implemented |

### 7.2 Services Missing Ownership Validation

| Service | Issue | Fix Required |
|---------|-------|--------------|
| CourseService | No ownership check | Add validation |
| DocumentService | No ownership check | Add validation |
| QuestionBankService | No ownership check | Add validation |
| ExamService | No ownership check | Add validation |

---

## 8. Security Findings

### 8.1 Positive Findings

- [x] Passwords hashed with BCrypt
- [x] JWT tokens properly signed
- [x] Role-based authorization enforced
- [x] Admin demotion protection (cannot demote last admin)
- [x] Self-role change prevention
- [x] Generic error messages (no information leakage)
- [x] Rate limiting on login endpoint

### 8.2 Areas for Improvement

- [ ] Add ownership validation to all STAFF endpoints
- [ ] Add audit logging for role changes
- [ ] Add login history tracking
- [ ] Add session management (concurrent sessions)
- [ ] Add 2FA support

---

## 9. Recommendations

### 9.1 Immediate Actions

1. **Add ownership validation** to all STAFF-managed endpoints
2. **Verify** ExamSessionService ownership checks
3. **Update** remaining code comments (done ✅)

### 9.2 Short-term Actions

1. **Add audit logging** for role changes
2. **Add login history** tracking
3. **Implement** session management

### 9.3 Long-term Actions

1. **Add 2FA** support
2. **Add IP-based** access control
3. **Add API key** management for service accounts

---

## 10. Test Scenarios

### 10.1 Authentication Tests

| Test | Expected | Status |
|------|----------|--------|
| Login with valid credentials | 200 OK + JWT | ✅ |
| Login with invalid credentials | 401 Unauthorized | ✅ |
| Login with missing token | 401 Unauthorized | ✅ |
| Login with expired token | 401 Unauthorized | ✅ |
| Logout adds token to blacklist | Token invalidated | ✅ |

### 10.2 Role Tests

| Test | Expected | Status |
|------|----------|--------|
| ADMIN can access admin endpoints | 200 OK | ✅ |
| STUDENT cannot access admin endpoints | 403 Forbidden | ✅ |
| STAFF can access assigned courses | 200 OK | ⚠️ |
| STAFF cannot access unassigned courses | 403 Forbidden | ⚠️ |
| EXAMINER can review assigned results | 200 OK | ✅ |
| EXAMINER cannot review unassigned results | 403 Forbidden | ✅ |

### 10.3 Ownership Tests

| Test | Expected | Status |
|------|----------|--------|
| STUDENT views own result (published) | 200 OK | ✅ |
| STUDENT views own result (draft) | 403 Forbidden | ✅ |
| STUDENT views other student's result | 403 Forbidden | ✅ |
| STAFF creates exam for assigned course | 201 Created | ⚠️ |
| STAFF creates exam for unassigned course | 403 Forbidden | ⚠️ |

---

## 11. Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Backend Lead | Hưng | 2026-10-16 | ✅ |
| Security Review | Pending | - | - |
| Product Owner | Pending | - | - |

---

## 12. Appendix: File Changes

### 12.1 Updated Files

| File | Changes |
|------|---------|
| `IAuthService.cs` | Updated comment (TEACHER → STAFF, EXAMINER) |
| `CreateUserWithRoleRequest.cs` | Updated comment |
| `AuthController.cs` | Updated comment |
| `ResultsController.cs` | Updated comments (TEACHER → EXAMINER) |

### 12.2 Files Requiring Updates (Future)

| File | Required Changes |
|------|------------------|
| `CourseService` | Add ownership validation |
| `DocumentService` | Add ownership validation |
| `QuestionBankService` | Add ownership validation |
| `ExamService` | Add ownership validation |
| `ExamSessionService` | Verify ownership validation |

---

## 13. Revision History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-10-16 | 1.0 | Initial audit | Hưng |
