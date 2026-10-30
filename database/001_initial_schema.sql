-- Kodiak PostgreSQL initial schema. Run once on an empty database.
-- Application authentication/authorization must be enforced by the ASP.NET API.
BEGIN;
CREATE SCHEMA kodiak;
SET LOCAL search_path = kodiak, public;

CREATE TABLE countries (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code varchar(2) NOT NULL UNIQUE CHECK (code ~ '^[A-Z]{2}$'),
 name text NOT NULL, isactive boolean NOT NULL DEFAULT true
);
CREATE TABLE states (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), countryid uuid NOT NULL REFERENCES countries,
 code text NOT NULL, name text NOT NULL, isactive boolean NOT NULL DEFAULT true,
 UNIQUE(countryid, code), UNIQUE(id, countryid)
);
CREATE TABLE ports (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), countryid uuid NOT NULL REFERENCES countries,
 stateid uuid, code text NOT NULL UNIQUE, name text NOT NULL, city text,
 timezone text NOT NULL DEFAULT 'UTC', isactive boolean NOT NULL DEFAULT true,
 FOREIGN KEY(stateid, countryid) REFERENCES states(id, countryid)
);
CREATE TABLE consignees (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, name text NOT NULL,
 contactname text, email text, phone text, address text, countryid uuid REFERENCES countries,
 registrationnumber text, isactive boolean NOT NULL DEFAULT true
);
CREATE TABLE shippers (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, name text NOT NULL,
 consigneeid uuid REFERENCES consignees, contactname text, email text, phone text, address text,
 countryid uuid REFERENCES countries, registrationnumber text,
 approvalstatus text NOT NULL DEFAULT 'PENDING' CHECK (approvalstatus IN ('PENDING','CHANGES_REQUESTED','APPROVED','REJECTED')),
 isactive boolean NOT NULL DEFAULT true,
 CHECK (approvalstatus <> 'APPROVED' OR consigneeid IS NOT NULL)
);
CREATE TABLE roles (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE CHECK(code IN ('ADMIN','CONSIGNEE','SHIPPER')), name text NOT NULL
);
CREATE TABLE users (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), roleid uuid NOT NULL REFERENCES roles,
 shipperid uuid REFERENCES shippers, consigneeid uuid REFERENCES consignees,
 displayname text NOT NULL, email text NOT NULL, passwordhash text,
 isactive boolean NOT NULL DEFAULT true, emailconfirmed boolean NOT NULL DEFAULT false,
 failedlogincount integer NOT NULL DEFAULT 0 CHECK(failedlogincount >= 0), lockoutuntil timestamptz,
 securitystamp uuid NOT NULL DEFAULT gen_random_uuid(), lastloginon timestamptz,
 CHECK (num_nonnulls(shipperid, consigneeid) <= 1)
);
CREATE UNIQUE INDEX ux_users_email ON users(lower(btrim(email)));
CREATE FUNCTION validate_user_scope() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE rolecode text;
BEGIN
 SELECT code INTO rolecode FROM kodiak.roles WHERE id = NEW.roleid;
 IF (rolecode = 'ADMIN' AND num_nonnulls(NEW.shipperid,NEW.consigneeid) <> 0)
 OR (rolecode = 'SHIPPER' AND (NEW.shipperid IS NULL OR NEW.consigneeid IS NOT NULL))
 OR (rolecode = 'CONSIGNEE' AND (NEW.consigneeid IS NULL OR NEW.shipperid IS NOT NULL)) THEN
 RAISE EXCEPTION 'User organization does not match role';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER users_scope BEFORE INSERT OR UPDATE ON users FOR EACH ROW EXECUTE FUNCTION validate_user_scope();

CREATE TABLE container_types (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, name text NOT NULL,
 isrefrigerated boolean NOT NULL DEFAULT false, isactive boolean NOT NULL DEFAULT true
);
CREATE TABLE tracking_statuses (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, name text NOT NULL,
 displayorder integer NOT NULL, isterminal boolean NOT NULL DEFAULT false, isactive boolean NOT NULL DEFAULT true
);
CREATE TABLE document_types (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, name text NOT NULL, isactive boolean NOT NULL DEFAULT true
);
CREATE TABLE restriction_reasons (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL UNIQUE, name text NOT NULL, isactive boolean NOT NULL DEFAULT true
);
CREATE TABLE containers (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), containernumber varchar(11) NOT NULL UNIQUE
 CHECK(containernumber ~ '^[A-Z]{3}[UJZ][0-9]{7}$'),
 containertypeid uuid NOT NULL REFERENCES container_types, notes text, isactive boolean NOT NULL DEFAULT true
);

