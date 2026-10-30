-- PostgreSQL user module following the requested names.
-- REVIEW DESIGN: isolated schema; does not replace the existing kodiak tables.
-- Run once. Camel-case names are quoted and must be quoted in SQL queries.
BEGIN;
CREATE SCHEMA user_design;
SET LOCAL search_path = user_design, public;

CREATE TABLE "userDetails" (
 "userId" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "userType" integer NOT NULL CHECK ("userType" IN (1,2,3)), -- 1 internal/normal, 2 consignee, 3 shipper
 "firstName" varchar(300) NOT NULL,
 "lastName" varchar(300),
 "dob" date,
 "emailId" varchar(150) NOT NULL CHECK (length(btrim("emailId")) > 0),
 "userStatus" integer NOT NULL DEFAULT 1 CHECK ("userStatus" IN (1,2,3)), -- active, terminated, suspended
 "userName" varchar(256) NOT NULL CHECK (length(btrim("userName")) > 0),
 "userPassword" text NOT NULL, -- Password HASH only; never plaintext or reversible encryption.
 "emailConfirmed" boolean NOT NULL DEFAULT false,
 "createOn" timestamptz NOT NULL DEFAULT now(),
 "createdBy" bigint REFERENCES "userDetails"("userId"),
 "updatedOn" timestamptz NOT NULL DEFAULT now(),
 "updatedBy" bigint REFERENCES "userDetails"("userId")
);
CREATE UNIQUE INDEX ux_user_email ON "userDetails" (lower(btrim("emailId")));
CREATE UNIQUE INDEX ux_user_name ON "userDetails" (lower(btrim("userName")));

CREATE TABLE "userLoginDetails" (
 "loginID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "fkUserId" bigint NOT NULL REFERENCES "userDetails"("userId"),
 "loginTime" timestamptz NOT NULL DEFAULT now(),
 "logoutTime" timestamptz,
 "createOn" timestamptz NOT NULL DEFAULT now(),
 "createdBy" bigint REFERENCES "userDetails"("userId"),
 "updatedOn" timestamptz NOT NULL DEFAULT now(),
 "updatedBy" bigint REFERENCES "userDetails"("userId"),
 CHECK ("logoutTime" >= "loginTime")
);
CREATE INDEX ix_user_login_time ON "userLoginDetails" ("fkUserId", "loginTime" DESC);

CREATE TABLE "userTokens" (
 "tokenId" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "fkUserId" bigint NOT NULL REFERENCES "userDetails"("userId"),
 "purpose" text NOT NULL CHECK ("purpose" IN ('REFRESH','RESET_PASSWORD','VERIFY_EMAIL')),
 "tokenhash" text NOT NULL UNIQUE,
 "expireson" timestamptz NOT NULL,
 "usedon" timestamptz,
 "revokedon" timestamptz,
 "createOn" timestamptz NOT NULL DEFAULT now(),
 "createdBy" bigint REFERENCES "userDetails"("userId"),
 "updatedOn" timestamptz NOT NULL DEFAULT now(),
 "updatedBy" bigint REFERENCES "userDetails"("userId"),
 CHECK ("expireson" > "createOn")
);
CREATE INDEX ix_user_tokens_user ON "userTokens" ("fkUserId");

-- Role MASTER: one row per custom role, not one row per assignment.
CREATE TABLE "userRoles" (
 "roleId" integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "code" varchar(100) NOT NULL CHECK (length(btrim("code")) > 0),
 "roleName" varchar(250) NOT NULL CHECK (length(btrim("roleName")) > 0),
 "isActive" boolean NOT NULL DEFAULT true,
 "createOn" timestamptz NOT NULL DEFAULT now(),
 "createdBy" bigint REFERENCES "userDetails"("userId"),
 "updatedOn" timestamptz NOT NULL DEFAULT now(),
 "updatedBy" bigint REFERENCES "userDetails"("userId")
);
CREATE UNIQUE INDEX ux_role_code ON "userRoles" (lower(btrim("code")));

CREATE TABLE "userRoleMapping" (
 "mappingId" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "fkUserId" bigint NOT NULL REFERENCES "userDetails"("userId"),
 "fkRoleId" integer NOT NULL REFERENCES "userRoles"("roleId"),
 "createOn" timestamptz NOT NULL DEFAULT now(),
 "createdBy" bigint REFERENCES "userDetails"("userId"),
 "updatedOn" timestamptz NOT NULL DEFAULT now(),
 "updatedBy" bigint REFERENCES "userDetails"("userId"),
 UNIQUE ("fkUserId", "fkRoleId")
);
CREATE INDEX ix_role_mapping_role ON "userRoleMapping" ("fkRoleId");

CREATE FUNCTION stamp_user_audit() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE actor bigint := nullif(current_setting('kodiak.actor_id',true),'')::bigint;
BEGIN
 IF TG_OP='INSERT' THEN
 NEW."createOn" := clock_timestamp(); NEW."createdBy" := actor;
 ELSE
 NEW."createOn" := OLD."createOn"; NEW."createdBy" := OLD."createdBy";
 END IF;
 NEW."updatedOn" := clock_timestamp(); NEW."updatedBy" := actor;
 RETURN NEW;
END $$;
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['userDetails','userLoginDetails','userTokens','userRoles','userRoleMapping'] LOOP
 EXECUTE format('CREATE TRIGGER stamp_audit BEFORE INSERT OR UPDATE ON user_design.%I FOR EACH ROW EXECUTE FUNCTION user_design.stamp_user_audit()',t);
 END LOOP;
END $$;
COMMENT ON TABLE "userDetails" IS 'What: named user accounts. Why: separate login identity from dynamic job roles. How: userType identifies business category only; normal/internal users are not automatically administrators. Store only password hashes.';
COMMENT ON TABLE "userLoginDetails" IS 'What: successful login sessions. Why: record login/logout history. How: insert per successful login and update that specific loginID on explicit logout. Null logoutTime is not proof the session is still active.';
COMMENT ON TABLE "userTokens" IS 'What: hashed authentication/recovery tokens. Why: support expiry and revocation. How: API checks purpose, expiry and revocation and atomically consumes/rotates tokens.';
COMMENT ON TABLE "userRoles" IS 'What: configurable role master. Why: support arbitrary job roles. How: create custom roles and assign them through userRoleMapping; names alone confer no permissions.';
COMMENT ON TABLE "userRoleMapping" IS 'What: user-to-role junction. Why: multiple users per role and multiple roles per user. How: insert/remove unique assignments; authorization requires subsequent permission and organization integration.';
COMMIT;
