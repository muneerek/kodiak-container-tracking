# Kodiak container tracking Web API

.NET 10, ASP.NET Core controllers, PostgreSQL and EF Core code first. The UI folder is reserved for later work.

## Architecture

- Api: HTTP controllers, JWT validation/issuance, password hashing, rate limiting and dependency injection.
- Business: DTOs, service interfaces, validation, account/master workflows and bootstrap.
- DataAccess: entities, Fluent mappings, repositories, shared transactions, audit history and migrations.
- Tests: HTTP tests against relational SQLite plus optional live PostgreSQL migration/concurrency tests.

Dependencies flow API ? Business ? DataAccess. Controllers do not return database entities or query EF directly. Writes and audit events commit in one serializable transaction with bounded conflict retries.

## Final schema

All tables are in schema ctracking. PostgreSQL types replace the mixed SQL Server/PostgreSQL syntax in the draft: integer identity keys, boolean active flags, text for long strings, date for DOB, and timestamp with time zone for UTC audit/login/token times. FK ID widths match their integer primary keys.

| Table | Main fields |
|---|---|
| applicationUser | appUserId, userName, password hash, linkedUniqueId, linkedUniqueCode (50), userType, isActive, audit fields |
| userLoginDetails | loginId, fkAppUserId, loginTime, logoutTime |
| userTokens | tokenId, fkAppUserId, purpose, tokenHash, expiresOn, usedOn, revokedOn, audit fields |
| userRoles | roleId, code, roleName, isActive, audit fields |
| userRolesmapin | mapinRoleId, fkAppUserId, **fkRoleId**, audit fields; unique account/role pair |
| employeeDetails | employeeId, firstName, lastName, logo, dob, emailId, userStatus, designationId, orgId, code/audit fields |
| consigneeDetails | consigneeId, companyName, lastName/contact name, logo, emailId, status, phoneNumber, countryId, address, code/audit fields |
| shipperDetails | shipperId, companyName, lastName/contact person, logo, emailId, status, phoneNumber, countryId, address, businessCategoryId, **consigneeId**, code/audit fields |
| company | companyId, code, name, logo/contact/location details, isActive, audit fields |
| designation | designationId, code, name, isActive, audit fields |
| organizationHirarchy | orgId, companyId, parentOrgId, code, name, isActive, audit fields |
| conuntryMaster | countryId, code, name, isActive, audit fields |
| StateMaster | stateId, countryId, code, name, isActive, audit fields |
| businessCategory | businessCategoryId, code, name, isActive, audit fields |

The draft names userRolesmapin, organizationHirarchy and conuntryMaster are retained in SQL. Public C# names use standard spelling. The missing role FK is added to make assignments functional. A shipper may optionally link to one active consignee.

Internal normalized username/role-code fields and version columns support uniqueness and ETags. partyReferences and uniqueCodeCounters preserve the existing generated linked-record codes and FK integrity. Employee/consignee/shipper IDs are allocated through the shared identity registry, rather than three unrelated identity sequences; the master ID and linkedUniqueId refer to the same record. accessAuditEvents stores append-only application history.

Account types: **0 Normal, 1 Admin, 2 Consignee, 3 Shipper**. Normal and Admin both link to an employee. Types 2/3 link to their respective business records. Employee userStatus: **1 Active, 2 Terminated**. Business status: **1 Active, 2 Blacklisted**. App isActive independently enables/disables sign-in.

Audit actor columns are varchar(50) strings, not numeric user FKs. Local account IDs are stored as text; a long external audit subject is represented by a stable hash. Password hashes and token hashes never appear in audit payloads or public DTOs.

## Initial setup and super-admin

Run from this folder. Requires .NET 10 SDK and a PostgreSQL server.

