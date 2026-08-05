import { beforeEach, describe, expect, it, vi } from 'vitest';
import { accountsPayableService } from './accountsPayableService';

describe('AP procurement/Finance reconciliation client', () => {
    beforeEach(() => {
        vi.restoreAllMocks();
        localStorage.clear();
    });

    it('loads the tenant-safe AP-005 report with bounded filters', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
            ruleCode: 'AP-005',
            taskCode: 'TDC-0508',
            decisionKeys: ['DEC-001', 'DEC-014'],
            rows: [],
        }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
        vi.stubGlobal('fetch', fetchMock);

        const result = await accountsPayableService.getProcurementFinanceReconciliation({
            asOfDate: '2026-07-31',
            purchaseOrderId: 'po-0508',
        });

        expect(result.ruleCode).toBe('AP-005');
        expect(fetchMock).toHaveBeenCalledWith(
            expect.stringContaining('/api/ap/reports/procurement-reconciliation'),
            expect.objectContaining({ headers: expect.any(Object) })
        );
        expect(fetchMock.mock.calls[0][0]).toContain('asOfDate=2026-07-31');
        expect(fetchMock.mock.calls[0][0]).toContain('purchaseOrderId=po-0508');
    });

    it('uses the dedicated controlled CSV export endpoint', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response('PurchaseOrder,Status', {
            status: 200,
            headers: { 'Content-Type': 'text/csv' },
        }));
        vi.stubGlobal('fetch', fetchMock);

        const blob = await accountsPayableService.downloadProcurementFinanceReconciliation({
            asOfDate: '2026-07-31',
        });

        expect(blob.size).toBeGreaterThan(0);
        expect(blob.type).toBe('text/csv');
        expect(fetchMock.mock.calls[0][0]).toContain('/api/ap/reports/procurement-reconciliation/export');
        expect(fetchMock.mock.calls[0][0]).toContain('format=Csv');
    });
});
