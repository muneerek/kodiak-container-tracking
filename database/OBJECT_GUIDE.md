# Kodiak database object guide

**Current role model:** [Dynamic roles and permissions](DYNAMIC_ROLES.md) supersedes this baseline's single-role descriptions. Migration 005 removes users.roleid and validate_user_scope/users_scope, adds three tables and the user_effective_permissions view. Other baseline objects remain unchanged. Apply 004 comments before 005 only.

This guide explains **what** each object stores or does, **why** it exists, and **how** the application uses it. Names follow the new Shipper/Consignee terminology. It documents the current design; API responsibilities described here are not implemented yet.

## Schema

| Object | What | Why | How |
|---|---|---|---|
| kodiak | Namespace containing the application's database objects. | Keeps application objects together and separate from other schemas. | Qualify queries with kodiak or configure the API search path; assign privileges through a restricted database login. |

## Master tables

| Object | What | Why | How |
|---|---|---|---|
| countries | Country directory with a unique two-letter code and name. | Prevents inconsistent country text across companies and ports. | Admin maintains entries; company, state and port forms select a country ID. Deactivate obsolete entries. |
| states | States or emirates belonging to countries. | Supports country-dependent location choices. | Filter by countryid; code is unique within a country. A port's optional state must belong to its country. |
| ports | Port directory with country, optional state, city and timezone. | Gives bookings and tracking consistent origin, destination and event locations. | Admin maintains ports; API validates timezone names and uses them for display. Timestamps themselves are stored as timestamptz. |
| consignees | Consignee companies and business contact details. | Provides the organization that oversees linked shippers in the agreed business model. | Admin creates companies and their users; API scopes historical bookings by bookings.consigneeid, not merely the shipper's current relationship. |
| shippers | Shipper companies, current consignee and onboarding approval state. | Separates a shipping business from its individual login accounts. | Public signup creates a pending company; admin assigns a consignee and approves it. Approval requires a consignee; API checks active status and blacklists before booking. |
| roles | Three business role definitions: ADMIN, CONSIGNEE and SHIPPER. | Provides consistent role identifiers without repeating role text on users. | Seed once; users reference roleid. Keep role codes immutable through runtime permissions; API implements each role's permissions. |
| users | Named login accounts with role, organization, password hash and access state. | Allows multiple accountable users per company and independent login suspension. | Admin users have no external organization; external users reference exactly their role's organization. API handles password verification, lockout and session invalidation; nullable passwordhash must not permit password login. |
| container_types | Container size/type options and refrigeration flag. | Standardizes requested and allocated equipment. | Booking items and physical containers reference this directory; API checks that allocated equipment satisfies the request. |
| tracking_statuses | Physical tracking milestones with display order and terminal flag. | Keeps progress labels consistent and separate from booking approval and holds. | Tracking events reference statuses; displayorder helps presentation but does not enforce transition order. API validates transitions and terminal closure. |
| document_types | Categories such as bill of lading, invoice and delivery proof. | Makes attachments searchable and understandable. | Upload forms select documenttypeid; API enforces any required-document checklist. |
| restriction_reasons | Shared hold and blacklist reason categories. | Makes restrictions consistent and reportable without losing detailed explanations. | Admin selects a category and adds the specific internal/public explanation to a hold or blacklist entry. |
| containers | Physical container inventory with unique normalized number and equipment type. | Allows the same physical container to be used for multiple journeys without losing history. | Store uppercase compact numbers; SQL checks format, API checks the check digit. Allocate to an open journey; blacklist through blacklists, not a progress status. |

## Transaction and supporting tables

