-- Run after 001 and 002 on a development/test database. All test data rolls back.
BEGIN;
SET LOCAL search_path = kodiak, public;
DO $$
DECLARE
 adminid uuid; consignee uuid; shipper uuid; booking uuid; item uuid; container uuid;
 journey uuid; eventid uuid; statusid uuid; originid uuid; destinationid uuid; typeid uuid; n integer;
BEGIN
 SELECT count(*) INTO n FROM information_schema.columns
 WHERE table_schema='kodiak' AND table_name IN (SELECT tablename FROM pg_tables WHERE schemaname='kodiak')
 AND column_name IN ('createdby','updatedby','createdon','updatedon');
 IF n <> 104 THEN RAISE EXCEPTION 'Expected audit fields on 26 tables; found %',n; END IF;
 INSERT INTO users(roleid,displayname,email) SELECT id,'Test Admin','schema-test@example.invalid' FROM roles WHERE code='ADMIN' RETURNING id INTO adminid;
 PERFORM set_config('kodiak.actor_id',adminid::text,true);
 INSERT INTO consignees(code,name) VALUES('TEST-C','Test Consignee') RETURNING id INTO consignee;
 INSERT INTO shippers(code,name,consigneeid,approvalstatus) VALUES('TEST-S','Test Shipper',consignee,'APPROVED') RETURNING id INTO shipper;
 IF (SELECT createdby FROM shippers WHERE id=shipper) IS DISTINCT FROM adminid THEN RAISE EXCEPTION 'Actor not stamped'; END IF;
 BEGIN
 INSERT INTO users(roleid,displayname,email) SELECT id,'Invalid Shipper','bad@example.invalid' FROM roles WHERE code='SHIPPER';
 RAISE EXCEPTION 'FAIL: unscoped shipper accepted';
 EXCEPTION WHEN raise_exception THEN IF SQLERRM LIKE 'FAIL:%' THEN RAISE; END IF; END;
 BEGIN
 INSERT INTO users(roleid,displayname,email) SELECT id,'Duplicate','SCHEMA-TEST@example.invalid' FROM roles WHERE code='ADMIN';
 RAISE EXCEPTION 'FAIL: duplicate email accepted';
 EXCEPTION WHEN unique_violation THEN NULL; END;
 SELECT id INTO originid FROM ports WHERE code='INNSA';
 SELECT id INTO destinationid FROM ports WHERE code='AEJEA';
 SELECT id INTO typeid FROM container_types WHERE code='20GP';
 INSERT INTO bookings(bookingnumber,shipperid,consigneeid,originportid,destinationportid,cargodescription)
 VALUES('TEST-B',shipper,consignee,originid,destinationid,'Test cargo') RETURNING id INTO booking;
 INSERT INTO booking_items(bookingid,containertypeid,quantity) VALUES(booking,typeid,1) RETURNING id INTO item;
 INSERT INTO containers(containernumber,containertypeid) VALUES('TESU1234567',typeid) RETURNING id INTO container;
 INSERT INTO container_journeys(journeynumber,bookingid,bookingitemid,containerid)
 VALUES('TEST-J',booking,item,container) RETURNING id INTO journey;
 BEGIN
 INSERT INTO container_journeys(journeynumber,bookingid,bookingitemid,containerid) VALUES('TEST-J2',booking,item,container);
 RAISE EXCEPTION 'FAIL: second open allocation accepted';
 EXCEPTION WHEN unique_violation THEN NULL; END;
 SELECT id INTO statusid FROM tracking_statuses WHERE code='AT_ORIGIN';
 INSERT INTO tracking_events(journeyid,statusid,occurredon,publicnotes) VALUES(journey,statusid,now(),'Original') RETURNING id INTO eventid;
 INSERT INTO tracking_events(journeyid,statusid,occurredon,supersedeseventid,correctionreason,publicnotes)
 VALUES(journey,statusid,now(),eventid,'Correct note','Corrected');
 IF (SELECT count(*) FROM journey_summary WHERE id=journey AND statuscode='AT_ORIGIN') <> 1 THEN RAISE EXCEPTION 'Summary failure'; END IF;
 BEGIN
 UPDATE tracking_events SET publicnotes='Overwrite' WHERE id=eventid;
 RAISE EXCEPTION 'FAIL: history mutation accepted';
 EXCEPTION WHEN raise_exception THEN IF SQLERRM LIKE 'FAIL:%' THEN RAISE; END IF; END;
 INSERT INTO journey_holds(journeyid,reasonid,internalreason,heldby)
 SELECT journey,id,'Test hold',adminid FROM restriction_reasons WHERE code='DOCUMENTATION';
 IF NOT (SELECT isonhold FROM journey_summary WHERE id=journey) THEN RAISE EXCEPTION 'Hold missing from summary'; END IF;
 INSERT INTO blacklists(shipperid,reasonid,internalreason,blockedby)
 SELECT shipper,id,'Test block',adminid FROM restriction_reasons WHERE code='OTHER';
 BEGIN
 INSERT INTO blacklists(shipperid,reasonid,internalreason,blockedby)
 SELECT shipper,id,'Duplicate block',adminid FROM restriction_reasons WHERE code='OTHER';
 RAISE EXCEPTION 'FAIL: duplicate active blacklist accepted';
 EXCEPTION WHEN unique_violation THEN NULL; END;
 IF NOT EXISTS(SELECT 1 FROM audit_logs WHERE entityid=booking AND createdby=adminid) THEN RAISE EXCEPTION 'Audit entry missing'; END IF;
 RAISE NOTICE 'Schema smoke tests passed';
END $$;
ROLLBACK;
