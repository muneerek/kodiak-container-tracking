# FSD-UD — User Details

Version 1.0 • 15 October 2026 • Draft • Owner: Kodiak business owner / Eknixis delivery team

## 1. Purpose and scope

Manage named Kodiak, consignee and shipper accounts with appropriate company scope and multiple configurable roles. Includes user search, creation, profile editing, activation/suspension/termination, setup invitations, and viewing login history. Roles are selected on the Create User screen; users do not need a second mapping screen to finish creation.

Related specifications: [Role Details](02-ROLE-DETAILS-FSD.md), [Role Mapping](03-USER-ROLE-MAPPING-FSD.md), [shared conventions](README.md). Public shipper/company registration is a separate onboarding workflow; it must never expose administrative role selection.

## 2. Actors and access

| Action | Required capability | Additional rule |
|---|---|---|
| List/view accounts and login history | users.manage | Internal account only in release one |
| Create account with roles | users.manage + access.manage | Assign only permitted, active, compatible roles |
| Edit ordinary profile fields | users.manage | No implicit authority to change role/company/type |
| Change roles | access.manage | Delegate through FSD-URM; user selection also requires users.manage |
| Change company membership | users.manage + access.manage | Explicit reason; revoke existing sessions |
| Suspend/terminate/reactivate | users.manage | Cannot remove the last usable access administrator |
| Send setup/reset email | users.manage | Rate limit; never disclose or set a user's password in the admin screen |

Internal/Normal does not automatically mean Admin. Effective capabilities and account/company state are checked on every request. Self-service profile edits, if added later, use a separate allowlist and endpoint.

## 3. UI specification

### UD-01 User list

Breadcrumb: Administration > Users. Primary button: **Create User**, visible/enabled only with both required capabilities. Grid columns: User ID, Name, Username, Email, User Type, Company, Roles, Account Status, Email Verified, Updated On, Actions. Long role lists show two chips and a “+N” summary with an accessible expanded view.

Search matches name, username and email. Filters: type, company, status, role, verified/unverified. Default sort: newest created first, stable ID tie-breaker. Actions: View/Edit, Manage Roles, Suspend/Reactivate, Terminate, Send Setup/Reset Email, Login History, according to permissions and state. No hard-delete action. Empty, loading, no-results and retryable-error states must be distinct. Refresh preserves filters; changing filters resets pagination.

### UD-02 Create User — role selection included

```text
Administration / Users / Create User
Personal details
[First name *                 ] [Last name                   ]
[Date of birth (optional)     ]
Account and organization
[User type *                 v] [Company *                 v]
[Email *                      ] [Username *                  ]
Access roles *
[Search compatible roles...                                  ]
[x] Booking Coordinator   [x] Report Viewer   [ ] Operations
[View selected permissions]       Selected: 2 roles
Account status: Awaiting account setup (system-controlled)
[Cancel]                                  [Create & Send Setup]
```

Company is hidden and cleared for Internal, a consignee selector for Consignee, and a shipper selector for Shipper. Lists show eligible companies only; server rechecks eligibility. Changing type clears company and selected roles with an explanatory message. Roles are searchable multi-select checkboxes/chips filtered by active status and matching user type. Show role name, code, description and read-only permission preview. No role creation embedded in this form; an authorized user can open Role Administration separately.

No password/hash field or manual email-confirmed checkbox. Pending company approval and missing eligible roles display actionable guidance and block save. Cancel with dirty fields prompts to discard. While saving, disable repeat submission, retain data on errors, and focus the first invalid field. On success, show the new user and selected roles; if invitation delivery is queued/failed, state that explicitly without claiming the email was delivered.

### UD-03 Edit/View User

Same sections, prefilled. User ID, username, user type and audit fields are read-only after creation under this proposed policy. Users.manage-only operators see roles as read-only. Role edits require access.manage and reuse the mapping service. Email change clears verification, revokes sessions and starts reverification. Company change is a separate confirmed operation requiring a reason; historical bookings retain their original parties. Display existing inactive roles with warnings; do not silently remove them while saving unrelated profile changes.

### UD-04 Account lifecycle and login history

Suspend/Terminate dialogs show the target identity, impact on sign-in/sessions, mandatory reason and Confirm/Cancel. Reactivate applies only to suspended accounts; it does not approve a pending company. Termination is final in this release. Block termination/suspension of the last usable access administrator under a serialized transaction.

Login history displays login ID, login time, recorded logout time and “No logout recorded” where null. Never label a null logout as proof of an active session. No IP/device fields are promised by the current table. Empty history is valid. All times show timezone.

## 4. Table and field specification

Existing fields from user_design.userDetails:

| Field | PostgreSQL type | Required | UI and behavior |
|---|---|---|---|
| userId | bigint identity PK | Yes, generated | Read-only; JSON string |
| userType | integer | Yes | 1 Internal, 2 Consignee, 3 Shipper; not a role |
| firstName | varchar(300) | Yes | Trim; reject blank; preserve Unicode names |
| lastName | varchar(300) | No | Trim; blank becomes null |
| dob | date | No | Valid date, not in the future; no age threshold assumed |
| emailId | varchar(150) | Yes | Trim, validate email; case-insensitive unique comparison |
| userStatus | integer | Yes | 1 Active, 2 Terminated, 3 Suspended; server-controlled workflow |
| userName | varchar(256) | Yes | Trim, nonblank, case-insensitive unique; proposed immutable after create |
| userPassword | text | Yes in baseline | Hash only; never returned. Proposed nullable until account setup completes |
| emailConfirmed | boolean | Yes | Default false; set only after valid verification/setup token |
| createOn | timestamptz | Yes | Server insertion time, immutable |
| createdBy | bigint FK userDetails | No for bootstrap | Authenticated actor for admin creation |
| updatedOn | timestamptz | Yes | Server last write time |
| updatedBy | bigint FK userDetails | No for bootstrap | Authenticated actor for admin changes |