| Object | What | Why | How |
|---|---|---|---|
| shipper_reviews | Append-only onboarding decisions with reviewer and review time. | Explains how and why a shipper was approved, rejected or asked for changes. | Admin API inserts a decision and updates shippers.approvalstatus in one transaction; previous reviews remain unchanged. |
| shipper_consignee_history | Append-only old/new consignee assignment records with reason and effective time. | Preserves company relationship history after transfers. | Update shippers.consigneeid and insert history atomically. A null previousconsigneeid represents initial assignment. This records a change; it is not a scheduler for future transfers. |
| bookings | Shipping request header with parties, route, cargo and review status. | Groups the customer's request separately from physical container movement. | Shipper or admin creates a draft. API derives consignee on submission, freezes submitted party assignments and validates approval/blacklist rules. A pending unassigned shipper cannot create this header because consigneeid is required. |
| booking_items | Requested container type and quantity, with optional aggregate cargo measurements. | Lets one booking request several containers and different equipment types. | Add one line per requested group; weights, volumes and package counts are totals for that line, not automatically per container. API ensures allocated journeys do not exceed quantity. |
| booking_reviews | Append-only booking status decisions and reasons. | Preserves the review trail rather than retaining only the latest status. | Insert the review and update bookings.status in one transaction. SQL checks status vocabulary; API checks permitted transitions. |
| container_journeys | One container movement fulfilling a booking item, with schedule and closure outcome. | Gives each movement its own lifecycle, even when containers are reused or a booking has several containers. | Create before allocation if necessary; containerid may initially be null. One physical container has at most one open journey. Set endedon and outcome together on closure/cancellation; API also writes the terminal event. |
| journey_legs | Ordered port-to-port sections of a journey with vessel and planned/actual dates. | Supports transshipment and intermediate stops without overwriting the original route. | Number legs from one within a journey. API validates route continuity and overall route consistency; actual times are entered manually. |
| tracking_events | Append-only manual status observations with occurrence time, location, source and notes. | Builds an accountable timeline and distinguishes event time from data-entry time. | Insert observations; correct mistakes with a new event referencing supersedeseventid and a reason. Replacements must belong to the same journey. Hide internalnotes from external users. |
| journey_holds | Independent operational holds with reasons, review dates and release details. | Keeps restrictions separate from physical progress and permits several simultaneous holds. | Insert each hold separately; null releasedon means open. Release by setting releasedon, releasedby and releasereason together. API blocks closure while any hold is open. |
| blacklists | Restrictions targeting either one shipper or one physical container. | Blocks future activity while preserving ongoing shipment history. | Exactly one target is required. Insert a restriction; release it with actor/time/reason. API rechecks active restrictions during submission, confirmation and allocation; SQL alone does not block those operations. |
| documents | Attachment metadata, parent record, private storage key and scan/visibility status. | Links files to business records without storing file binaries in database rows. | Attach to exactly one shipper, booking or journey. Store bytes in private object storage; API validates type/size, scans files and authorizes downloads. Record each additional file separately. |
| notifications | One recipient's in-app message, optional booking/journey link and read time. | Supports personal unread counts without sharing read state between users. | Insert one row per recipient; set readon when read. API checks current authorization before exposing messages or opening their linked records. This table is not an email delivery queue. |
| user_tokens | Hashes of refresh, password-reset and verification tokens with lifecycle timestamps. | Supports revocable credentials without retaining plaintext token secrets. | API issues tokens, stores only secure hashes, checks expiry/revocation and atomically consumes or rotates them. Purpose separates token uses; expiry is checked by the API. |
| audit_logs | Append-only change metadata: entity, action, actor, request and changed field names. | Helps investigate who changed a record and correlate changes with API requests. | Triggers create entries automatically. No full before/after values are retained, so this cannot restore deleted records or reconstruct every previous value. entityid is a generic UUID, not a foreign key to every business table. |

## View

| Object | What | Why | How |
|---|---|---|---|
| journey_summary | Query combining journeys, booking parties, container number, latest effective event and open-hold flag. | Gives dashboards and tracking lists a consistent current-state projection. | Query the view with API authorization filters. It excludes replaced events and selects latest by occurredon, then createdon and ID. lastrecordedon is the entry time of that selected event, not necessarily the latest write anywhere in the journey. endedon/outcome remain the authoritative closure fields. |

## Functions