```powershell
dotnet restore
dotnet tool restore
$env:ConnectionStrings__TrackingDatabase = "Host=localhost;Port=5432;Database=kodiak_container_tracking;Username=postgres;Password=YOUR_DATABASE_PASSWORD"

# Generate a signing key once. Keep the same value in your local/hosting secret configuration.
$jwtKeyBytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($jwtKeyBytes)
$env:Jwt__SigningKey = [Convert]::ToBase64String($jwtKeyBytes)

dotnet ef database update --project src/Kodiak.ContainerTracking.DataAccess --startup-project src/Kodiak.ContainerTracking.Api

$initialPassword = Read-Host "Initial superadmin password (12-128 characters)" -AsSecureString
$env:Bootstrap__InitialPassword = [System.Net.NetworkCredential]::new("", $initialPassword).Password
dotnet run --project src/Kodiak.ContainerTracking.Api -- --bootstrap-super-admin
Remove-Item Env:\Bootstrap__InitialPassword

dotnet dev-certs https --trust
dotnet run --project src/Kodiak.ContainerTracking.Api --launch-profile https
```

The setup command creates one active **superadmin** application user linked to a **Super Admin** employee, the SUPERADMIN role and mapping, and default Kodiak company/Head Office/Super Administrator designation records if absent. The password is hashed with ASP.NET Core Identity's password hasher; no default plaintext password is shipped. Email can be configured as Bootstrap:EmailId. Unknown DOB/email values are not fabricated.

Running setup again verifies and returns the existing account; it never resets its password or creates a duplicate employee. A username collision with a normal, inactive or incompatible account stops setup instead of upgrading it. The super-admin username cannot be renamed through ordinary user edits. Bootstrap:Enabled=true enables the same controlled process at startup; its default is false. Migrations are always applied explicitly.

The signing key and initial password are intentionally empty in appsettings.json. Supply them through environment variables or your secret store. Do not regenerate the JWT signing key on every restart; all API instances must share the configured key. User secrets remain an optional local-development configuration source.

The configured application database was not migrated or seeded automatically during development. Tests and command-line setup were verified on disposable test databases.

## JWT sign-in and token lifecycle

Default HTTPS: https://localhost:7225. Development OpenAPI JSON: /openapi/v1.json. Requests are in the API project's .http file.

```http
POST /api/auth/login
Content-Type: application/json

{ "userName": "superadmin", "password": "YOUR_CHOSEN_INITIAL_PASSWORD" }
```

Response: accessToken, refreshToken, tokenType=Bearer, expiresOn and safe user details. Send Authorization: Bearer ACCESS_TOKEN on protected requests.

- POST /api/auth/refresh: submit { "refreshToken": "..." }. Refresh tokens are cryptographically random, stored only as SHA-256 hashes, expire, and rotate once per successful use. Reuse is rejected.
- POST /api/auth/logout: authenticated; closes recorded open login sessions, revokes refresh tokens and invalidates outstanding access tokens for the account.
- Access tokens use HS256 and validate signature, issuer, audience and expiry. Default lifetime: 15 minutes, with 10 seconds clock skew. Refresh lifetime: 7 days. Configure Jwt values as needed.
- Each protected request checks the current account version, active state and employee/business status. Role claims are rebuilt from active stored mappings. Deactivation, termination, blacklisting, password reset and session revocation take effect on the next request.
- Type 1 accounts receive users.manage and access.manage capabilities. Normal/external types do not gain administrative access merely by naming or assigning a role. Role records are configurable groups; this schema does not include a fine-grained permission catalog.
- The final active administrator cannot be deactivated, nor can its employee be terminated. SUPERADMIN cannot be deactivated as a role.
- Login/refresh have a per-IP limit of 10 requests/minute. Set trusted proxy handling appropriately if deploying behind a proxy.

Login history records successful login/logout times. A null logoutTime means no logout was recorded; it is not proof a session is currently active. RESET_PASSWORD/VERIFY_EMAIL delivery workflows are not implemented; the token table supports future purposes.

## APIs

Administrative APIs require an active Admin JWT. Updates use ETag/If-Match. Missing precondition is 428; validation is 400; unauthenticated/unauthorized is 401/403; missing resource is 404; uniqueness/business conflicts are 409; stale versions are 412. Responses use Problem Details and a trace ID.

