USE DBMaint;
GO

IF OBJECT_ID('dbo.sample_table', 'U') IS NOT NULL
  DROP TABLE dbo.sample_table;
GO

CREATE TABLE dbo.connection
(
	id int NOT NULL PRIMARY KEY,
	startCity varchar(16) NOT NULL, 
	endCity varchar(16) NOT NULL, 
	length int,
    CONSTRAINT PK_connection UNIQUE (startCity, endCity),
	CONSTRAINT UK_connection UNIQUE (endCity, startCity)
);
GO

CREATE TABLE dbo.ticket
(
	startCity varchar(16) NOT NULL, 
	endCity varchar(16) NOT NULL, 
    CONSTRAINT PK_ticket PRIMARY KEY (startCity, endCity)
);
GO
