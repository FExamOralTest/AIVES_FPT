# AIVES Role Migration Documentation

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Overview

This document outlines the role migration from legacy role system to the new RBAC model.

### 1.1 Migration Status

| Item | Status | Notes |
|------|--------|-------|
| UserRole Enum | ✅ Complete | STAFF, EXAMINER added |
| Database Column | ✅ Complete | PostgreSQL handles enum |
| JWT Claims | ✅ Complete | Role in claims |
| Authorization | ✅ Complete | Role policies defined |
| Code References | 🔄 In Progress | Some comments still reference TEACHER |

---

## 2. Role System Changes

### 2.1 Before Migration

```csharp
// Old System
public enum UserRole
{
    ADMIN = 0,
    STUDENT = 1,
    TEACHER = 2  // Legacy
}
```

### 2.2 After Migration

```csharp
// New System
public enum UserRole
{
    ADMIN = 0,
    STUDENT = 1,
    STAFF = 2,     // Replaces TEACHER
    EXAMINER = 3   // New role
}
```

### 2.3 Role Mapping

| Old Role | New Role | Migration |
|----------|----------|-----------|
| ADMIN | ADMIN | No change |
| STUDENT | STUDENT | No change |
| TEACHER | STAFF | Auto-migrated |
| (new) | EXAMINER | Added |

---

## 3. Current Code References to TEACHER

### 3.1 Files with TEACHER References

| File | Type | Issue |
|------|------|-------|
| `IAuthService.cs` | Interface comment | Outdated comment |
| `AuthController.cs` | Controller comment | Outdated comment |
| `CreateUserWithRoleRequest.cs` | DTO comment | Outdated comment |
| `ResultsController.cs` | XML comments | Outdated comments |
| Migration files | Historical | No action needed |

### 3.2 Required Updates

**1. IAuthService.cs** (Line 15)
```csharp
// Before:
/// <summary>Chỉ ADMIN — tạo user với role tuỳ chọn (ADMIN, TEACHER, STUDENT).</summary>

// After:
/// <summary>Chỉ ADMIN — tạo user với role tuỳ chọn (ADMIN, STAFF, EXAMINER, STUDENT).</summary>
```

**2. AuthController.cs** (Line 108)
```csharp
// Before:
/// Admin tạo user với role tuỳ chọn (ADMIN, TEACHER, STUDENT).

// After:
/// Admin tạo user với role tuỳ chọn (ADMIN, STAFF, EXAMINER, STUDENT).
```

**3. CreateUserWithRoleRequest.cs** (Line 7)
```csharp
// Before:
/// Chỉ ADMIN mới được dùng — tạo user với role tuỳ chọn (ADMIN, TEACHER, STUDENT).

// After:
/// Chỉ ADMIN mới được dùng — tạo user với role tuỳ chọn (ADMIN, STAFF, EXAMINER, STUDENT).
```

**4. ResultsController.cs** (Multiple lines)
```csharp
// Before:
/// [TEACHER] Lấy tất cả kết quả PENDING_REVIEW...

// After:
/// [EXAMINER] Lấy tất cả kết quả PENDING_REVIEW...
```

---

## 4. Database Migration

### 4.1 Migration Status

**Migration File:** `20261016100236_RestructureRolesAndAddAuditFields.cs`

This migration:
- ✅ Added EXAMINER role
- ✅ Migrated TEACHER to STAFF
- ✅ Added audit fields

### 4.2 Data Verification

Run this to verify migration:

```sql
-- Check role distribution
SELECT role, COUNT(*) as user_count 
FROM users 
GROUP BY role;

-- Check for any remaining TEACHER references
SELECT * FROM users WHERE role = 'TEACHER';
```

### 4.3 Rollback Plan

If rollback is needed:

```sql
-- Rollback: Convert STAFF back to TEACHER
UPDATE users SET role = 'TEACHER' WHERE role = 'STAFF';

-- Remove EXAMINER (set to STUDENT as fallback)
UPDATE users SET role = 'STUDENT' WHERE role = 'EXAMINER';
```

> **Warning:** Rollback will lose examiner assignments.

---

## 5. Authorization Changes

### 5.1 Before

```csharp
// Old authorization
[Authorize(Roles = "TEACHER,ADMIN")]
public class ResultsController : ControllerBase
```

### 5.2 After

