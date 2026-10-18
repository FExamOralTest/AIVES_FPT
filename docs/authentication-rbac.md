# AIVES Authentication & RBAC Documentation

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Authentication Overview

### 1.1 Technology Stack

| Component | Technology | Status |
|-----------|------------|--------|
| Password Hashing | BCrypt | ✅ Secure |
| Token | JWT (HS256) | ✅ Secure |
| Auth Protocol | Bearer Token | ✅ Standard |
| Password Policy | StrongPassword Attribute | ✅ Implemented |

### 1.2 Configuration

**File:** `appsettings.json`

```json
{
  "Jwt": {
    "Key": "your-super-secret-jwt-key-min-32-chars",
    "Issuer": "AIVES",
    "Audience": "AIVES.Client",
    "ExpirationMinutes": 60
  }
}
```

> **Note:** Key must be at least 32 characters for HS256.

---

## 2. Login/JWT Flow

### 2.1 Login Endpoint

```
POST /api/auth/login
Content-Type: application/json

Request:
{
  "identifier": "username OR email",
  "password": "userpassword"
}

Response 200 OK:
{
  "accessToken": "eyJhbGc...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "userId": 1,
    "username": "john",
    "fullName": "John Doe",
    "email": "john@example.com",
    "role": "STUDENT"
  }
}

Response 401 Unauthorized:
{ "message": "Invalid username or password." }
```

### 2.2 JWT Claims

```csharp
var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
    new Claim(ClaimTypes.Name, user.Username),
    new Claim(ClaimTypes.Role, user.Role.ToString()),
};
```

| Claim Type | Value | Example |
|------------|-------|---------|
| NameIdentifier | UserId | "1" |
| Name | Username | "john" |
| Role | UserRole | "STUDENT" |

### 2.3 Token Expiration

- Default: 60 minutes
- Configurable via `Jwt:ExpirationMinutes`
- Token blacklist implemented for logout

---

## 3. Role Definitions

### 3.1 Role Enum

**File:** `Aives.Domain.Enums.UserRole.cs`

```csharp
public enum UserRole
{
    ADMIN = 0,
    STUDENT = 1,
    STAFF = 2,
    EXAMINER = 3
}
```

### 3.2 Role Responsibilities

| Role | Responsibilities |
|------|------------------|
| **ADMIN** | System administration, user management, configuration |
| **STAFF** | Manage Course, Document, Question Bank, Exam, Session in assigned scope |
| **EXAMINER** | Review and publish exam Results in assigned scope |
| **STUDENT** | Enroll, take viva exams, view own published results |

### 3.3 Role Matrix

| Feature | ADMIN | STAFF | EXAMINER | STUDENT |
|---------|-------|-------|----------|---------|
| Create Course | ✅ | ❌ | ❌ | ❌ |
| Assign Teacher | ✅ | ❌ | ❌ | ❌ |
| Manage Course | ✅ | Assigned only | ❌ | ❌ |
| Manage Documents | ✅ | Assigned only | ❌ | ❌ |
| Manage Questions | ✅ | Assigned only | ❌ | ❌ |
| Create Exam | ✅ | Assigned only | ❌ | ❌ |
| Manage Sessions | ✅ | Assigned only | ❌ | ❌ |
| Enroll in Exam | ❌ | ❌ | ❌ | ✅ |
| Take Viva | ❌ | ❌ | ❌ | ✅ |
| View Own Results | ❌ | ❌ | ❌ | Published only |
| Review Results | ✅ | ❌ | Assigned only | ❌ |
| Publish Results | ✅ | ❌ | Assigned only | ❌ |
| Manage Users | ✅ | ❌ | ❌ | ❌ |

---

## 4. Authorization Policies

### 4.1 Policy Configuration

**File:** `Program.cs`

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("ADMIN"));
    
    options.AddPolicy("StaffOnly", policy => 
        policy.RequireRole("STAFF", "ADMIN"));
    
    options.AddPolicy("ExaminerOnly", policy => 
        policy.RequireRole("EXAMINER", "ADMIN"));
    
    options.AddPolicy("StudentOnly", policy => 
        policy.RequireRole("STUDENT"));
});
```

### 4.2 Usage in Controllers

```csharp
// Admin only
[Authorize(Roles = "ADMIN")]
[HttpGet("admin/users")]

// Examiner and Admin
[Authorize(Roles = "EXAMINER,ADMIN")]
[HttpGet("results/pending")]

// Student only
[Authorize(Roles = "STUDENT")]
[HttpPost("viva-sessions/{sessionId}/start")]

// Authenticated any role
[Authorize]
[HttpGet("auth/me")]
```

### 4.3 Anonymous Endpoints

```csharp
[AllowAnonymous]
[HttpPost("auth/register")]

[AllowAnonymous]
[HttpPost("auth/login")]
```

---

## 5. Ownership Authorization

### 5.1 STAFF Ownership Rules

- Can only access Courses assigned to them
- Can only manage Documents in assigned Courses
- Can only manage Questions in assigned Courses
- Can only create Exams in assigned Courses
- Can only manage Sessions for assigned Courses

**Implementation:** Service layer validates `Course.ScheduledBy == currentUserId`

### 5.2 EXAMINER Ownership Rules

- Can only review Results for Sessions they are assigned to
- Can only publish Results in their scope
- Cannot modify Results outside their assignment

**Implementation:** Service validates `ExamSession.ScheduledBy == currentUserId`

### 5.3 STUDENT Ownership Rules

- Can only view own Results
- Can only view Results in PUBLISHED status
- Cannot access other Students' audio, transcripts, or scores

**Implementation:** Service validates `Result.StudentId == currentUserId && Result.Status == PUBLISHED`

---

## 6. Security Features

### 6.1 Password Security

- **Algorithm:** BCrypt (cost factor 10)
- **Never stored in plaintext**
- **Verification:** `BCrypt.Net.BCrypt.Verify(password, hash)`

**File:** `Aives.Infrastructure.Auth.PasswordHasher.cs`

```csharp
public string Hash(string password)
{
    return BCrypt.Net.BCrypt.HashPassword(password);
}