Supporting tables: userRoleMapping stores selected roles; userLoginDetails stores loginID bigint, fkUserId bigint, loginTime/logoutTime timestamptz and audit fields; userTokens stores tokenId bigint, fkUserId bigint, purpose/tokenhash text, expireson/usedon/revokedon timestamptz and audit fields. Password/token values never appear in DTOs, notifications or audit logs.

**Required proposed schema additions, not yet applied:** nullable fkConsigneeId/fkShipperId bigint with organization/type consistency checks; reconcile company tables to numeric IDs before adding FKs. Add setupCompletedOn timestamptz nullable, version bigint default 1, and securityVersion bigint default 1. Make userPassword nullable only while setupCompletedOn is null; account setup must atomically set both. Add ACCOUNT_SETUP to token purposes. Add a transactional email outbox and append-only account/access audit store. Define lockout/rate-limit persistence during authentication implementation. Existing status defaults alone must not permit unconfigured accounts to log in.

## 5. Functions and API contracts

| Function | Proposed endpoint | Result / important rule |
|---|---|---|
| SearchUsers | GET /api/users | Paged safe summary; no hashes or DOB in grid |
| GetUser | GET /api/users/{id} | Profile, organization, role summaries, ETag |
| CreateUserWithRoles | POST /api/users | Profile + companyId + roleIds; 201 and Location |
| UpdateUserProfile | PATCH /api/users/{id} | Allowlisted fields; If-Match required |
| ChangeCompany | POST /api/users/{id}/company-change | Company, reason, If-Match; access reevaluated |
| ChangeUserStatus | POST /api/users/{id}/status-changes | Target status, reason, If-Match |
| SendSetup | POST /api/users/{id}/setup-invitations | 202 queued; idempotent/rate limited |
| SearchLoginHistory | GET /api/users/{id}/logins | Paged recorded login/logout history |

Create payload example: `{ "firstName":"Aisha", "lastName":"Khan", "userType":1, "companyId":null, "emailId":"aisha@example.com", "userName":"aisha.k", "roleIds":[7,12] }`. Server rejects attempts to supply password hashes, audit fields, emailConfirmed or unauthorized status. Use an idempotency key for create; store its result to prevent duplicate account/invitation creation on retries.

Create transaction: validate caller and fields → lock/check selected roles/company → insert unconfigured account → insert unique role mappings → store hashed setup token + email-outbox event + audit → commit. Failure rolls back all records. Email is sent after commit by an outbox worker. Delivery failure never deletes the account; admin can retry after correcting the delivery issue. Raw setup token exists only as necessary in the protected delivery payload; exclude it from logs and remove delivery payload after processing/retention expiry.

Account setup is single-use, expiring, rate-limited and verifies mailbox ownership. It atomically sets a maintained password hash, setup completion and verification state. Sign-in requires Active, setup complete, verification policy satisfied, and company eligibility. Permission-free users get no operational access. Password policy and token lifetimes must be configurable and specified in the authentication FSD; they are not invented database defaults here.

## 6. Validation and failures

Duplicate email/username → 409 with field-level feedback in this authorized admin workflow. Future DOB/blank first name/oversized field → 400. Stale version → 412 and offer reload, not silent overwrite. A selected role deactivated or changed since loading → 409, refresh options and require resubmission. No eligible role → block operational account creation. Unexpected database failure → rollback, generic message and trace ID. Do not disclose password hashes, raw tokens or another company's details in any response.

## 7. Acceptance criteria

| ID | Scenario and expected result |
|---|---|
| UD-AC01 | Create internal user with two active internal roles: one account and two mappings committed; setup email queued |
| UD-AC02 | Invalid/inactive/incompatible role: no user, mapping, token or email event committed |
| UD-AC03 | Duplicate normalized username/email: one account only, clear conflict response |
| UD-AC04 | Shipper user without shipper company: rejected; internal user with company: rejected |
| UD-AC05 | users.manage without access.manage: cannot create a role-bearing account or alter assignments |
| UD-AC06 | Suspension/termination: existing authorization becomes invalid immediately; history preserved |
| UD-AC07 | Attempt to disable last usable access administrator: rejected, including concurrent requests |
| UD-AC08 | Concurrent profile edits: stale client receives 412 without lost updates |
| UD-AC09 | Email delivery failure: account remains unconfigured; retry does not duplicate user |
| UD-AC10 | API/UI/audit inspection: no password hashes or raw tokens exposed; audit actor matches caller |
| UD-AC11 | Small-screen/keyboard use: all fields, role selector, errors and buttons remain accessible |
| UD-AC12 | Email/company changes: verification/session rules enforced and business history unchanged |

## 8. Dependencies and exclusions

Requires FSD-RD/FSD-URM, company eligibility queries, transactional delivery, authentication services, concurrency and immutable audit infrastructure. No hard delete, bulk import, payroll/HR functions, MFA, SSO or external delegated user administration is included. Review and approve proposed schema gaps before EF implementation.
