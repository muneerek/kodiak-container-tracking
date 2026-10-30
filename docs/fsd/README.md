# Kodiak — User administration functional specifications

Version 1.0 • 15 October 2026 • Draft for business and technical review

Prepared for Eknixis / Kodiak. Platform: Vue.js, ASP.NET Web API, PostgreSQL; future EF Code First implementation. These documents specify behavior, not completed software. No EF classes, migrations or screens are created by this deliverable.

1. [User Details FSD](01-USER-DETAILS-FSD.md): user list, creation with role selection, profile editing, account lifecycle and authentication-related records.
2. [Role Details FSD](02-ROLE-DETAILS-FSD.md): configurable role master, permission selection and role lifecycle.
3. [User–Role Mapping FSD](03-USER-ROLE-MAPPING-FSD.md): multiple assignments, integrated create/edit experience, authorization resolution and audit.

## Baseline and precedence

The latest table-name baseline is `database/007_user_structure_proposal.sql`: userDetails, userLoginDetails, userTokens, userRoles and userRoleMapping. The earlier UUID-based full schema and diagrams are not the numeric-ID implementation target. Dynamic permission concepts from migration 005 are retained, but their tables need adaptation to the latest names and numeric keys.

Each document marks **existing** fields versus **proposed additions**. These FSDs do not imply those additions have been applied. User type is a company access boundary; it is not an administrator role. Roles and their permissions are configurable. Role selection is embedded in Create User and Edit User, with a dedicated mapping page as an alternate entry point to the same save logic.

## Shared conventions

- User types: 1 Internal/Normal, 2 Consignee, 3 Shipper. Statuses: 1 Active, 2 Terminated, 3 Suspended. Company approval remains separate from account status.
- All APIs require authentication except explicitly designed signup/authentication endpoints. Administrative functions require capabilities, never a role-name comparison.
- Identifiers backed by bigint are JSON strings. Dates of birth are date-only; audit timestamps are timezone-aware, displayed in the viewer's timezone with a visible timezone label.
- All tables use createOn, createdBy, updatedOn and updatedBy. Trusted server context sets actors; clients cannot set audit fields.
- Errors use a consistent problem-details body with code, message, fieldErrors and traceId. Validation is 400, unauthenticated 401, unauthorized 403, unavailable/not-found resource 404, uniqueness/business conflict 409, stale edit 412. Never expose SQL errors or hashes.
- Updates use an opaque ETag/If-Match version. Proposed version columns and atomic service transactions are required; updatedOn alone is not the concurrency mechanism.
- List defaults: 25 rows, options 25/50/100; allowlisted sorting and server-side filters. Responsive forms, visible labels, keyboard operation, inline errors and screen-reader status messages are required.
- Bootstrap the first internal access administrator through a controlled setup procedure, not public registration. Preserve at least one active internal user with effective users.manage and access.manage capabilities.

## Proposed decisions to confirm before implementation

These are working defaults, not claims of customer approval: internal administrators manage all accounts; external users cannot administer colleagues in release one; DOB is optional; creation uses a one-time account-setup email; terminated accounts cannot be reactivated; email changes require reverification; role codes and user types are immutable after creation. Custom role and permission editing are included, while bulk assignment, role inheritance, explicit deny rules, MFA and SSO are outside this release.

If transactional email is unavailable, the account-setup delivery process must be agreed before release. Do not silently replace it with emailed plaintext passwords.