| Routes | Operations |
|---|---|
| /api/users | GET paged list; POST create account/password/roles |
| /api/users/{id} | GET account/details; PUT username and isActive |
| /api/users/{id}/password | POST password reset with reason and If-Match; revokes tokens |
| /api/users/{id}/roles | GET/PUT role assignments; replacement is atomic |
| /api/users/{id}/logins | GET paged login/logout history |
| /api/users/{id}/tokens | GET safe token metadata; never returns hashes/raw tokens |
| /api/users/{id}/revoke-sessions | POST reason + If-Match; revokes all sessions |
| /api/roles and /api/roles/{id} | GET/POST and GET/PUT configurable roles |
| /api/employees and /api/employees/{id} | GET/POST and GET/PUT employee details |
| /api/consignees and /api/consignees/{id} | GET/POST and GET/PUT consignee details |
| /api/shippers and /api/shippers/{id} | GET/POST and GET/PUT shipper details |
| /api/parties/by-code/{code} | GET current linked record details by generated code |
| /api/masters/{kind} and /api/masters/{kind}/{id} | GET/POST and GET/PUT master records |

Master kinds: Companies, Designations, Organizations, Countries, States, BusinessCategories. Countries/states and organization parents are validated. An organization parent must be in the same company, and cycles are rejected. Hard-delete endpoints are not exposed.

### Employee

```json
{ "firstName": "Aisha", "lastName": "Khan", "dob": "1990-01-01", "emailId": "aisha@example.com", "logo": null, "userStatus": 1, "designationId": 1, "orgId": 1 }
```

### Consignee

```json
{ "companyName": "Customer Logistics", "lastName": "Contact Name", "logo": null, "emailId": "contact@example.com", "status": 1, "phoneNumber": "+919876543210", "countryId": 1, "address": "24 Port Road" }
```

### Shipper

```json
{ "companyName": "Exporter Logistics", "lastName": "Contact Person", "logo": null, "emailId": "shipper@example.com", "status": 1, "phoneNumber": "+919876543210", "countryId": 1, "address": "36 Dock Road", "businessCategoryId": 1, "consigneeId": 2 }
```

### Application user

Create a linked employee/business record and role first; use their returned IDs:

```json
{ "userName": "aisha.k", "password": "CHOOSE_A_PASSWORD_OF_AT_LEAST_12_CHARACTERS", "linkedUniqueId": 2, "userType": 0, "isActive": true, "roleIds": [2] }
```

The application copies linkedUniqueCode from the selected record. Clients cannot supply a password hash, generated code or audit fields. IDs are integer JSON numbers; version remains a quoted ETag/string. Account type and linked record are immutable in ordinary edits.

Employee Logo stores the relative photo path. Employee photographs can be uploaded, viewed and removed through authorized employee photo endpoints. Location, designation and organization FKs may be omitted for legacy/incomplete records. When supplied they must identify active master records. Shipper consignee selection is optional for new/updated records; supplied selections must be active.

UniqueCodes settings still control EmployeePrefix, ConsigneePrefix, ShipperPrefix and Padding. Separate database counters reserve numbers atomically in the transaction; existing codes are unchanged when prefixes change. Examples: EMP-000001, CON-000001, SHP-000001.

## Migration behavior

FinalApplicationUserStructure renames tables/keys in place and preserves account IDs, role mappings, password hashes and generated codes. Existing internal accounts become Normal (0), because old type 1 did not imply administrator. Only controlled bootstrap creates the initial Admin (1).

Fields removed by the final employee structure are archived in legacyStructureArchive before removal. Profile snapshots and account metadata are retained there; password hashes are excluded from the archive. Protect that archive as personal data. IDs/codes/company names exceeding the final integer/length limits stop migration rather than being truncated. The final restructuring migration is forward-only; restore a pre-upgrade backup for rollback.

## Validation

```powershell
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes --project src/Kodiak.ContainerTracking.DataAccess --startup-project src/Kodiak.ContainerTracking.Api
```

For live migration, deployment-script and concurrency tests, set KODIAK_TEST_POSTGRES to a **disposable** PostgreSQL server connection with CREATE DATABASE permission. Tests create/drop uniquely named databases. Without the variable they are explicitly skipped.

If the repository drive is low on space, set ArtifactsPath to another drive and UseArtifactsOutput=true before running dotnet commands.

References: [ASP.NET Core JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing).

## Swagger UI

In Development, open https://localhost:7225/swagger after starting the API. Execute POST /api/auth/login, copy accessToken, click Authorize and paste the token without the Bearer prefix. Protected operations send the Bearer header automatically. Swagger UI does not retain authorization across page reloads.


