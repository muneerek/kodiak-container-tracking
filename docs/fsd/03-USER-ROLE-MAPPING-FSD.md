# FSD-URM — User–Role Mapping

Version 1.0 • 15 October 2026 • Draft • Scope: many-to-many role assignment

## 1. Purpose

Assign multiple roles to a user and reuse each role across multiple users. The same assignment service supports Create User, Edit User and a dedicated Manage Roles page. There must be no duplicate or conflicting implementation of role assignment between screens.

Related: [User Details](01-USER-DETAILS-FSD.md), [Role Details](02-ROLE-DETAILS-FSD.md), [shared conventions](README.md). Roles define actions; user type/company determines data boundaries. Assigning a role never transfers company ownership or changes user type.

## 2. Actor and preconditions

Only active internal operators with access.manage and users.manage may select another account and change its assignments in this release. A caller cannot gain these capabilities by adding a role to the request payload. Public registration never exposes this functionality.

User must exist, be within administrator scope, and not be terminated. Suspended accounts may have assignments prepared but gain no operational access until reactivated. User and selected roles must have matching business types. Roles must be active for new assignment. At least one eligible role with effective capabilities is required when creating an operational account; removing all roles later is an explicit “Remove all access” action, with confirmation and reason. It does not delete the account or implicitly change status.

## 3. UI

### URM-01 Integrated role selector

On Create User, show searchable multi-select **Roles*** beneath company selection. Offer only active compatible roles with valid grants. Show name/code/description and permission preview. Changing user type clears selected roles. On create, selections are in-memory form state until the full account is saved; no mapping request is sent before user creation.

On Edit User, load existing assignments. Inactive existing roles appear as selected disabled-role badges with a warning and a Remove action; they are not offered as new additions. Ordinary profile edits preserve existing assignments, including inactive ones. Changing assignments requires an explicit mapping save through the authorized service.

### URM-02 Dedicated Manage Roles

```text
Administration / Users / Aisha Khan / Manage Roles
User: Aisha Khan    Username: aisha.k    Status: Active
Type: Internal     Company: Kodiak      Version: read-only
Current/available roles
[x] Booking Coordinator     Active       [View permissions]
[x] Report Viewer           Active       [View permissions]
[ ] Tracking Officer        Active       [View permissions]
[x] Legacy Operations       Inactive     [Remove]
Effective permissions preview: bookings.read, bookings.review, reports.read
[Change reason *                                            ]
[Cancel] [Remove all access]                  [Review & Save]
```

Review summary shows roles added/removed, newly granted/lost capabilities and whether removal has no effect because another role supplies the same capability. Save requires confirmation of this concrete change. No-change save is disabled. Preview is informational; the server recalculates everything during commit.

Use the same filters and accessible selection component on Create/Edit/Manage Roles. Show loading, no eligible roles, unavailable role and stale-user errors explicitly. After success, reload assigned/effective roles and show the new version. Do not discard unrelated unsaved profile fields when navigating between sections.

## 4. Table structure

Existing userRoleMapping:

| Field | Type | Meaning |
|---|---|---|
| mappingId | bigint identity PK | Stable assignment record identity |
| fkUserId | bigint FK userDetails.userId | Assigned user |
| fkRoleId | integer FK userRoles.roleId | Assigned dynamic role |
| createOn | timestamptz | Assignment entry time |
| createdBy | bigint FK userDetails.userId | Assigning operator |
| updatedOn | timestamptz | Latest mapping write time |
| updatedBy | bigint FK userDetails.userId | Latest actor |

Unique(fkUserId,fkRoleId) prevents duplicate assignments. Index fkRoleId supports reverse membership lookup. Required addition: userType integer and composite foreign keys to unique(userId,userType) and unique(roleId,userType), preventing cross-type assignments at database level. This is not yet implemented in SQL 007.

For revocation, delete the active mapping in the same transaction as an append-only audit event recording the removed mapping/user/role IDs, previous values, actor/time/reason. Deletion of a mapping is allowed; deletion of audit history is not. This avoids introducing a second inactive flag that could disagree with role/user status. Reassignment creates a new mapping with new creation metadata. Update counters on the parent user aggregate rather than overwriting retained mappings just to change timestamps.

Proposed userDetails.version protects profile-plus-role edits; securityVersion supports revocation. Role-grant changes must also invalidate all affected users or be resolved from authoritative permissions on every protected request. All user IDs in API JSON are strings.

## 5. Functional operations

| Function | Inputs | Output / behavior |
|---|---|---|
| GetAssignedRoles | User ID | Current role IDs, metadata, inactive flags, user ETag |
| GetAssignableRoles | User ID or draft userType/company | Active compatible roles caller may assign |
| PreviewAssignment | User ID, desired role IDs | Added/removed roles and effective-capability delta |
| ReplaceAssignments | User ID, desired role IDs, reason, If-Match | Atomic final assignment set and new ETag |
| CreateUserWithRoles | User DTO, desired role IDs | One transaction containing user and mappings |
| ResolveEffectiveAccess | Authenticated user ID | Active capabilities plus separately evaluated company scope |

