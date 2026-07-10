use testt

select * from Rhema_SOPItems

SELECT * FROM Rhema_SOPInvoiceDetails

SELECT 
                        d.[LINENO] AS ID,
                        d.SOPNUMBE, d.ITEMNMBR, d.ITEMDESC, d.QUANTITY, d.UNITPRCE, 
                        d.LINEAMNT,
                        CAST((d.QUANTITY * d.UNITPRCE) * 1 AS NUMERIC(19,5)) AS CONVERTEDLINEAMNT,
                        d.UOFM, d.LOCNCODE, d.HSCODE,
                        ISNULL(i.COO, '') AS COO,
                        ISNULL(i.ITEMDESC_FOREIGN, '') AS ITEMDESC_FOREIGN,
                        d.[LINENO]
                    FROM Rhema_SOPInvoiceDetails d
                    LEFT JOIN [dbo].[Rhema_SOPItems] i ON d.ITEMNMBR = i.ITEMNMBR
                    --WHERE d.SOPNUMBE = @SOPNUMBE
                    ORDER BY d.[LINENO]