Employee deletion: DELETE /api/employees/{id}, with If-Match, marks the employee Terminated while preserving linked records and audit history. The final active administrator remains protected. Reactivate through PUT. No new migration is required.


## Employee photos and technical account isolation

PhotoStorage in the API appsettings.json configures RootPath (default App_Data/Photos, relative to the API content root) and MaxFileBytes (default 2097152). RootPath may be an absolute local folder or a Windows UNC share. For example:

~~~json
"PhotoStorage": {
  "RootPath": "D:\\KodiakData\\Photos",
  "MaxFileBytes": 2097152
}
~~~

The API service identity must have read/write permission on the configured location, including share permissions for a UNC path. Keep this directory outside any public web root. Multiple API instances should use the same shared location. Database backups and the photo directory must be backed up together. Only a generated relative path, such as employees/{guid}.jpg, is stored in employeeDetails.logo; the configured storage root is not returned to the browser.

GET /api/employees/{id}/photo returns authorized image bytes, PUT accepts multipart/form-data field photo with If-Match, and DELETE clears the photo reference with If-Match. Stored uploads accept JPEG/PNG signatures and enforce the configured byte limit (up to 5 MiB). The UI accepts JPEG/PNG/WebP sources up to 10 MiB, offers zoom and horizontal/vertical positioning, and exports a 512 x 512 JPEG crop. Upload failures clean up the new file; existing replaced/removed files are retained for audit/recovery and need a separately managed retention policy. Photo reads are private and not cached. A stale version requires reopening the record.

Employee creation/update and photo upload are separate requests. If photo upload fails after profile save, the UI keeps the existing employee ID/version for retry and reports that the profile was already saved. It does not create another employee on retry.

Technical protection is based on the SUPERADMIN role or SUPERADMIN employee designation. Other administrators cannot list these accounts/employees (including pagination totals), fetch by ID/code, update/delete them, upload/read/remove their photos, reset passwords, inspect/revoke sessions or replace roles. Those resource requests return 404. Login/refresh/JWT validation remain functional through internal unfiltered authentication reads. Other administrators cannot assign the protected role or designation or create an alias linked to the protected employee. The reserved designation cannot be renamed/replaced/deactivated through master APIs; the SUPERADMIN role cannot be removed from its technical account. This is an application boundary: infrastructure/database administrators still control the underlying deployment.

No schema migration is required for these features. Restart the rebuilt API and refresh/sign in to the UI.

## Public shipper registration

The login page links to Register as a shipper. POST /api/registration/shippers anonymously accepts companyName, contactPerson, emailId, phoneNumber, address, consigneeId, userName, password, confirmPassword, and optional countryId/businessCategoryId. It creates shipperDetails, the generated party reference/code, applicationUser with fixed userType=3 and a password hash, and the SHIPPER role mapping in one transaction. A missing SHIPPER role is created automatically; an inactive role blocks registration. Invalid/duplicate submissions do not leave a partial shipper or account. The public contract cannot set account type, role IDs, status, generated code or audit fields.

Successful signup returns only userName and uniqueCode; the UI returns to sign-in. Accounts are active immediately and can log in. No administrator approval step is included in this increment. Administrative APIs remain forbidden to Shipper accounts.

GET /api/registration/options/{kind} provides active consignees, countries or businesscategories, with page/pageSize and optional search (up to 100 characters). Responses expose only id, code and name; company contact/address details are not exposed. Consignee is optional: an omitted or null consigneeId is stored as null, while any supplied ID must select an active consignee. Registration is limited to 5 POST requests/minute/IP and option lookups to 60/minute/IP. No schema migration is required.

## Country and state/province seeders

From the backend folder, after applying existing migrations:

~~~powershell
dotnet run --project src/Kodiak.ContainerTracking.Api -- --seed-geography
~~~

This inserts all 249 ISO country/territory entries into ctracking."conuntryMaster" and 77 subdivisions into ctracking."StateMaster": India 36 states/union territories, UAE 7 emirates, China 34 ISO provincial-level entries. Use --seed-countries for countries alone; --seed-states ensures country parents exist before inserting the selected subdivisions. The commands exit after seeding without starting an HTTP listener. Local dotnet run uses the development launch profile/user secrets; when running a published DLL, supply the deployment connection string or explicitly use the appropriate environment.

