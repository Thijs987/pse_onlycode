# Security Implementation Summary

## Overview

This document captures the current backend security implementation and the full authentication/authorization flow in the project.

The backend now supports:

- JWT-based authentication with issuer/audience validation
- refresh token lifecycle with rotation and revocation
- CSRF double-submit protection
- HTTPS enforcement and tight forwarded header trust
- password hashing with PBKDF2
- account lockout and persistent rate limiting
- frontend origin allow list via CORS configuration
- explicit security headers for API and frontend defense-in-depth
- email verification with hashed verification tokens
- database-backed audit logging for authentication/security events

## Security Implementation

### 1. JWT Authentication

- JWTs are signed using HMAC-SHA256.
- Tokens include standard claims and optional user context.
- Access tokens are configured to expire in 15 minutes by default.
- Validation enforces:
  - issuer
  - audience
  - signature
  - expiration
  - 30-second clock skew tolerance
- Tokens are accepted from either the `Authorization: Bearer` header or the `access_token` cookie.

### 2. Security Headers

- The API now adds explicit security headers in middleware for defense in depth:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: no-referrer`
  - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self';`
- These headers are set before most request processing, and they protect both API responses and any served frontend content.

### 3. CORS / Frontend Origin Allow List

- Frontend requests are allowed only from configured origins via `AppSettings:AllowedOrigins`.
- The server reads allowed origins from configuration and applies a strict CORS policy.
- This is the place to register your frontend host(s) when the client needs browser access to the API.
- If cookie-based auth is used, the policy should be extended with `AllowCredentials()` and exact origins.

### 4. Refresh Token Lifecycle

- Refresh tokens are generated as secure random values.
- The backend stores only a PBKDF2-hashed version of the refresh token.
- Each refresh request rotates the token:
  - the previous token is revoked
  - a new refresh token is issued and stored hashed
- Logout explicitly revokes the active refresh token.
- Default refresh token lifetime is 30 days (configurable).

### 5. CSRF Protection

- The backend issues a readable `csrf_token` cookie.
- Client code must send the same token in the `X-CSRF-Token` header for unsafe requests.
- Middleware compares cookie versus header for POST/PUT/PATCH/DELETE.
- Authentication endpoints that create tokens are exempted as needed.

### 6. HTTPS and Forwarded Header Trust

- HTTP is redirected to HTTPS unless `AppSettings:AllowHttp` is enabled.
- HSTS is enabled in non-development environments.
- Production JWT validation requires HTTPS metadata.
- `TrustForwardedHeaders` must be explicitly enabled via configuration.
- When enabled, only `X-Forwarded-For` and `X-Forwarded-Proto` are accepted.
- Forwarded header trust is limited to loopback proxies by default.
- `ForwardLimit = 1` is enforced to avoid header chain abuse.

### 7. Password and Token Hashing

- Passwords are hashed with PBKDF2-HMAC-SHA256.
- Implementation uses 100,000 iterations and a 16-byte random salt.
- Verification uses fixed-time comparison.
- Email verification tokens are now hashed before storage.
- Verification checks compare the provided plaintext token with the stored hash.

### 8. Account Lockout and Rate Limiting

- Login lockout after 5 failed attempts.
- Lockout duration is 15 minutes.
- Registration is limited to 3 attempts per 30 minutes per IP.
- Login is limited to 5 attempts per 15 minutes per email.
- The implementation now uses database-backed persistence for rate limiting, so limits survive server restarts and are shared across instances.

### 9. Persistent Audit Logging

- Authentication and security events are now recorded in a dedicated `AuditLogs` table.
- Audit log entries include action, email, success/failure, reason, timestamp, and IP address.
- Audit logs are stored in the database so they are durable, queryable, and available for security review.

### 10. Email Verification Flow

- Registration creates a new user with `IsEmailVerified = false`.
- A secure verification token is generated and hashed before storage.
- The plaintext token is sent only in the email verification link.
- Verification endpoint validates both expiry and hash match.
- Successful verification marks the email as verified and clears the token.

## Authentication + Security Flow

1. **Registration**
   - Validate inputs.
   - Normalize and validate email.
   - Hash password.
   - Generate verification token and store its hash.
   - Send verification email with plaintext token in the URL.

2. **Email verification**
   - User clicks verification link.
   - Backend validates email and token.
   - If valid, mark email verified and remove stored token.

3. **Login**
   - Validate credentials.
   - Ensure email is verified.
   - Enforce lockout and rate limiting.
   - Issue `access_token`, `refresh_token`, and `csrf_token` cookies.

4. **Protected request**
   - JWT is validated by authentication middleware.
   - Authorization allows access to secured endpoints.
   - Unsafe requests require CSRF header match.

5. **Token refresh**
   - Validate refresh token from cookie.
   - Validate CSRF header.
   - Rotate refresh token and issue new access token.
   - Revoke the old refresh token.

6. **Logout**
   - Revoke current refresh token.
   - Clear auth cookies.
   - Prevent reuse of the old refresh token.

## Key Files

- `Backend/src/Program.cs`
- `Backend/src/Api/AuthController.cs`
- `Backend/src/Infrastructure/Services/AuthService.cs`
- `Backend/src/Infrastructure/Services/DbAuditService.cs`
- `Backend/src/Infrastructure/Services/DbRateLimitService.cs`
- `Backend/src/Application/PasswordHasher.cs`
- `Backend/src/Infrastructure/Persistance/AppDbContextFactory.cs`
- `Backend/src/Migrations/20260615104715_AddRefreshToken.cs`
- `Backend/src/Migrations/20260618123015_AddAuditAndRateLimit.cs`
- `Backend/tests/Backend.Tests/AuditAndRateLimitIntegrationTests.cs`
- `Backend/tests/Backend.Tests/SecurityFeaturesIntegrationTests.cs`

## Tests Covered

The current security test suite covers:

- JWT token claims and validation
- signature failure with wrong key
- issuer and audience enforcement
- expiry validation and clock skew tolerance
- refresh token hashing
- refresh token rotation
- refresh token revocation
- invalid refresh token handling
- email verification requirement on login
- account lockout behavior
- email verification success
- expired verification token rejection
- password hashing and verification
- rate limiting enforcement
- persistent audit logging to the database
- DB-backed rate limit persistence and reset behavior

## Deployment Notes

- Use a strong `Jwt:Key` of at least 32 bytes.
- Set production HTTPS certificates properly.
- Keep `AppSettings:AllowHttp` disabled in production.
- Enable `AppSettings:TrustForwardedHeaders` only behind a trusted proxy.
- Limit trusted proxy IPs to the actual reverse proxy.
- Replace in-memory audit/rate-limit components with distributed services for production.

## Current Status

The backend now contains a hardened authentication flow with secure token storage, encrypted cookies, CSRF protection, strong hashing, strict forwarded header trust, and verified integration tests.
