# FSD-RD — Role Details

Version 1.0 • 15 October 2026 • Draft • Scope: configurable role master and capability grants

## 1. Purpose and boundaries

Allow authorized Kodiak administrators to define roles such as Booking Coordinator, Tracking Officer and Report Viewer without changing application code for each role. A role is a collection of implemented capabilities for one user type. Creating a new permission name does not implement a new application function.

This document covers userRoles and proposed permission tables. [Role Mapping](03-USER-ROLE-MAPPING-FSD.md) assigns roles to users; [User Details](01-USER-DETAILS-FSD.md) embeds the selection in user creation/editing. Shared policies are in [README](README.md).

## 2. Access and rules

Role list/details, creation, editing, grants and activation/deactivation require access.manage and an active internal identity. A normal user receives only safe summaries of roles already assigned to them. User type is fixed separately from role: Internal, Consignee or Shipper. Role codes confer no implicit privilege, even if named ADMIN.

Role code is unique case-insensitively and immutable after creation. Role name and description can change. User type is immutable once created; create a replacement role if its audience must change. Roles can have zero permissions while being configured; clearly label them “No capabilities” and do not offer them for new operational account creation. No role hierarchy, explicit deny, bulk grant or cross-company role is included.

## 3. UI

### RD-01 Role list

Administration > Roles. Columns: Code, Role Name, User Type, Status, Permission Count, Assigned User Count, Updated On, Actions. Search code/name; filter type and active/inactive; default name ascending. Actions: View/Edit, View Assigned Users, Activate/Deactivate. No hard delete. Assigned count includes all current mappings; distinguish active assigned users in the impact preview.

### RD-02 Create/Edit Role

```text
Administration / Roles / Create Role
[Role code *                 ] [Role name *                    ]
[User type *                v] [Active: yes/no                 ]
[Description                                                   ]
Permissions
Module           View     Create    Update/Review    Manage
Bookings         [x]      [ ]       [x]              —
Tracking         [x]      —         [ ]              —
Reports          [x]      —         —                —
Access           —        —         —                [ ]
[Preview granted actions]                      [Cancel] [Save]
```

The matrix is rendered from implemented permission metadata, not a promise that every module has every action. Only compatible capabilities appear. Each checkbox has a label and help text explaining its scope. Search/filter modules without losing selections. “Select all” applies only to the displayed module and shows affected count. Sensitive access.manage is explicitly described; it never grants itself through selecting a role name.

Changing type on a new unsaved form clears incompatible grants after a warning. Edit displays code/type read-only. Before saving changes to an assigned role, show added/removed permissions and affected user counts; require a reason for permission changes. An empty active role displays a warning. Unsaved changes prompt on navigation.

### RD-03 Deactivate/Activate

Deactivate dialog states: existing mappings are retained, this role stops granting permissions immediately, and users may retain access through other roles. Show affected active users and whether anyone loses all permissions. Require reason and confirm. Block any change leaving no active internal account with both users.manage and access.manage. Activation restores grants to retained mappings; explicitly show this impact and require confirmation. Never silently reactivate a role while selecting it on a user screen.

## 4. Tables and data

Existing userRoles fields:

| Field | Type | Required | Behavior |
|---|---|---|---|
| roleId | integer identity PK | Generated | Stable role identifier |
| code | varchar(100) | Yes | Trim; unique ignoring case; proposed uppercase letters/digits/underscore convention |
| roleName | varchar(250) | Yes | Trim; nonblank; display label |
| isActive | boolean | Yes | Default true; not the same as any user's status |
| createOn / updatedOn | timestamptz | Yes | Server audit times |
| createdBy / updatedBy | bigint FK userDetails | Nullable bootstrap only | Trusted actor |

**Proposed additions to userRoles:** description varchar(1000) nullable; userType integer required, restricted to 1/2/3; version bigint required default 1 for concurrency. These are not yet in SQL 007.

**Proposed userPermissions table:** permissionId integer identity PK; code varchar(100); userType integer; permissionName varchar(250); moduleCode varchar(100); description text; isActive boolean; four audit fields. Unique(code,userType). Permissions are seeded/versioned by developers as implemented endpoint actions; no arbitrary permission creation screen in this release.

**Proposed userRolePermissions table:** rolePermissionId bigint identity PK; fkRoleId integer FK userRoles; fkPermissionId integer FK userPermissions; userType integer; four audit fields. Unique(role,permission). Composite FKs enforce role/permission user-type compatibility. Use the same composite approach in userRoleMapping. Retain the audit event history separately when a grant is removed.