CountrySeeder and StateProvinceSeeder run in one transaction through GeographySeeder, with standard audit stamps. Repeat runs insert only missing records and report counts. Existing names, IDs, custom codes and inactive flags are preserved. Countries match ISO alpha-2/alpha-3 codes, the UAE alias, or normalized names. States match full ISO codes, or matching names/short codes within their country. Ambiguous records or a full state code assigned to another country stop the entire seed transaction instead of changing customer data. No schema migration or runtime network access is required.

The committed JSON snapshot, provenance notice and upstream license are under Business/Seeding/Data. Source: pycountry's packaged ISO 3166-1 and ISO 3166-2 datasets (retrieved 2026-10-20). Refresh these files deliberately when administrative geography changes. This adds master data only; it does not add or make a State field mandatory on shipper registration.

Business category CRUD: GET/POST /api/masters/BusinessCategories; GET/PUT/DELETE /api/masters/BusinessCategories/{id}. All require UsersManage authorization. PUT and DELETE require If-Match. DELETE sets isActive=false and increments the version, retains linked shippers/audit history, and removes the category from public registration options. PUT can reactivate it. No database migration is required.

Consignee CRUD: GET/POST /api/consignees; GET/PUT/DELETE /api/consignees/{id}. These require UsersManage authorization. PUT/DELETE require If-Match. DELETE changes status to Blacklisted (2), preserving the row, linked shippers and audit history. Public registration selections omit blacklisted consignees; linked consignee accounts fail the existing sign-in eligibility check. PUT with status Active (1) restores eligibility. No database migration is required.

## Shipper management and logos

Administrators have Management > Shippers with paged list/view/create/edit/delete. Shipper accounts have Management > My shipper details and can view/edit only the record linked to their application user. Ownership is checked against the database in the business layer, including logo endpoints; list totals and rows are restricted to the shipper's own record. Only administrators can create/delete shipper records or change Active/Blacklisted status. Other users cannot access shipper management.

GET/PUT /api/shippers/{id} and GET /api/shippers allow administrators or the linked shipper. POST /api/shippers and DELETE /api/shippers/{id} require administrator access. DELETE blacklists the shipper while preserving linked accounts/audit history; the sign-in eligibility check denies the linked shipper account on subsequent requests. Administrators can restore Active status through PUT. All updates/deletions require If-Match.

GET/PUT/DELETE /api/shippers/{id}/logo allow administrators or the linked shipper. Upload uses multipart field photo and If-Match, stores a generated shippers/{guid}.jpg or .png relative path in shipperDetails.logo, and writes the file beneath the existing PhotoStorage:RootPath. Image formats/limits, stale-upload cleanup, authorized file reads and historical-file retention match employee photographs. Profile changes cannot directly assign another stored photo/logo path.

The UI supports Browse/replace, Edit/crop, preview and Remove logo through the existing crop editor. Country and business category choices use safe active public option endpoints so shipper accounts do not need master-management permissions. Consignee remains optional; unchanged historical links to a blacklisted consignee can be retained, but a newly selected consignee must be active. No schema migration is required. Restart the rebuilt API and refresh the UI.

## Country, state/province and shipping port masters

Administrators can manage Countries, States / Provinces and Shipping ports under Management. Each uses the standard detail/Add/Edit popup, paging, fresh If-Match versions, and a Delete confirmation that deactivates rather than physically deleting. Existing references are retained; deactivation does not cascade automatically. Other account types cannot access these master APIs or menus.

Master routes are /api/masters/Countries, /api/masters/States, and /api/masters/ShippingPorts, with GET/POST on the collection and GET/PUT/DELETE by ID. States require an active country; their country cannot be changed after creation. Ports require a code, name, active country and valid time zone (default UTC); state/province and city are optional. Selected states must be active and belong to the selected country. The database also enforces the port's country/state pairing through a composite foreign key.