Proposed endpoints: GET /api/users/{id}/roles; GET /api/roles/assignable?userType=1; POST /api/users/{id}/roles/preview; PUT /api/users/{id}/roles. Creation remains POST /api/users, not a second client-side mapping call. Example replacement body: `{ "roleIds":[7,12], "reason":"Assigned to booking operations" }` with If-Match header.

Use PUT replacement semantics: submitted IDs are the desired final set. Omitted roleIds is invalid on the dedicated mapping endpoint, while an empty array means explicit removal of all access. Reject duplicate IDs rather than creating duplicates. On generic profile updates, omitted roles means unchanged; do not interpret missing fields as empty assignments.

## 6. Transaction and concurrency

1. Authenticate caller and check required capabilities from authoritative state.
2. Lock target user; compare If-Match with current aggregate version. Reject terminated targets.
3. Read/lock affected roles consistently; validate existence, compatibility, active status for additions and capability eligibility. Retained inactive mappings are allowed but grant nothing.
4. Calculate final effective grants and last-administrator invariant. Use a shared access-control lock/serializable strategy covering user status, role grants, role activation and mappings so concurrent changes cannot each remove the last administrator.
5. Insert new mappings, delete removed mappings, keep unchanged rows untouched. Write audit entries and bump user version/securityVersion in the same transaction.
6. Commit; ensure subsequent protected requests use updated authorization. Do not depend solely on delayed cache events for revocation correctness.
7. Return current assignments/effective permissions/version. If a role changes during save, retry safely at the service layer or return conflict; never accept partially validated grants.

For account creation, this sequence occurs inside the user-creation transaction. A failed assignment rolls back the user, token and outbox records too. Mapping writes must not be exposed directly to browsers or database users.

## 7. Effective access and lifecycle

Effective permissions = union of active permissions granted by active roles assigned to the active, configured user. An inactive user, incomplete setup or ineligible company overrides the permission result. Company scope is a mandatory additional predicate; permission union never widens an external user's company scope.

Role deactivation preserves assignments but removes its grants. User suspension retains assignments while denying access. Termination preserves history and denies access; editing assignments is blocked. A user with no effective grants may access only explicitly allowed account/setup/logout functions, never business records. No deny precedence or role inheritance exists in this release.

An operator may alter their own assignments only if the last-administrator invariant remains satisfied. Apply changes immediately and redirect them to an allowed page if they lose administration access; frontend navigation is not the enforcement boundary.

## 8. Errors and audit

Unknown/incompatible role → 400; newly inactive role → 409; duplicate payload IDs → 400; unauthorized caller → 403; missing target → 404; stale version → 412; last-administrator violation → 409. Retain selection and show the specific role requiring correction without exposing unauthorized user details. Audit must identify adds and removals, including removal of all access. Never rely on a mutable mapping table alone as assignment history.

## 9. Acceptance criteria

| ID | Scenario and expected result |
|---|---|
| URM-AC01 | New user with two roles: account and mappings appear together; no second save needed |
| URM-AC02 | One role assigned to several users: all resolve its compatible active grants |
| URM-AC03 | Repeated/duplicate submission: no duplicate mapping or partial create |
| URM-AC04 | Forged cross-type role ID: rejected by service and database compatibility constraint |
| URM-AC05 | Desired set replaces current set: adds/removals atomic; unchanged mapping audit timestamps retained |
| URM-AC06 | Remove one of two roles granting reports.read: reports.read remains effective |
| URM-AC07 | Remove all roles explicitly: business access stops; account and immutable history remain |
| URM-AC08 | Save old user version after another assignment change: 412, no lost update |
| URM-AC09 | Role deactivated between opening form and save: no new inactive assignment accepted |
| URM-AC10 | Existing inactive mapping on profile-only edit: not silently deleted or reactivated |
| URM-AC11 | Concurrent removal/suspension/deactivation of final administrators: at least one usable administrator remains |
| URM-AC12 | Mapping failure during account creation: account/token/outbox all roll back |
| URM-AC13 | Permission removal takes effect on next protected request even with an old client token |
| URM-AC14 | Non-authorized caller hides/forges UI fields: server still blocks assignment changes |

## 10. Delivery checklist

Before EF implementation, reconcile numeric IDs, company membership, permission catalog, composite constraints, concurrency, immutable audit and invitation lifecycle. Test the three screen entry points against the same service behavior. Bulk assignment, expiring assignments, delegated external administrators, direct per-user permissions and role inheritance are excluded.
