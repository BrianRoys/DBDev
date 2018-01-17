USE DBMaint
GO

-- Drop stored procedure if it already exists
IF OBJECT_ID('dbo.MyProc','P') IS NOT NULL
   DROP PROCEDURE dbo.MyProc
GO

CREATE PROCEDURE dbo.MyProc
	@Name varchar(50) = 0, 
	@Length int = 0
AS 
BEGIN 
	SELECT @Name, @Length
END
GO;

-- Finished