Migration ShippingPortMaster adds ctracking.shippingPort and the supporting StateMaster alternate key. Existing countries, states and their IDs are preserved. Apply with the normal dotnet ef database update command on each deployment, then rebuild/restart the API. No port sample records are inserted automatically.

## Container registration and attachments

Workspace > Containers follows the prototype registration fields: shipper, optional container number, five container types, cargo, weight, packages, origin/destination ports, departure/expected arrival dates and an optional reference. Each registration receives a KD-prefixed sequence code and starts Booked. At least two active shipping ports are needed. Physical container numbers are normalized and unique among Booked registrations. This increment covers registration, not subsequent tracking events.

Administrators with users.manage can register for active shippers, browse all registrations, edit Booked registrations and cancel them. Shippers can register and edit only their own records; ownership is derived from their account. Consignees can read registrations linked to them when the registration was created. The consignee link is a snapshot and does not change when the shipper profile is reassigned. Cancellation retains the registration and attachments.

GET/POST /api/containers, GET/PUT/DELETE /api/containers/{id}, GET /api/containers/options/{ports|shippers}, POST /api/containers/{id}/files, and GET/DELETE /api/containers/{id}/files/{fileId} implement the flow. DELETE on a registration cancels it and requires administrator access. Updates, cancellations and file changes require If-Match. Uploads use multipart fields file and kind (Photo=1, Document=2). Downloads require the same record access as other reads; storage paths are never returned to the browser.

ContainerStorage in API appsettings.json configures RootPath (default App_Data/ContainerAttachments, relative to the API content root), MaxFileBytes (default 10485760, up to 10 MiB) and MaxFilesPerContainer (default 20). RootPath may be an absolute local folder or UNC share. Grant the API identity read/write access, keep it outside the public web root, and back it up together with the database. Store the same root on a shared location for multiple API instances.

Photos support JPEG/PNG; reference documents support PDF, DOCX, XLSX and UTF-8 TXT. Files are checked for size and format, use generated storage names, and are accessed through authorized endpoints. Office files reject macro payloads and oversized archive contents. Document downloads use attachment disposition. These format checks are not a malware scanner. Removed files are soft deleted and old files remain on disk for recovery; manage their retention separately.

The popup supports multiple photos/documents, preview/download and removal while editing. Registration and uploads use separate requests. If an upload fails after the registration saves, the popup retains the registration ID/version and remaining files for retry, avoiding duplicate registrations.

Migration ContainerRegistrationsAndAttachments adds the two tables and code sequence. Apply dotnet ef database update on deployments before starting the rebuilt API. No existing master or party records are replaced.

Container listing accepts optional status=1 (Booked) or status=2 (Cancelled), combined with search. Unknown status values return 400. Search also matches references and origin/destination port codes/names. Filters retain the same server-side ownership scope and matching pagination totals. No database migration is required for these filters.

## Time zone directory

The TimeZoneDirectory migration creates ctracking."timeZone" and seeds every name (including aliases) from PostgreSQL pg_timezone_names. GET /api/masters/timezones requires users.manage and returns the full sorted directory for the shipping port autocomplete. Explicit timezone values must use an exact directory name. The shipping port screen hides timezone; new ports default to UTC and edits preserve the saved value.

Columns: name (primary key), abbreviation, utcOffsetSeconds (supports half/quarter-hour zones), isDaylightSaving, observedAt. Offsets and DST are a snapshot at observedAt, not permanent rules; use the timezone name for date conversions. PostgreSQL documentation: https://www.postgresql.org/docs/current/view-pg-timezone-names.html

Refresh metadata after timezone database updates or seasonal changes by running:

```sql
INSERT INTO ctracking."timeZone" (name, abbreviation, "utcOffsetSeconds", "isDaylightSaving", "observedAt")
SELECT name, abbrev, EXTRACT(EPOCH FROM utc_offset)::integer, is_dst, CURRENT_TIMESTAMP
FROM pg_timezone_names
ON CONFLICT (name) DO UPDATE SET abbreviation = EXCLUDED.abbreviation,
    "utcOffsetSeconds" = EXCLUDED."utcOffsetSeconds", "isDaylightSaving" = EXCLUDED."isDaylightSaving",
    "observedAt" = EXCLUDED."observedAt";
```