```csharp
// New authorization
[Authorize(Roles = "EXAMINER,ADMIN")]
public class ResultsController : ControllerBase
```

### 5.3 Impact Analysis

| Endpoint | Old Role | New Role | Impact |
|----------|----------|----------|--------|
| Results/pending | TEACHER, ADMIN | EXAMINER, ADMIN | **Breaking** |
| Results/session/{id} | TEACHER, ADMIN | EXAMINER, ADMIN | **Breaking** |
| Results/{id} | TEACHER, ADMIN | EXAMINER, ADMIN | **Breaking** |
| Results/{id}/review | TEACHER, ADMIN | EXAMINER, ADMIN | **Breaking** |
| Results/{id}/publish | TEACHER, ADMIN | EXAMINER, ADMIN | **Breaking** |

---

## 6. New EXAMINER Role

### 6.1 Purpose

EXAMINER is a separate role from STAFF for better separation of duties:

- **STAFF:** Manages courses, exams, sessions (operational)
- **EXAMINER:** Reviews and publishes results (assessment)

### 6.2 Permissions

| Feature | STAFF | EXAMINER |
|---------|-------|----------|
| Manage Course | ✅ Own | ❌ |
| Manage Exam | ✅ Own | ❌ |
| Manage Session | ✅ Own | ❌ |
| Review Results | ❌ | ✅ Own |
| Publish Results | ❌ | ✅ Own |

### 6.3 Assignment

EXAMINER can be assigned to courses via:

```csharp
// In ExamSession or separate assignment table
public class ExaminerAssignment
{
    public int ExaminerId { get; set; }
    public int CourseId { get; set; }
    public DateTime AssignedAt { get; set; }
}
```

---

## 7. Testing Checklist

### 7.1 Pre-Migration

- [ ] Backup database
- [ ] Document current user roles
- [ ] Identify all TEACHER accounts

### 7.2 Post-Migration

- [ ] Verify all TEACHER → STAFF migration
- [ ] Verify EXAMINER role exists
- [ ] Test STAFF login and access
- [ ] Test EXAMINER login and access
- [ ] Test authorization enforcement
- [ ] Test ownership validation
- [ ] Verify old TEACHER tokens still work (if using rolling refresh)

### 7.3 Frontend Changes

- [ ] Update role dropdown options
- [ ] Update permission checks
- [ ] Update UI labels (Teacher → Staff)
- [ ] Test role-based UI rendering

---

## 8. Breaking Changes

### 8.1 Backend

| Change | Impact | Mitigation |
|--------|--------|------------|
| Role name change | High | Update all references |
| Authorization change | High | Update policies |
| API behavior | Medium | Version API if needed |

### 8.2 Frontend

| Change | Impact | Mitigation |
|--------|--------|------------|
| Role labels | Low | Update UI text |
| Permission checks | Medium | Update logic |
| Redirect logic | Low | Update conditions |

---

## 9. Migration Commands

### 9.1 Apply Migration

```bash
dotnet ef database update
```

### 9.2 Verify Migration

```bash
dotnet ef migrations list
```

### 9.3 Rollback (if needed)

```bash
# Rollback last migration
dotnet ef migrations rollback

# Or rollback to specific migration
dotnet ef migrations revert <migration-name>
```

---

## 10. FAQ

### Q1: What happens to existing TEACHER accounts?

**A:** They are automatically migrated to STAFF role. Users can still login with existing credentials.

### Q2: Can one user have both STAFF and EXAMINER roles?

**A:** Currently no. Each user has one role. This can be extended if business need arises.

### Q3: How do we assign EXAMINER to specific courses?

**A:** Currently through ExamSession.ScheduledBy. A separate examiner assignment table can be added.

### Q4: What about users who need both teaching and examining duties?

**A:** Create two accounts - one with STAFF role, one with EXAMINER role.

### Q5: Can STUDENT self-register as EXAMINER?

**A:** No. Self-registration always creates STUDENT role. Only ADMIN can create privileged roles.

---

## 11. Contacts

| Role | Contact |
|------|---------|
| Migration Lead | Hưng (BE1) |
| Backend Support | Team Backend |
| Frontend Support | Team Frontend |
| Database Admin | Infrastructure Team |

---

## 12. Revision History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-10-16 | 1.0 | Initial documentation | Hưng |
