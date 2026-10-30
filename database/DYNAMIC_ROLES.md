# Dynamic roles and multiple user assignments

Migration 005 replaces the original single-role model. **Role names and counts are now configurable.** Account types INTERNAL, SHIPPER and CONSIGNEE remain fixed business boundaries; they are not job roles.

## What, why and how

| Object | What | Why | How |
|---|---|---|---|
| roles | Custom permission bundles with name, code, description, active flag and account type | Define jobs such as Operations Officer or Report Viewer | Authorized internal administrator creates roles and selects supported permissions |
| permissions | API action catalog for each account type | Makes permissions explicit instead of testing role names | API checks codes such as tracking.update; creating a catalog row alone does not implement a new action |
| role_permissions | Many-to-many role/permission assignments | Give each role several capabilities | Insert/delete grants; composite foreign keys prevent giving internal capabilities to external roles |
| user_roles | Many-to-many user/role assignments | A role serves many users and a user can hold multiple roles | Assign compatible roles; duplicate assignments are rejected |
| users.accounttype | Organization access boundary | Keeps custom roles from widening business data access | One internal identity or one shipper/consignee company per user; role assignment cannot change this scope |
| user_effective_permissions | Distinct union of active permission grants | Resolve the combined privileges of multiple roles | Query for authenticated userid and required permissioncode; independently filter all business records by organization |
| ix_user_roles_role | Index for reverse role membership | Supports listing affected users when a role changes | Filter user_roles by roleid |
| ix_role_permissions_permission | Index for reverse capability grants | Supports finding roles affected by a capability change | Filter role_permissions by permissionid |

All three new tables have createdby, updatedby, createdon, updatedon, automatic stamping and change logging. There are now **29 tables**. Role/permission deactivation removes effective grants. Removing one role retains grants from the user's other roles. Zero roles means no permissions. There are no explicit denies or inherited roles.

## Apply

Fresh database: run **001 → 002 → 004 → 005**. Existing database through 004: run only 005. Do not rerun 004 after 005 because it describes the old role-validation function removed by 005. Migration 005 copies existing users.roleid assignments into user_roles before removing that column; it does not delete accounts. The original 003 smoke test applies only before 005; run 006 after 005.

```sh
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f database/005_dynamic_roles.sql
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f database/006_dynamic_roles_test.sql
```

The original object guide describes the baseline. This document supersedes its single-role rules, roles/users descriptions and validate_user_scope/users_scope function/trigger descriptions. That function/trigger is removed; a user-scope CHECK constraint now enforces organization shape independently of role names.

## Example

Create internal roles **Booking Coordinator**, **Tracking Officer**, and **Report Viewer**. Grant bookings.read/bookings.review to the first, tracking.read/tracking.update to the second, reports.read to the third. A user can receive both Booking Coordinator and Report Viewer. Several other users can share Tracking Officer. Create separate shipper and consignee roles using capabilities permitted for their account type.

ADMIN, SHIPPER and CONSIGNEE are initial seeded role templates only. The API must never grant access by comparing those role names. Renaming a role must not change its capabilities.

## API integration requirements

- Check an active authenticated account, effective capability, company scope, and business restrictions on every endpoint. Company approval, company activity, blacklists and record ownership are not evaluated by the permission view.
- Require access.manage to create roles, change grants or assign roles. users.manage alone cannot assign roles or change accounttype/company membership. Treat company reassignment as an access-management operation.
- Public signup must never accept arbitrary roles, account type, grants or company IDs. Server chooses the approved onboarding role; operational access still requires shipper approval.
- Do not implement role-name shortcuts or implicit super-admin bypasses. New permissions are not automatically granted to existing roles; migrations/admin actions must grant them deliberately.
- Protect the last active internal account able to manage access. Only authorized access administrators can grant/revoke capabilities; do not let ordinary users update join tables directly.
- Refresh authorization after grant changes: query per request or invalidate cached permissions immediately. Long-lived JWT permission snapshots must not keep revoked access. Enforce token/lockout policies separately.
- Scope all queries/documents/reports by authenticated user organization. Internal roles still require the specific action capability. External users cannot acquire internal roles because both user and permission assignments must match account type.
- Expose role creation, role permission selection and user multi-role assignment screens when building the real API/frontend. The JSON prototype is not connected to this schema yet.