| Object | What | Why | How |
|---|---|---|---|
| validate_user_scope | Trigger function that checks role-to-organization consistency. | Prevents a shipper login pointing to a consignee organization or an externally scoped admin. | users_scope calls it before every user insert/update. It validates the row, not the caller's permission to create accounts. |
| stamp_audit_columns | Trigger function that stamps actor IDs and database entry times. | Standardizes audit fields and preserves original creation metadata. | API sets transaction-local kodiak.actor_id; the trigger reads it, stamps updates and preserves created fields on update. Missing actor context produces null actor IDs for bootstrap/system/anonymous operations. |
| log_change | Trigger function that inserts an audit_logs row after a successful row change. | Records changes consistently across business tables in the same transaction. | Trigger reads kodiak.request_id and kodiak.change_reason when supplied. UPDATE entries list changed field names excluding updatedby/updatedon; INSERT/DELETE entries use an empty list. Audit rows are excluded from recursive logging. |
| reject_history_mutation | Trigger function that rejects UPDATE and DELETE on history tables. | Preserves review, tracking and audit evidence. | preserve_history invokes it; add new review/correction records instead. Runtime permissions must additionally prohibit TRUNCATE and trigger disabling. |

## Triggers

| Object | What | Why | How |
|---|---|---|---|
| users_scope | BEFORE INSERT/UPDATE trigger on users. | Applies the organization/role consistency check automatically. | Calls validate_user_scope for each affected user row. |
| stamp_audit | BEFORE INSERT/UPDATE trigger on every table. | Ensures all 26 tables use the same four audit fields. | Calls stamp_audit_columns once per row; new rows initialize both created and updated metadata. |
| log_change | AFTER INSERT/UPDATE/DELETE trigger on all tables except audit_logs. | Captures successful changes without application code manually inserting generic audit records. | Calls log_change once per row; its audit entry rolls back if the enclosing transaction rolls back. |
| preserve_history | BEFORE UPDATE/DELETE trigger on audit_logs, tracking_events, booking_reviews, shipper_reviews and shipper_consignee_history. | Prevents accidental overwriting or deletion of historical evidence. | Calls reject_history_mutation; ordinary application workflows append records instead. |

## Explicit indexes

The database maintains indexes automatically. They can help matching queries but do not grant permissions; PostgreSQL chooses a plan based on query shape and data volume.

| Object | What | Why | How |
|---|---|---|---|
| ux_users_email | Unique index on normalized email. | Prevents duplicate logins differing only by case or surrounding spaces. | Normalize email comparisons with lower(btrim(email)); duplicate writes fail. |
| ux_container_active_journey | Unique partial index on allocated containers in open journeys. | Prevents concurrent double allocation. | Applies only while endedon is null and containerid is set; closed movements do not prevent reuse. |
| ux_shipper_blacklist | Unique partial index on shipper targets of open blacklists. | Prevents duplicate active restrictions for one shipper. | Rows with null releasedon participate; released history permits a later new restriction. |
| ux_container_blacklist | Unique partial index on container targets of open blacklists. | Prevents duplicate active restrictions for one container. | Rows with null releasedon participate; released history remains available. |
| ix_shippers_consignee | Index on current shipper consignee. | Supports listing the consignee's current shipper network. | Filter shippers by consigneeid; historical booking access uses booking assignment instead. |
| ix_bookings_shipper | Index on shipper and descending creation time. | Supports recent booking lists for a shipper. | Filter by shipperid and order by createdon descending. |
| ix_bookings_consignee | Index on retained consignee and descending creation time. | Supports recent bookings for a consignee's authorized records. | Filter by consigneeid and order by createdon descending. |
| ix_bookings_status | Index on booking status and descending creation time. | Supports review queues such as submitted bookings. | Filter by status and order by creation time. |
| ix_journeys_booking | Index on journey booking ID. | Supports loading the containers fulfilling a booking. | Filter container_journeys by bookingid. |
| ix_journeys_eta | Partial index on expected arrival for open journeys. | Supports operational ETA and overdue candidate queries. | Filter endedon IS NULL and expectedarrival by date/time; API/report must additionally account for already-arrived shipments when calculating overdue arrivals. |
| ix_events_timeline | Index on journey and descending occurrence/entry time. | Supports timeline and latest-event lookups. | Filter by journeyid and order by occurredon then createdon. |
| ix_holds_open | Partial index on journey ID for unreleased holds. | Supports current hold checks without scanning released history. | Filter journeyid and releasedon IS NULL. |
| ix_documents_booking | Index on document booking parent. | Supports loading booking attachments. | Filter documents by bookingid and apply visibility/access checks. |
| ix_documents_journey | Index on document journey parent. | Supports loading journey attachments. | Filter documents by journeyid and apply visibility/access checks. |
| ix_documents_shipper | Index on document shipper parent. | Supports loading onboarding/company attachments. | Filter documents by shipperid and apply visibility/access checks. |
| ix_notifications_unread | Partial index on recipient and descending creation time for unread messages. | Supports unread counts and recent unread notifications. | Filter recipientid and readon IS NULL. |
| ix_tokens_user | Index on token account owner. | Supports finding and revoking a user's issued tokens. | Filter by userid; tokenhash's unique constraint separately supports token lookup. |
| ix_audit_entity | Index on entity name, ID and descending creation time. | Supports a particular record's change history. | Filter by entityname and entityid and order by createdon descending. |

