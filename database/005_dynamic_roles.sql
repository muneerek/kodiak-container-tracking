-- Run once AFTER 001, 002 and 004. Preserves existing user assignments.
BEGIN;
SET LOCAL search_path = kodiak, public;
ALTER TABLE roles DROP CONSTRAINT roles_code_check;
ALTER TABLE roles ADD COLUMN description text,
 ADD COLUMN accounttype text,
 ADD COLUMN isactive boolean NOT NULL DEFAULT true;
UPDATE roles SET accounttype = CASE code WHEN 'ADMIN' THEN 'INTERNAL' ELSE code END;
ALTER TABLE roles ALTER COLUMN accounttype SET NOT NULL;
ALTER TABLE roles ADD CONSTRAINT roles_accounttype_check CHECK(accounttype IN ('INTERNAL','SHIPPER','CONSIGNEE')),
 ADD CONSTRAINT roles_id_accounttype_key UNIQUE(id,accounttype);
ALTER TABLE users ADD COLUMN accounttype text;
UPDATE users u SET accounttype = r.accounttype FROM roles r WHERE r.id=u.roleid;
ALTER TABLE users ALTER COLUMN accounttype SET NOT NULL;
ALTER TABLE users ADD CONSTRAINT users_account_scope_check CHECK(
 (accounttype='INTERNAL' AND shipperid IS NULL AND consigneeid IS NULL) OR
 (accounttype='SHIPPER' AND shipperid IS NOT NULL AND consigneeid IS NULL) OR
 (accounttype='CONSIGNEE' AND consigneeid IS NOT NULL AND shipperid IS NULL)),
 ADD CONSTRAINT users_id_accounttype_key UNIQUE(id,accounttype);

