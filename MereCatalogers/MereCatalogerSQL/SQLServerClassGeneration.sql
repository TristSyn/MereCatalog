
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER FUNCTION _TableAsClass
(
	@tableName nvarchar(256),
	@baseTable nvarchar(256)
)
RETURNS nvarchar(max)
AS
BEGIN
	DECLARE @result nvarchar(max) = 'public class '+ replace(@tableName, ' ','')
		+ CASE WHEN NULLIF(@baseTable, '') IS NOT NULL AND @baseTable <> @tableName THEN ' : ' + @baseTable ELSE '' END 
		+  '{' + char(10) + char(13) 

	DECLARE @colOrdinal int

	DECLARE col_cursor CURSOR  
    FOR SELECT ORDINAL_POSITION FROM INFORMATION_SCHEMA.COLUMNS
	WHERE TABLE_NAME = @tableName  

	OPEN col_cursor  

	FETCH NEXT FROM col_cursor INTO @colOrdinal;  

	WHILE @@FETCH_STATUS = 0  
	BEGIN  
		SELECT @result = @result + '	public '
			--https://docs.microsoft.com/en-us/sql/relational-databases/clr-integration-database-objects-types-net-framework/mapping-clr-parameter-data?view=sql-server-2017
			+ CASE cols.Data_Type
				WHEN 'bigint' THEN 'long'
				WHEN 'int' THEN 'int'
				WHEN 'smallint' THEN 'short'
				WHEN 'datetime' THEN 'DateTime'
				WHEN 'bit' THEN 'bool'
				WHEN 'varchar' THEN 'string'
				WHEN 'nvarchar' THEN 'string'
				WHEN 'char' THEN 'string'
				WHEN 'nchar' THEN 'string'
				WHEN 'money' THEN 'decimal'
				WHEN 'real' THEN 'single'
				WHEN 'uniqueidentifier' THEN 'Guid'
				ELSE cols.Data_Type END
			+ ' ' + cols.COLUMN_NAME + ' { get; set; }' + char(10)
		FROM INFORMATION_SCHEMA.COLUMNS cols
			left join INFORMATION_SCHEMA.COLUMNS baseCols on NULLIF(@baseTable, '') IS NOT NULL AND @baseTable <> @tableName AND baseCols.TABLE_NAME = @baseTable and basecols.COLUMN_NAME = cols.COLUMN_NAME
		WHERE cols.TABLE_NAME = @tableName AND cols.ORDINAL_POSITION = @colOrdinal AND baseCols.ORDINAL_POSITION IS NULL
		
		SELECT @result = @result 
			+ '	[DBProperty(KeyID="' + COL_NAME(fc.parent_object_id,fc.parent_column_id) + '")]' + char(10)
			+ '	public ' 
					+ OBJECT_NAME(f.referenced_object_id) 
					+ ' ' 
					+ CASE WHEN RIGHT(cols.COLUMN_NAME, 2) = 'ID' THEN LEFT(cols.COLUMN_NAME, LEN(cols.COLUMN_NAME)-2) ELSE cols.COLUMN_NAME + '_OBJ' END
					+ ' { get; set; }' + char(10) 	   
		FROM 
		   sys.foreign_keys AS f
			INNER JOIN sys.foreign_key_columns AS fc ON f.OBJECT_ID = fc.constraint_object_id
			--INNER JOIN sys.tables t ON t.OBJECT_ID = fc.referenced_object_id
			JOIN INFORMATION_SCHEMA.COLUMNS cols on cols.TABLE_NAME= @tableName AND cols.COLUMN_NAME = COL_NAME(fc.parent_object_id,fc.parent_column_id)
			left join INFORMATION_SCHEMA.COLUMNS baseCols on NULLIF(@baseTable, '') IS NOT NULL AND @baseTable <> @tableName AND baseCols.TABLE_NAME = @baseTable and basecols.COLUMN_NAME = cols.COLUMN_NAME
		WHERE OBJECT_NAME (f.parent_object_id) = @tableName AND cols.ORDINAL_POSITION = @colOrdinal AND baseCols.ORDINAL_POSITION IS NULL
		


		SELECT @result = @result + char(10)

		FETCH NEXT FROM col_cursor INTO @colOrdinal;  
	END
	
	/*
	SELECT @result = @result 
		+ '	//public '+replace(t.name, ' ', '')+'[] '+replace(t.name, ' ', '')+' { get; set; }' + char(10) 	   
	FROM 
		sys.foreign_keys AS f
		INNER JOIN sys.foreign_key_columns AS fc ON f.OBJECT_ID = fc.constraint_object_id
		INNER JOIN sys.tables t ON t.OBJECT_ID = fc.parent_object_id
		JOIN INFORMATION_SCHEMA.COLUMNS cols on cols.TABLE_NAME= @tableName AND cols.COLUMN_NAME = COL_NAME(fc.parent_object_id,fc.parent_column_id)
		left join INFORMATION_SCHEMA.COLUMNS baseCols on NULLIF(@baseTable, '') IS NOT NULL AND @baseTable <> @tableName AND baseCols.TABLE_NAME = @baseTable and basecols.COLUMN_NAME = cols.COLUMN_NAME
	WHERE OBJECT_NAME (f.referenced_object_id) = @tableName
	*/

	SET @result = @result + '}' + char(10)
	RETURN @result

END
GO


select dbo._TableAsClass(tbl.TABLE_NAME, '')
from INFORMATION_SCHEMA.TABLES tbl
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY tbl.TABLE_NAME