These replace/adapt the older permission model's UUID references; do not create duplicate authorization catalogs. All final FK types must match the numeric identity model.

## 5. Capability catalog and semantics

Initial examples: users.manage, access.manage, bookings.read, bookings.create, bookings.review, tracking.read, tracking.update, reports.read. Internal-only capabilities include access management and official tracking updates. External read/create capabilities always retain company/ownership checks. Viewing internal notes and internal documents requires separate internal capabilities.

The final catalog must inventory every endpoint before release. Read permission is not automatically implied by write permission. The editor validates declared capability dependencies (for example tracking.update requires tracking.read) and prompts to add missing prerequisites rather than silently broadening access. This dependency metadata/validation is proposed service configuration. API enforces the same validation.

Effective access is the union of active capability grants across active roles of an active eligible user. There are no explicit denies. Deleting one grant cannot remove a capability granted by another assigned role. Business restrictions can still prevent an otherwise permitted action.

## 6. Functions and API

| Function | Proposed endpoint | Behavior |
|---|---|---|
| SearchRoles | GET /api/roles | Paged list/counts, filters |
| GetRole | GET /api/roles/{id} | Metadata, permission IDs, ETag |
| GetCapabilityCatalog | GET /api/permissions?userType=1 | Implemented compatible permissions and dependency help |
| CreateRole | POST /api/roles | Save role and selected grants atomically; 201 |
| UpdateRole | PUT /api/roles/{id} | Full editable metadata/grant set, reason, If-Match |
| PreviewRoleImpact | POST /api/roles/{id}/impact-preview | Affected counts and access-loss warning; preview does not authorize save |
| SetRoleStatus | POST /api/roles/{id}/status-changes | Desired active state, reason, If-Match |
| GetAssignedUsers | GET /api/roles/{id}/users | Paged user summaries, authorized internal caller only |

Save transaction: authenticate/authorize → validate version/code/type/capabilities → lock affected access-control records → calculate additions/removals and last-administrator invariant → save role/grants + immutable audit → bump role version and invalidate affected authorization → commit. Preview data can become stale; repeat all checks at commit. Role changes and user/mapping changes must use the same serialization strategy for the last-administrator invariant.

Queries must never authorize based on client-submitted role names or checkbox state. Inactive permissions are unavailable for new grants and give no effective access even where an old grant remains. Reenabling a permission has an access-restoration impact and is a controlled catalog operation.

## 7. Errors, audit and concurrency

Duplicate code → 409; missing name/oversized description/unsupported permission → 400; cross-type grant → 400; last-administrator removal → 409; stale If-Match → 412; missing capability → 403. Failed saves leave both metadata and grants unchanged. Responses retain user input and identify invalid selections.

Audit records include role ID, changed metadata, added/removed permission IDs, reason, actor, timestamp and correlation ID. Do not log credentials. Grant removal must not erase audit history. Role versions change whenever metadata, status or grants change. Avoid stale JWT role/permission snapshots: permissions must reflect a committed revocation on the next protected request.

## 8. Acceptance criteria

| ID | Scenario and expected result |
|---|---|
| RD-AC01 | Create a previously unknown role code: succeeds without source changes and persists chosen capabilities |
| RD-AC02 | Duplicate case-variant code: rejected with no partial grants |
| RD-AC03 | Shipper role given internal access.manage: rejected server-side even with forged payload |
| RD-AC04 | Rename role: existing assignments and effective permissions remain unchanged |
| RD-AC05 | Deactivate assigned role: mappings retained; grants stop on next protected request |
| RD-AC06 | Activate role: retained mappings regain its active grants after confirmed impact preview |
| RD-AC07 | Remove last combined users.manage/access.manage path: blocked, including concurrent saves |
| RD-AC08 | Edit stale role version: 412, no overwrite |
| RD-AC09 | Remove permission also held through another role: user retains that capability |
| RD-AC10 | Fail halfway through saving grants: entire role operation rolls back |
| RD-AC11 | Permission matrix works by keyboard; labels/help distinguish read and write actions |
| RD-AC12 | Inactive or unimplemented catalog selection: cannot become an effective grant |

## 9. Implementation boundary

Requires numeric company/user integration, permission catalog, versions, immutable audit and immediate authorization invalidation. No role inheritance or per-user direct permission override is specified. Approve the capability catalog and UI actions before implementation; no EF code is part of this FSD.
