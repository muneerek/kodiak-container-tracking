create table applicationUser(appUserID int identiry(1,1) primary key ,userName nvarchar(256),userPassword nvarchar(max),linkedUnqueID int ,linkedUniqueCode varchar(50),
userType int /* 0 normal user,1 admin,2 consignee,3 shipper*/,isActive bit default(1)

,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50))

)

create table userLoginDetails(loginID int identity(1,1) primary key ,fkappUserId bigint refrence applicationUser ,loginTime datetime ,logoutTime datetime)
create table userTokens(tokenId int identity(1,1) primary key,fkappUserId  bigint refrence applicationUser ,purpose text ,tokenhash text ,expireson timestamptz ,
usedon timestamptz,revokedon timestamptz,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50)  )

create table userRoles(roleId int identity(1,1) primary key ,code varchar(100) ,roleName varchar(250) ,isActive bit,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50) )
create table userRolesmapin(mapinRoleId int identity(1,1) primary key ,fkappUserId  bigint refrence applicationUser  ,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50) )

create table employeeDetails(EmployeeID int identity(1,1) primary key ,firstName nvarchar(300),lastName nvarchar(300),Logo nvarchar(max),
dob date ,emailId nvarhcar(150),userStatus int /*active ,terminated ,*/  
designationID int ,OrgID int ,
,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50) )
create table company
create table designation
create table organizationHirarchy
create table conuntryMaster
create table StateMaster

create table shipperDetails(shipperID int identity(1,1) primary key ,CompanyName nvarchar(300),lastName nvarchar(300),Logo nvarchar(max),
 emailId nvarhcar(150), status int /* active , black listed */,
 Phonenumber ,Country int 	,Address,Businesscategory int	,
,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50) )


create table consigneeDetails(consigneID int identity(1,1) primary key ,CompanyName nvarchar(300),lastName nvarchar(300),Logo nvarchar(max),
 emailId nvarhcar(150), status int /* active , black listed */,
 Phonenumber ,Country int 	,Address, 
,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50) )