CREATE TABLE shipper_reviews (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), shipperid uuid NOT NULL REFERENCES shippers,
 decision text NOT NULL CHECK(decision IN ('PENDING','CHANGES_REQUESTED','APPROVED','REJECTED')),
 notes text, reviewedby uuid NOT NULL REFERENCES users, reviewedon timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE shipper_consignee_history (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), shipperid uuid NOT NULL REFERENCES shippers,
 previousconsigneeid uuid REFERENCES consignees, newconsigneeid uuid NOT NULL REFERENCES consignees,
 reason text NOT NULL, effectiveon timestamptz NOT NULL DEFAULT now(),
 CHECK(previousconsigneeid IS DISTINCT FROM newconsigneeid)
);
CREATE TABLE bookings (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bookingnumber text NOT NULL UNIQUE,
 shipperid uuid NOT NULL REFERENCES shippers, consigneeid uuid NOT NULL REFERENCES consignees,
 originportid uuid NOT NULL REFERENCES ports, destinationportid uuid NOT NULL REFERENCES ports,
 status text NOT NULL DEFAULT 'DRAFT' CHECK(status IN ('DRAFT','SUBMITTED','CHANGES_REQUESTED','CONFIRMED','REJECTED','CANCELLED')),
 customerreference text, cargodescription text NOT NULL, handlinginstructions text,
 requesteddeparture date, requestedarrival date, submittedon timestamptz,
 CHECK(originportid <> destinationportid), CHECK(requestedarrival >= requesteddeparture)
);
CREATE TABLE booking_items (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bookingid uuid NOT NULL REFERENCES bookings,
 containertypeid uuid NOT NULL REFERENCES container_types, quantity integer NOT NULL CHECK(quantity > 0),
 totalweightkg numeric(14,3) CHECK(totalweightkg >= 0), totalvolume_m3 numeric(14,3) CHECK(totalvolume_m3 >= 0),
 totalpackages integer CHECK(totalpackages >= 0), cargodescription text,
 UNIQUE(id, bookingid)
);
CREATE TABLE booking_reviews (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bookingid uuid NOT NULL REFERENCES bookings,
 previousstatus text NOT NULL, newstatus text NOT NULL, reason text,
 reviewedby uuid NOT NULL REFERENCES users, reviewedon timestamptz NOT NULL DEFAULT now(),
 CHECK(previousstatus IN ('DRAFT','SUBMITTED','CHANGES_REQUESTED','CONFIRMED','REJECTED','CANCELLED')),
 CHECK(newstatus IN ('DRAFT','SUBMITTED','CHANGES_REQUESTED','CONFIRMED','REJECTED','CANCELLED'))
);
CREATE TABLE container_journeys (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), journeynumber text NOT NULL UNIQUE,
 bookingid uuid NOT NULL REFERENCES bookings, bookingitemid uuid NOT NULL,
 containerid uuid REFERENCES containers, sealnumber text, billofladingnumber text,
 expecteddeparture timestamptz, expectedarrival timestamptz,
 endedon timestamptz, outcome text CHECK(outcome IN ('CLOSED','CANCELLED')),
 returnofjourneyid uuid REFERENCES container_journeys,
 FOREIGN KEY(bookingitemid, bookingid) REFERENCES booking_items(id, bookingid),
 CHECK(expectedarrival >= expecteddeparture), CHECK((endedon IS NULL) = (outcome IS NULL)),
 CHECK(returnofjourneyid IS DISTINCT FROM id)
);
CREATE UNIQUE INDEX ux_container_active_journey ON container_journeys(containerid) WHERE endedon IS NULL AND containerid IS NOT NULL;
CREATE TABLE journey_legs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), journeyid uuid NOT NULL REFERENCES container_journeys,
 sequence integer NOT NULL CHECK(sequence > 0), originportid uuid NOT NULL REFERENCES ports,
 destinationportid uuid NOT NULL REFERENCES ports, vesselname text, voyagenumber text,
 expecteddeparture timestamptz, expectedarrival timestamptz, actualdeparture timestamptz, actualarrival timestamptz,
 UNIQUE(journeyid, sequence), UNIQUE(id, journeyid),
 CHECK(originportid <> destinationportid), CHECK(expectedarrival >= expecteddeparture), CHECK(actualarrival >= actualdeparture)
);
CREATE TABLE tracking_events (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), journeyid uuid NOT NULL REFERENCES container_journeys,
 legid uuid, statusid uuid NOT NULL REFERENCES tracking_statuses, occurredon timestamptz NOT NULL,
 portid uuid REFERENCES ports, locationdescription text, informationsource text,
 publicnotes text, internalnotes text,
 supersedeseventid uuid UNIQUE, correctionreason text,
 UNIQUE(id, journeyid),
 FOREIGN KEY(legid, journeyid) REFERENCES journey_legs(id, journeyid),
 FOREIGN KEY(supersedeseventid, journeyid) REFERENCES tracking_events(id, journeyid),
 CHECK(supersedeseventid IS DISTINCT FROM id),
 CHECK(supersedeseventid IS NULL OR length(btrim(correctionreason)) > 0 AND correctionreason IS NOT NULL)
);
CREATE TABLE journey_holds (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), journeyid uuid NOT NULL REFERENCES container_journeys,
 reasonid uuid NOT NULL REFERENCES restriction_reasons, internalreason text NOT NULL, publicreason text,
 heldon timestamptz NOT NULL DEFAULT now(), heldby uuid NOT NULL REFERENCES users, reviewon date,
 releasedon timestamptz, releasedby uuid REFERENCES users, releasereason text,
 CHECK(releasedon >= heldon),
 CHECK((releasedon IS NULL AND releasedby IS NULL AND releasereason IS NULL) OR
 (releasedon IS NOT NULL AND releasedby IS NOT NULL AND releasereason IS NOT NULL))
);
CREATE TABLE blacklists (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), shipperid uuid REFERENCES shippers, containerid uuid REFERENCES containers,
 reasonid uuid NOT NULL REFERENCES restriction_reasons, internalreason text NOT NULL, publicreason text,
 blockedon timestamptz NOT NULL DEFAULT now(), blockedby uuid NOT NULL REFERENCES users, reviewon date,
 releasedon timestamptz, releasedby uuid REFERENCES users, releasereason text,
 CHECK(num_nonnulls(shipperid, containerid) = 1), CHECK(releasedon >= blockedon),
 CHECK((releasedon IS NULL AND releasedby IS NULL AND releasereason IS NULL) OR
 (releasedon IS NOT NULL AND releasedby IS NOT NULL AND releasereason IS NOT NULL))
);
CREATE UNIQUE INDEX ux_shipper_blacklist ON blacklists(shipperid) WHERE releasedon IS NULL;
CREATE UNIQUE INDEX ux_container_blacklist ON blacklists(containerid) WHERE releasedon IS NULL;
CREATE TABLE documents (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), documenttypeid uuid NOT NULL REFERENCES document_types,
 shipperid uuid REFERENCES shippers, bookingid uuid REFERENCES bookings, journeyid uuid REFERENCES container_journeys,
 originalfilename text NOT NULL, storagekey text NOT NULL UNIQUE, contenttype text NOT NULL,
 sizebytes bigint NOT NULL CHECK(sizebytes > 0), sha256 varchar(64) NOT NULL CHECK(sha256 ~ '^[a-f0-9]{64}$'),
 visibility text NOT NULL DEFAULT 'INTERNAL' CHECK(visibility IN ('INTERNAL','PARTIES')),
 scanstatus text NOT NULL DEFAULT 'PENDING' CHECK(scanstatus IN ('PENDING','CLEAN','REJECTED')),
 CHECK(num_nonnulls(shipperid, bookingid, journeyid) = 1)
);
CREATE TABLE notifications (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), recipientid uuid NOT NULL REFERENCES users,
 bookingid uuid REFERENCES bookings, journeyid uuid REFERENCES container_journeys,
 title text NOT NULL, message text NOT NULL, readon timestamptz,
 CHECK(num_nonnulls(bookingid, journeyid) <= 1)
);
CREATE TABLE user_tokens (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), userid uuid NOT NULL REFERENCES users,
 purpose text NOT NULL CHECK(purpose IN ('REFRESH','RESET_PASSWORD','VERIFY_EMAIL')),
 tokenhash text NOT NULL UNIQUE, expireson timestamptz NOT NULL, usedon timestamptz, revokedon timestamptz
);
CREATE TABLE audit_logs (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), entityname text NOT NULL, entityid uuid NOT NULL,
 action text NOT NULL CHECK(action IN ('INSERT','UPDATE','DELETE')), changedfields jsonb NOT NULL DEFAULT '[]',
 requestid text, reason text
);