Shipping ports automatically allocate PORT-000001 style codes from the transactional masterCodeCounters table. Existing codes stay unchanged on edit; callers can omit Code for shipping ports. Country and state codes remain required.

## Container tracking and evidence

The ContainerTrackingTimeline migration adds containerTrackingEvent, trackingStatus and isOnHold, plus a composite attachment/event foreign key. Existing registrations start as Booked. Registration cancellation remains separate from tracking milestones (Booked, Loaded, In transit, Arrived, Customs clearance, Delivered).

POST /api/containers/{id}/tracking accepts multipart fields status, eventAt (ISO timestamp with offset), location, expectedArrival, optional containerNumber/vesselName/note/isOnHold, and repeated files parts. Requires an operations administrator and If-Match. Status, event, files and audit metadata commit together; failed updates clean up prepared uploads. Event times must be chronological and no more than five minutes in the future. Held containers cannot be marked Delivered until released.

Tracking files are retained with history. Existing scoped GET /api/containers/{id}/files/{fileId} serves authorized readers; unlinked parties cannot access the container or its files. Supported evidence: JPEG/PNG/WebP, PDF, XLS and XLSX (up to 10 MB each, 10 files / 50 MB per update, subject to the configured per-container attachment limit). Registration attachments additionally retain DOCX/TXT support.

The UI previews images and PDFs locally and uses SheetJS CE 0.20.3 for read-only XLS/XLSX previews (up to 200 rows and 30 columns per sheet), with original-file download. Files are not sent to an external viewer service. SheetJS official installation documentation: https://docs.sheetjs.com/docs/getting-started/installation/frameworks/

## Tracking workspace

The sidebar Tracking screen uses GET /api/containers/tracking with paged search, trackingStatus, onHold, shipperId and optional registration status filters. Filters are applied to the authorized scope before paging and export. GET /api/containers/options/tracking-shippers returns only shippers represented in that user's authorized container journeys, including historical links.

Container responses include origin/destination port codes for route cards. Journey details reuse the saved timeline and attachment viewers. Operations administrators can update status from either cards or the detail view; shippers and consignees retain their scoped viewing access. Register container opens the existing registration form. No external location or GPS feed is implied by reported locations.

## Admin overview dashboard

GET /api/dashboard/overview aggregates registrations in the database within the authenticated account scope. Administrators see the complete operations workspace; partner scopes are described below. The optional date query is the browser's local business date, used for overdue arrival calculations. Totals include cancelled registrations; active excludes cancellation. In-transit counts include held shipments at that reported stage. Status distribution is mutually exclusive, with holds and cancellations shown separately. Delivered/cancelled shipments are excluded from attention; hold, overdue and unallocated registrations are counted once each.

The response includes active partner totals, registered route/country counts, top four routes, five recent containers, up to five prioritized attention items, and six latest recorded registration/tracking activities. Refresh supplies a generatedAt timestamp. Dashboard cards open Tracking with the corresponding status filter; rows open the selected shipment timeline. No sample counts, trend percentages, GPS positions or fabricated activity are shown.

## Supplier and distributor overviews

The same overview endpoint supports the existing ContainersAccess policy for administrators, shippers and consignees. OverviewService derives scope from the active authenticated user and linked company; browser-supplied company IDs do not change it. Every registration, status count, route, country, attention item and activity query uses that scope. Shippers see their own registrations. Consignees see registrations whose stored consigneeId matches their linked company, preserving historical visibility when a shipper changes its current consignee.

The response profile supplies company identity and registration eligibility. Consignee dashboards additionally show up to six currently linked shippers with scoped registration counts, including those with no shipments. Tracking's shipper choices include the current consignee network as well as historically visible shipment owners. Shippers can register containers when their account permits; consignees view their network and documents. Tracking status changes retain their existing operations-only authorization.

## Header company profile

Shippers and consignees open their profile editor from the header initials icon. Their sidebar has no profile entry. GET/PUT /api/profile/company resolves the linked company from the authenticated account, requires If-Match for edits, and permits only company name, contact name, email, phone, address and country. Status, codes, consignee links, categories and login credentials are excluded from the request contract. Shipper logo editing continues through the existing authorized logo endpoint. Saved company details update the current header and session display.

