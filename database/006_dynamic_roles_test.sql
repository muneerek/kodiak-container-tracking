-- Run after 005. All fixtures roll back.
BEGIN;
SET LOCAL search_path=kodiak,public;
DO $$
DECLARE u uuid; u2 uuid; r1 uuid; r2 uuid; externalrole uuid; p uuid; n integer;
BEGIN
 INSERT INTO users(accounttype,displayname,email) VALUES('INTERNAL','Test One','rbac-one@example.invalid') RETURNING id INTO u;
 PERFORM set_config('kodiak.actor_id',u::text,true);
 INSERT INTO users(accounttype,displayname,email) VALUES('INTERNAL','Test Two','rbac-two@example.invalid') RETURNING id INTO u2;
 INSERT INTO roles(code,name,accounttype) VALUES('CUSTOM_ONE','Coordinator','INTERNAL') RETURNING id INTO r1;
 INSERT INTO roles(code,name,accounttype) VALUES('CUSTOM_TWO','Viewer','INTERNAL') RETURNING id INTO r2;
 SELECT id INTO p FROM permissions WHERE code='reports.read' AND accounttype='INTERNAL';
 INSERT INTO role_permissions(roleid,permissionid,accounttype) VALUES(r1,p,'INTERNAL'),(r2,p,'INTERNAL');
 INSERT INTO user_roles(userid,roleid,accounttype) VALUES(u,r1,'INTERNAL'),(u,r2,'INTERNAL'),(u2,r1,'INTERNAL');
 SELECT count(*) INTO n FROM user_effective_permissions WHERE userid=u AND permissioncode='reports.read';
 IF n<>1 THEN RAISE EXCEPTION 'Multiple roles must produce one effective capability'; END IF;
 IF NOT EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u2) THEN RAISE EXCEPTION 'Shared role not effective'; END IF;
 UPDATE roles SET name='Renamed Role',code='RENAMED_CUSTOM' WHERE id=r1;
 IF NOT EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u2) THEN RAISE EXCEPTION 'Rename altered grants'; END IF;
 UPDATE roles SET isactive=false WHERE id=r1;
 IF EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u2) THEN RAISE EXCEPTION 'Disabled role grants access'; END IF;
 IF NOT EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u) THEN RAISE EXCEPTION 'Second role lost'; END IF;
 UPDATE permissions SET isactive=false WHERE id=p;
 IF EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u) THEN RAISE EXCEPTION 'Disabled permission grants access'; END IF;
 UPDATE permissions SET isactive=true WHERE id=p;
 UPDATE users SET isactive=false WHERE id=u;
 IF EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u) THEN RAISE EXCEPTION 'Disabled user grants access'; END IF;
 SELECT id INTO externalrole FROM roles WHERE code='SHIPPER';
 BEGIN
 INSERT INTO user_roles(userid,roleid,accounttype) VALUES(u,externalrole,'INTERNAL');
 RAISE EXCEPTION 'Cross-scope role accepted';
 EXCEPTION WHEN foreign_key_violation THEN NULL; END;
 BEGIN
 INSERT INTO role_permissions(roleid,permissionid,accounttype) VALUES(externalrole,p,'SHIPPER');
 RAISE EXCEPTION 'Internal permission given to external role';
 EXCEPTION WHEN foreign_key_violation THEN NULL; END;
 BEGIN
 INSERT INTO user_roles(userid,roleid,accounttype) VALUES(u,r1,'INTERNAL');
 RAISE EXCEPTION 'Duplicate role assignment accepted';
 EXCEPTION WHEN unique_violation THEN NULL; END;
 BEGIN
 INSERT INTO users(accounttype,displayname,email) VALUES('SHIPPER','Unscoped','unscoped@example.invalid');
 RAISE EXCEPTION 'Unscoped shipper accepted';
 EXCEPTION WHEN check_violation THEN NULL; END;
 DELETE FROM user_roles WHERE userid=u;
 IF EXISTS(SELECT 1 FROM user_effective_permissions WHERE userid=u) THEN RAISE EXCEPTION 'Zero roles grants access'; END IF;
 SELECT count(*) INTO n FROM information_schema.columns WHERE table_schema='kodiak'
 AND table_name IN (SELECT tablename FROM pg_tables WHERE schemaname='kodiak')
 AND column_name IN ('createdby','updatedby','createdon','updatedon');
 IF n<>116 THEN RAISE EXCEPTION 'Missing audit columns: %',n; END IF;
 IF NOT EXISTS(SELECT 1 FROM audit_logs WHERE entityname='user_roles' AND createdby=u) THEN RAISE EXCEPTION 'Missing grant audit'; END IF;
 RAISE NOTICE 'Dynamic roles checks passed';
END $$;
ROLLBACK;