-- Same audit columns on EVERY master/transaction table, including junction/history tables.
-- Actor is supplied by the trusted API inside its transaction:
-- SELECT set_config('kodiak.actor_id', '<authenticated user UUID>', true);
DO $$ DECLARE t record; BEGIN
 FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'kodiak' LOOP
 EXECUTE format('ALTER TABLE kodiak.%I
 ADD COLUMN createdby uuid REFERENCES kodiak.users(id),
 ADD COLUMN updatedby uuid REFERENCES kodiak.users(id),
 ADD COLUMN createdon timestamptz NOT NULL DEFAULT now(),
 ADD COLUMN updatedon timestamptz NOT NULL DEFAULT now()', t.tablename);
 END LOOP;
END $$;
CREATE FUNCTION stamp_audit_columns() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE actor uuid := nullif(current_setting('kodiak.actor_id', true), '')::uuid;
BEGIN
 IF TG_OP = 'INSERT' THEN
 NEW.createdby := actor; NEW.createdon := clock_timestamp();
 ELSE
 NEW.createdby := OLD.createdby; NEW.createdon := OLD.createdon;
 END IF;
 NEW.updatedby := actor; NEW.updatedon := clock_timestamp(); RETURN NEW;
END $$;
CREATE FUNCTION log_change() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE rowid uuid; fields jsonb;
BEGIN
 IF TG_OP = 'DELETE' THEN rowid := OLD.id; ELSE rowid := NEW.id; END IF;
 IF TG_OP = 'UPDATE' THEN
 SELECT coalesce(jsonb_agg(n.key), '[]'::jsonb) INTO fields
 FROM jsonb_each(to_jsonb(NEW)) n WHERE n.value IS DISTINCT FROM to_jsonb(OLD)->n.key
 AND n.key NOT IN ('updatedon','updatedby');
 ELSE fields := '[]'::jsonb; END IF;
 INSERT INTO kodiak.audit_logs(entityname,entityid,action,changedfields,requestid,reason)
 VALUES(TG_TABLE_NAME,rowid,TG_OP,fields,nullif(current_setting('kodiak.request_id',true),''),nullif(current_setting('kodiak.change_reason',true),''));
 RETURN NULL;
END $$;
CREATE FUNCTION reject_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION '% is append-only; add a new history/correction record', TG_TABLE_NAME; END $$;
DO $$ DECLARE t record; BEGIN
 FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'kodiak' LOOP
 EXECUTE format('CREATE TRIGGER stamp_audit BEFORE INSERT OR UPDATE ON kodiak.%I FOR EACH ROW EXECUTE FUNCTION kodiak.stamp_audit_columns()', t.tablename);
 IF t.tablename <> 'audit_logs' THEN
 EXECUTE format('CREATE TRIGGER log_change AFTER INSERT OR UPDATE OR DELETE ON kodiak.%I FOR EACH ROW EXECUTE FUNCTION kodiak.log_change()', t.tablename);
 END IF;
 IF t.tablename IN ('audit_logs','tracking_events','booking_reviews','shipper_reviews','shipper_consignee_history') THEN
 EXECUTE format('CREATE TRIGGER preserve_history BEFORE UPDATE OR DELETE ON kodiak.%I FOR EACH ROW EXECUTE FUNCTION kodiak.reject_history_mutation()', t.tablename);
 END IF;
 END LOOP;
END $$;

CREATE INDEX ix_shippers_consignee ON shippers(consigneeid);
CREATE INDEX ix_bookings_shipper ON bookings(shipperid, createdon DESC);
CREATE INDEX ix_bookings_consignee ON bookings(consigneeid, createdon DESC);
CREATE INDEX ix_bookings_status ON bookings(status, createdon DESC);
CREATE INDEX ix_journeys_booking ON container_journeys(bookingid);
CREATE INDEX ix_journeys_eta ON container_journeys(expectedarrival) WHERE endedon IS NULL;
CREATE INDEX ix_events_timeline ON tracking_events(journeyid, occurredon DESC, createdon DESC);
CREATE INDEX ix_holds_open ON journey_holds(journeyid) WHERE releasedon IS NULL;
CREATE INDEX ix_documents_booking ON documents(bookingid);
CREATE INDEX ix_documents_journey ON documents(journeyid);
CREATE INDEX ix_documents_shipper ON documents(shipperid);
CREATE INDEX ix_notifications_unread ON notifications(recipientid, createdon DESC) WHERE readon IS NULL;
CREATE INDEX ix_tokens_user ON user_tokens(userid);
CREATE INDEX ix_audit_entity ON audit_logs(entityname, entityid, createdon DESC);

-- Latest effective event, excluding records replaced by corrections.
CREATE VIEW journey_summary AS
SELECT j.*, b.bookingnumber, b.shipperid, b.consigneeid, b.originportid, b.destinationportid,
 c.containernumber, e.statusid, s.code AS statuscode, e.occurredon AS lastoccurredon,
 e.createdon AS lastrecordedon,
 EXISTS(SELECT 1 FROM journey_holds h WHERE h.journeyid = j.id AND h.releasedon IS NULL) AS isonhold
FROM container_journeys j JOIN bookings b ON b.id = j.bookingid
LEFT JOIN containers c ON c.id = j.containerid
LEFT JOIN LATERAL (
 SELECT te.* FROM tracking_events te WHERE te.journeyid = j.id
 AND NOT EXISTS(SELECT 1 FROM tracking_events replacement WHERE replacement.supersedeseventid = te.id)
 ORDER BY te.occurredon DESC, te.createdon DESC, te.id DESC LIMIT 1
) e ON true LEFT JOIN tracking_statuses s ON s.id = e.statusid;
COMMIT;
