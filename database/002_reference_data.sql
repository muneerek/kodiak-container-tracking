-- Required reference data only; no demo users or passwords.
BEGIN;
SET LOCAL search_path = kodiak, public;
INSERT INTO roles(code,name) VALUES ('ADMIN','Admin'),('CONSIGNEE','Consignee'),('SHIPPER','Shipper');
INSERT INTO container_types(code,name,isrefrigerated) VALUES
 ('20GP','20 ft Standard',false),('40GP','40 ft Standard',false),
 ('40HC','40 ft High Cube',false),('20RF','20 ft Reefer',true),('40RF','40 ft Reefer',true);
INSERT INTO tracking_statuses(code,name,displayorder,isterminal) VALUES
 ('ASSIGNED','Container assigned',10,false),('AT_ORIGIN','Received at origin',20,false),
 ('DEPARTED','Departed',30,false),('IN_TRANSIT','In transit',40,false),
 ('AT_DESTINATION','Arrived at destination port',50,false),('CLEARED','Customs cleared',60,false),
 ('OUT_FOR_DELIVERY','Out for delivery',70,false),('DELIVERED','Delivered',80,false),
 ('CLOSED','Closed',90,true),('CANCELLED','Cancelled',100,true);
INSERT INTO document_types(code,name) VALUES
 ('REGISTRATION','Company registration'),('BILL_OF_LADING','Bill of lading'),
 ('INVOICE','Commercial invoice'),('PACKING_LIST','Packing list'),
 ('CUSTOMS','Customs document'),('DELIVERY_PROOF','Proof of delivery'),('OTHER','Other');
INSERT INTO restriction_reasons(code,name) VALUES
 ('DOCUMENTATION','Missing or incorrect documents'),('CUSTOMS','Customs review'),
 ('DAMAGE','Container or cargo damage'),('COMPLIANCE','Compliance review'),('OTHER','Other');
INSERT INTO countries(code,name) VALUES ('AE','United Arab Emirates'),('IN','India');
INSERT INTO states(countryid,code,name) SELECT id,'DU','Dubai' FROM countries WHERE code='AE';
INSERT INTO states(countryid,code,name) SELECT id,'MH','Maharashtra' FROM countries WHERE code='IN';
INSERT INTO ports(countryid,stateid,code,name,city,timezone)
 SELECT countryid,id,'AEJEA','Jebel Ali','Dubai','Asia/Dubai' FROM states WHERE code='DU';
INSERT INTO ports(countryid,stateid,code,name,city,timezone)
 SELECT countryid,id,'INNSA','Nhava Sheva','Navi Mumbai','Asia/Kolkata' FROM states WHERE code='MH';
COMMIT;
