import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const source = readFileSync(join(process.cwd(), 'src/app/finance/reports/layouts/page.tsx'), 'utf8');

describe('financial statement layout workspace authorization contract', () => {
    it('uses distinct read, run, manage, and publish permissions', () => {
        expect(source).toContain("const VIEW_PERMISSION = 'Finance.Read'");
        expect(source).toContain("const RUN_PERMISSION = 'Finance.Reports.Run'");
        expect(source).toContain("const MANAGE_PERMISSION = 'Finance.Reports.Layouts.Manage'");
        expect(source).toContain("const PUBLISH_PERMISSION = 'Finance.Reports.Layouts.Publish'");
        expect(source).toContain('canRun && !layout.isProtectedStandard');
        expect(source).toContain('canManage && !layout.isProtectedStandard');
        expect(source).toContain("canPublish && !layout.isProtectedStandard && selectedVersion?.status === 'Draft'");
    });

    it('keeps protected standards clone-only and classification editing draft-only', () => {
        expect(source).toContain('Protected standard · clone only');
        expect(source).toContain('layout.isProtectedStandard && canManage');
        expect(source).toContain("selectedVersion.status === 'Draft' && row.rowType === 'Account'");
        expect(source).toContain('Add classification mapping');
    });
});
