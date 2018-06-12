CREATE PROCEDURE [dbo].[GetMetaClassNames]
	@EntryIds udttIdTable READONLY,
	@NodeIds udttIdTable READONLY
AS
BEGIN
	SELECT E.ID, MC.Name as MetaClassName
	FROM @EntryIds E
	INNER JOIN CatalogEntry CE ON E.ID = CE.CatalogEntryId
	INNER JOIN MetaClass MC On CE.MetaClassId = MC.MetaClassId

	SELECT N.ID, MC.Name as MetaClassName
	FROM @NodeIds N
	INNER JOIN CatalogNode CN ON N.ID = CN.CatalogNodeId
	INNER JOIN MetaClass MC On CN.MetaClassId = MC.MetaClassId
END