CREATE TABLE permissions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), code text NOT NULL,
 accounttype text NOT NULL CHECK(accounttype IN ('INTERNAL','SHIPPER','CONSIGNEE')),
 name text NOT NULL, description text NOT NULL, isactive boolean NOT NULL DEFAULT true,
 UNIQUE(code,accounttype), UNIQUE(id,accounttype)
);
CREATE TABLE role_permissions (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), roleid uuid NOT NULL, permissionid uuid NOT NULL,
 accounttype text NOT NULL,
 FOREIGN KEY(roleid,accounttype) REFERENCES roles(id,accounttype),
 FOREIGN KEY(permissionid,accounttype) REFERENCES permissions(id,accounttype),
 UNIQUE(roleid,permissionid)
);
CREATE TABLE user_roles (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), userid uuid NOT NULL, roleid uuid NOT NULL,
 accounttype text NOT NULL,
 FOREIGN KEY(userid,accounttype) REFERENCES users(id,accounttype),
 FOREIGN KEY(roleid,accounttype) REFERENCES roles(id,accounttype),
 UNIQUE(userid,roleid)
);
DO $$ DECLARE t text; BEGIN
 FOREACH t IN ARRAY ARRAY['permissions','role_permissions','user_roles'] LOOP
 EXECUTE format('ALTER TABLE kodiak.%I ADD COLUMN createdby uuid REFERENCES kodiak.users,
 ADD COLUMN updatedby uuid REFERENCES kodiak.users,
 ADD COLUMN createdon timestamptz NOT NULL DEFAULT now(), ADD COLUMN updatedon timestamptz NOT NULL DEFAULT now()',t);
 EXECUTE format('CREATE TRIGGER stamp_audit BEFORE INSERT OR UPDATE ON kodiak.%I FOR EACH ROW EXECUTE FUNCTION kodiak.stamp_audit_columns()',t);
 EXECUTE format('CREATE TRIGGER log_change AFTER INSERT OR UPDATE OR DELETE ON kodiak.%I FOR EACH ROW EXECUTE FUNCTION kodiak.log_change()',t);
 END LOOP;
END $$;
INSERT INTO user_roles(userid,roleid,accounttype) SELECT id,roleid,accounttype FROM users;
DROP TRIGGER users_scope ON users;
DROP FUNCTION validate_user_scope();
ALTER TABLE users DROP COLUMN roleid;
CREATE INDEX ix_user_roles_role ON user_roles(roleid);
CREATE INDEX ix_role_permissions_permission ON role_permissions(permissionid);

-- Permissions are API capabilities, not arbitrary executable rules.
INSERT INTO permissions(code,accounttype,name,description)
SELECT code,scope,name,description FROM (VALUES
 ('dashboard.read','View dashboard','Read dashboard within authorized company scope'),
 ('bookings.read','View bookings','Read bookings within authorized company scope'),
 ('tracking.read','View tracking','Read public tracking within authorized company scope'),
 ('documents.read','View documents','Download allowed documents within authorized company scope'),
 ('reports.read','View reports','Read and export reports within authorized company scope'),
 ('notifications.read','View notifications','Read only own notifications')
) p(code,name,description) CROSS JOIN (VALUES ('INTERNAL'),('SHIPPER'),('CONSIGNEE')) s(scope);
INSERT INTO permissions(code,accounttype,name,description) VALUES
 ('bookings.create','SHIPPER','Create bookings','Create own shipper bookings; approval and blacklist rules still apply'),
 ('documents.upload','SHIPPER','Upload documents','Upload documents to own authorized records'),
 ('bookings.create','INTERNAL','Create bookings','Create on behalf of an eligible shipper'),
 ('bookings.review','INTERNAL','Review bookings','Confirm or reject booking requests'),
 ('tracking.update','INTERNAL','Update tracking','Record and correct official tracking'),
 ('containers.manage','INTERNAL','Manage containers','Maintain containers and allocate journeys'),
 ('holds.manage','INTERNAL','Manage holds','Place and release operational holds'),
 ('blacklists.manage','INTERNAL','Manage blacklists','Restrict and release shippers or containers'),
 ('shippers.manage','INTERNAL','Manage shippers','Approve and maintain shippers and relationships'),
 ('consignees.manage','INTERNAL','Manage consignees','Maintain consignee companies'),
 ('locations.manage','INTERNAL','Manage locations','Maintain country, state and port directories'),
 ('documents.upload','INTERNAL','Upload documents','Upload operational documents'),
 ('documents.internal.read','INTERNAL','Read internal documents','Read internal attachments'),
 ('tracking.internal.read','INTERNAL','Read internal tracking notes','Read internal operational notes'),
 ('users.manage','INTERNAL','Manage users','Maintain user accounts; role assignment requires access.manage'),
 ('access.manage','INTERNAL','Manage access','Create roles, assign permissions and assign user roles'),
 ('audit.read','INTERNAL','Read audit','Read operational audit history');
-- Starter templates only. Roles can be renamed, extended or replaced.
INSERT INTO role_permissions(roleid,permissionid,accounttype)
SELECT r.id,p.id,r.accounttype FROM roles r JOIN permissions p ON p.accounttype=r.accounttype
WHERE r.code IN ('ADMIN','SHIPPER','CONSIGNEE');

CREATE VIEW user_effective_permissions AS
SELECT DISTINCT u.id AS userid,p.code AS permissioncode,u.accounttype,u.shipperid,u.consigneeid
FROM users u JOIN user_roles ur ON ur.userid=u.id
JOIN roles r ON r.id=ur.roleid AND r.isactive
JOIN role_permissions rp ON rp.roleid=r.id
JOIN permissions p ON p.id=rp.permissionid AND p.isactive
WHERE u.isactive;
COMMENT ON TABLE roles IS 'What: configurable named permission bundles. Why: support custom job roles. How: create a role for one account type and assign capabilities through role_permissions; role codes confer no automatic privilege.';
COMMENT ON TABLE users IS 'What: named accounts with organization scope. Why: separate identity and data ownership from job roles. How: assign zero or more compatible roles via user_roles; zero roles grants no capabilities.';
COMMENT ON TABLE permissions IS 'What: API capability catalog per account type. Why: define actions that can be granted. How: select supported capability codes for roles; new capability codes need corresponding API implementation.';
COMMENT ON TABLE role_permissions IS 'What: many-to-many role/capability grants. Why: configure role behavior. How: insert or delete compatible grants; changes are audited. No deny or inheritance rules.';
COMMENT ON TABLE user_roles IS 'What: many-to-many user/role assignments. Why: support many users per role and multiple roles per user. How: insert or remove assignments; matching account types are enforced by composite foreign keys.';
COMMENT ON VIEW user_effective_permissions IS 'What: union of active grants for active users. Why: resolve multiple roles without duplicates. How: API checks capability plus organization scope and business eligibility; this view alone is not row-level security.';
COMMENT ON COLUMN users.accounttype IS 'What: business access boundary, not a role. Why: prevent custom role grants crossing organization types. How: INTERNAL has no external organization; SHIPPER/CONSIGNEE requires its matching company ID.';
COMMENT ON COLUMN roles.accounttype IS 'What: eligible account type for this role. Why: separate role customization from company access. How: must match assigned users and permissions.';
COMMIT;
