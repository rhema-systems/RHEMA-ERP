import { describe, expect, it } from 'vitest';
import type { SegmentStructure } from '@/types/finance';
import { composeAccountIdentityPreview } from './account-identity-preview';

const segment = (id: string, position: number, natural: boolean, separator?: string): SegmentStructure => ({
    id, segmentName: id, segmentCode: id, segmentPosition: position, segmentLength: natural ? 4 : 3,
    dataType: natural ? 'Numeric' : 'Alphanumeric', separatorCharacter: separator,
    lookupTableRequired: !natural, isRequired: true, isReportingDimension: false,
    isNaturalAccount: natural, isActive: true, lifecycleStatus: 'Active', rowVersion: 'fixture',
    accountUsageCount: 0, totalAccountCount: 0, canActivate: false, canFreeze: true, isSystemDefined: true,
    createdAt: '2026-01-01', updatedAt: '2026-01-01',
});

describe('account identity preview', () => {
    it('uses stable position order, per-segment separators and Natural Account as AccountCode', () => {
        const natural = segment('NATURAL_ACCOUNT', 2, true);
        const company = segment('COMPANY', 1, false, '.');
        expect(composeAccountIdentityPreview([natural, company], {
            COMPANY: 'tdc', NATURAL_ACCOUNT: '6100',
        }, '-')).toEqual({ accountNumber: 'TDC.6100', naturalAccountCode: '6100' });
    });

    it('shows fixed-length placeholders without inventing a natural account code', () => {
        expect(composeAccountIdentityPreview([
            segment('COMPANY', 1, false), segment('NATURAL_ACCOUNT', 2, true),
        ], {}, '-')).toEqual({ accountNumber: '000-0000', naturalAccountCode: '' });
    });
});