## Common columns

| Column | What | Why | How |
|---|---|---|---|
| id | UUID primary key of a table row. | Gives references a stable identity independent of names and display numbers. | Defaults to gen_random_uuid(); reference this value in foreign keys. |
| createdby | User who inserted the record, when an authenticated actor exists. | Attributes initial data entry. | Trigger reads transaction-local kodiak.actor_id; null is allowed for seed/system/anonymous operations. References users.id. |
| updatedby | User responsible for the latest insert/update, when an actor exists. | Attributes the latest write. | Initialized on insert and refreshed on update by the trigger; references users.id. |
| createdon | Database insertion timestamp. | Records when the system first stored the row. | Database stamps it and preserves it during updates; it is not the business event occurrence time. |
| updatedon | Timestamp of the latest insert/update. | Identifies when this row was last written. | Database stamps it on insert/update. Child-row changes do not automatically touch a parent row's timestamp. |

Other columns are defined in the SQL next to their types, references and checks. This guide describes object behavior, not a separate per-column data dictionary.

## Constraints and implicit indexes

Primary keys identify rows; foreign keys preserve valid relationships; UNIQUE constraints prevent duplicates; CHECK constraints validate row-local rules; NOT NULL marks required values. PostgreSQL creates supporting indexes for primary keys and UNIQUE constraints. Foreign keys do not automatically create an index on the referencing column; the explicit indexes above support common access paths.

Composite foreign keys ensure a journey belongs to the specified booking item, an event's leg/corrected event belongs to the same journey, and a port's state belongs to its country. Quantity checks, blacklist checks at booking time, transition permissions and historical-access policies require API transactions as explained in README.md.

## Example: a shipment from Nhava Sheva to Jebel Ali

1. Admin creates a consignee, approves a shipper under it and creates named accounts.
2. Shipper submits one booking with a line requesting two 20 ft containers.
3. Admin confirms the booking, inserts a review and creates two journeys, allocating one physical container to each.
4. Admin adds route legs and tracking events separately for each journey.
5. If a document is missing, admin places one journey on hold. Its physical progress remains visible.
6. A corrected milestone is a new tracking event linked to the earlier event; the original remains in history.
7. Admin releases any holds and records final delivery/closure. Closing a journey permits later reuse of its container.
8. The consignee sees bookings assigned to it; the shipper sees its own bookings. API authorization applies to every related document, notification and report.

## Applying database descriptions

Run `004_object_comments.sql` after migrations 001 and 002. It adds PostgreSQL COMMENT metadata visible in tools such as pgAdmin and can be reapplied to refresh descriptions. It does not change business data or schema behavior. Script 003 is an optional rollback-only validation script, not a deployment migration.
