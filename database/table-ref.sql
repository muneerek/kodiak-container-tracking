create table userDetails(userId bigint identity(1,1) primary key  cluster,userType int /* normal user,consignee,shipper*/,firstName nvarchar(300),lastName nvarchar(300),
dob date ,emailId nvarhcar(150),userStatus int /*active ,terminated ,*/,userName nvarchar(256),userPassword nvarchar(max),emailConfirmed boolean,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50))

create table userLoginDetails(loginID int identity(1,1) primary key ,fkUserId bigint ,loginTime datetime ,logoutTime datetime)
create table userTokens(tokenId int identity(1,1) primary key,fkUserId  bigint ,purpose text ,tokenhash text ,expireson timestamptz ,
usedon timestamptz,revokedon timestamptz,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50)  )

create table userRoles(roleId int identity(1,1) primary key ,code varchar(100) ,roleName varchar(250) ,isActive bit,,createOn datetime ,createdBy varchar(50) 
,updatedOn datetime ,updatedBy varchar(50) )