public bool Verify(string password, string passwordHash)
{
    return BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
```

### 6.2 JWT Security

- **Algorithm:** HMAC-SHA256
- **Key:** Minimum 32 characters
- **Issuer/Audience:** Configurable
- **Expiration:** Default 60 minutes
- **Blacklist:** Token revocation on logout

### 6.3 Rate Limiting

**Login Endpoint:** `EnableRateLimiting("LoginPolicy")`

Prevents brute force attacks on login.

### 6.4 Error Handling

| Scenario | Response | Status |
|----------|----------|--------|
| Invalid credentials | Generic message | 401 |
| Missing token | Generic message | 401 |
| Expired token | Generic message | 401 |
| Insufficient role | No details | 403 |
| Resource not found | Generic message | 404 |

> **Security Note:** Login errors don't reveal whether username or password is wrong.

---

## 7. API Endpoints

### 7.1 Authentication

| Endpoint | Method | Auth | Roles |
|----------|--------|------|-------|
| `/api/auth/register` | POST | Anonymous | - |
| `/api/auth/login` | POST | Anonymous | Rate Limited |
| `/api/auth/me` | GET | JWT | All |
| `/api/auth/logout` | POST | JWT | All |
| `/api/auth/admin/create-user` | POST | JWT | ADMIN |
| `/api/auth/admin/users` | GET | JWT | ADMIN |
| `/api/auth/admin/users/{id}` | PUT | JWT | ADMIN |

### 7.2 Course Management

| Endpoint | Method | Auth | Roles | Ownership |
|----------|--------|------|-------|-----------|
| `/api/admin/courses` | GET | JWT | ADMIN | - |
| `/api/admin/courses/{id}` | GET | JWT | ADMIN | - |
| `/api/admin/courses` | POST | JWT | ADMIN | - |
| `/api/admin/courses/{id}/assign-teacher` | PUT | JWT | ADMIN | - |
| `/api/teacher/courses` | GET | JWT | STAFF, ADMIN | Assigned only |

### 7.3 Exam & Results

| Endpoint | Method | Auth | Roles | Ownership |
|----------|--------|------|-------|-----------|
| `/api/results/pending` | GET | JWT | EXAMINER, ADMIN | Assigned |
| `/api/results/session/{sessionId}` | GET | JWT | EXAMINER, ADMIN | Assigned |
| `/api/results/{resultId}/review` | PUT | JWT | EXAMINER, ADMIN | Assigned |
| `/api/results/{resultId}/publish` | POST | JWT | EXAMINER, ADMIN | Assigned |
| `/api/student/results/{sessionId}` | GET | JWT | STUDENT | Own only |

---

## 8. User Management

### 8.1 Registration

```
POST /api/auth/register
Request:
{
  "username": "john",
  "password": "SecurePassword123!",
  "email": "john@example.com",
  "fullName": "John Doe"
}

Response 201 Created:
{
  "userId": 1,
  "username": "john",
  "email": "john@example.com",
  "fullName": "John Doe",
  "role": "STUDENT"
}
```

> **Note:** Self-registration always creates STUDENT role.

### 8.2 Admin User Creation

```
POST /api/auth/admin/create-user
Request:
{
  "username": "teacher1",
  "password": "SecurePassword123!",
  "email": "teacher1@example.com",
  "fullName": "Teacher One",
  "role": "STAFF"
}

Response 201 Created:
{
  "userId": 2,
  "username": "teacher1",
  "email": "teacher1@example.com",
  "fullName": "Teacher One",
  "role": "STAFF"
}
```

> **Note:** Only ADMIN can create users with STAFF or EXAMINER roles.

### 8.3 Role Update

```
PUT /api/auth/admin/users/1
Request:
{
  "role": "EXAMINER"
}

Response 200 OK:
{
  "userId": 1,
  "username": "john",
  "role": "EXAMINER"
}
```

**Restrictions:**
- Cannot demote last ADMIN
- Cannot change own role

---

## 9. Token Blacklist

### 9.1 Logout

When user logs out, token is added to blacklist:

```csharp
await _blacklistService.AddToBlacklistAsync(token, DateTime.UtcNow.AddDays(1));
```

### 9.2 Validation

JWT middleware checks token against blacklist before processing requests.

---

## 10. Audit Logging

All authentication events are logged:

| Event | Log Level | PII |
|-------|-----------|-----|
| Login success | Information | No |
| Login failure | Warning | No |
| Logout | Information | No |
| User created | Information | No |
| Role changed | Information | No |
| Unauthorized access | Warning | No |

---

## 11. Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `Jwt__Key` | JWT signing key | Yes |
| `Jwt__Issuer` | Token issuer | No (default: AIVES) |
| `Jwt__Audience` | Token audience | No (default: AIVES.Client) |
| `Jwt__ExpirationMinutes` | Token lifetime | No (default: 60) |

---

## 12. Security Checklist

- [x] Passwords hashed with BCrypt
- [x] JWT signed with HS256
- [x] Token expiration configured
- [x] Token blacklist implemented
- [x] Rate limiting on login
- [x] Role-based authorization
- [x] Ownership validation
- [x] No password in logs
- [x] Generic error messages
- [x] Admin demotion protection
