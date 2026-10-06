import type { SegmentStructure, SegmentLookupValue } from '@/types/finance';

/**
 * Segment Structure Definitions for Segmented COA
 * Format: DEPT-CC-NAT
 */

// ===== SEGMENT 1: DEPARTMENT =====
export const SEGMENT_DEPARTMENT: SegmentStructure = {
    id: 'seg-1',
    segmentName: 'Department',
    segmentCode: 'DEPT',
    segmentPosition: 1,
    segmentLength: 3,
    dataType: 'Numeric',
    separatorCharacter: '-',
    lookupTableRequired: true,
    isRequired: true,
    isReportingDimension: true,
    isNaturalAccount: false,
    isActive: true,
    lifecycleStatus: 'Active',
    rowVersion: 'fixture',
    accountUsageCount: 0,
    totalAccountCount: 0,
    canActivate: false,
    canFreeze: true,
    isSystemDefined: false,
    description: 'Organizational department for cost tracking and reporting',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
};

// Department Lookup Values with Hierarchies (Dept → Division)
export const DEPARTMENT_LOOKUP_VALUES: SegmentLookupValue[] = [
    // Division 10: Corporate
    {
        id: 'dept-div-10',
        segmentStructureId: 'seg-1',
        segmentValue: '010',
        description: 'Corporate Division',
        parentLookupValueId: undefined, // Top level
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'dept-100',
        segmentStructureId: 'seg-1',
        segmentValue: '100',
        description: 'Finance Department',
        parentLookupValueId: 'dept-div-10', // Rolls up to Corporate Division
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 2,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'dept-110',
        segmentStructureId: 'seg-1',
        segmentValue: '110',
        description: 'Accounting Department',
        parentLookupValueId: 'dept-div-10',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 3,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Division 20: Operations
    {
        id: 'dept-div-20',
        segmentStructureId: 'seg-1',
        segmentValue: '020',
        description: 'Operations Division',
        parentLookupValueId: undefined,
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 4,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'dept-200',
        segmentStructureId: 'seg-1',
        segmentValue: '200',
        description: 'Sales Department',
        parentLookupValueId: 'dept-div-20',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 5,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'dept-210',
        segmentStructureId: 'seg-1',
        segmentValue: '210',
        description: 'Marketing Department',
        parentLookupValueId: 'dept-div-20',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 6,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Division 30: Technology
    {
        id: 'dept-div-30',
        segmentStructureId: 'seg-1',
        segmentValue: '030',
        description: 'Technology Division',
        parentLookupValueId: undefined,
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 7,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'dept-300',
        segmentStructureId: 'seg-1',
        segmentValue: '300',
        description: 'IT Department',
        parentLookupValueId: 'dept-div-30',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 8,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'dept-310',
        segmentStructureId: 'seg-1',
        segmentValue: '310',
        description: 'Development Department',
        parentLookupValueId: 'dept-div-30',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 9,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

// ===== SEGMENT 2: COST CENTER =====
export const SEGMENT_COST_CENTER: SegmentStructure = {
    id: 'seg-2',
    segmentName: 'Cost Center',
    segmentCode: 'CC',
    segmentPosition: 2,
    segmentLength: 3,
    dataType: 'Numeric',
    separatorCharacter: '-',
    lookupTableRequired: true,
    isRequired: true,
    isReportingDimension: true,
    isNaturalAccount: false,
    isActive: true,
    lifecycleStatus: 'Active',
    rowVersion: 'fixture',
    accountUsageCount: 0,
    totalAccountCount: 0,
    canActivate: false,
    canFreeze: true,
    isSystemDefined: false,
    description: 'Physical location or cost center for expense allocation',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
};

// Cost Center Lookup Values with Hierarchies (Cost Center → Region)
export const COST_CENTER_LOOKUP_VALUES: SegmentLookupValue[] = [
    // Region 01: Accra Region
    {
        id: 'cc-region-01',
        segmentStructureId: 'seg-2',
        segmentValue: '001',
        description: 'Accra Region',
        parentLookupValueId: undefined,
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'cc-200',
        segmentStructureId: 'seg-2',
        segmentValue: '200',
        description: 'Head Office',
        parentLookupValueId: 'cc-region-01',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 2,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'cc-210',
        segmentStructureId: 'seg-2',
        segmentValue: '210',
        description: 'Accra Warehouse',
        parentLookupValueId: 'cc-region-01',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 3,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Region 02: Kumasi Region
    {
        id: 'cc-region-02',
        segmentStructureId: 'seg-2',
        segmentValue: '002',
        description: 'Kumasi Region',
        parentLookupValueId: undefined,
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 4,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'cc-300',
        segmentStructureId: 'seg-2',
        segmentValue: '300',
        description: 'Branch 1 - Kumasi',
        parentLookupValueId: 'cc-region-02',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 5,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Region 03: Takoradi Region
    {
        id: 'cc-region-03',
        segmentStructureId: 'seg-2',
        segmentValue: '003',
        description: 'Takoradi Region',
        parentLookupValueId: undefined,
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 6,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'cc-400',
        segmentStructureId: 'seg-2',
        segmentValue: '400',
        description: 'Branch 2 - Takoradi',
        parentLookupValueId: 'cc-region-03',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 7,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'cc-410',
        segmentStructureId: 'seg-2',
        segmentValue: '410',
        description: 'Takoradi Warehouse',
        parentLookupValueId: 'cc-region-03',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 8,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

// ===== SEGMENT 3: NATURAL ACCOUNT =====
export const SEGMENT_NATURAL_ACCOUNT: SegmentStructure = {
    id: 'seg-3',
    segmentName: 'Natural Account',
    segmentCode: 'NAT',
    segmentPosition: 3,
    segmentLength: 4,
    dataType: 'Numeric',
    separatorCharacter: undefined, // No separator after last segment
    lookupTableRequired: false, // Text entry allowed
    isRequired: true,
    isReportingDimension: false, // Not a reporting dimension
    isNaturalAccount: true,
    isActive: true,
    lifecycleStatus: 'Active',
    rowVersion: 'fixture',
    accountUsageCount: 0,
    totalAccountCount: 0,
    canActivate: false,
    canFreeze: true,
    isSystemDefined: false,
    description: 'Natural account classification (Assets, Liabilities, Revenue, Expenses)',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
};

// Natural Account Lookup Values (Optional - for guidance)
export const NATURAL_ACCOUNT_LOOKUP_VALUES: SegmentLookupValue[] = [
    // Assets (1000-1999)
    {
        id: 'nat-1000',
        segmentStructureId: 'seg-3',
        segmentValue: '1000',
        description: 'Cash',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'nat-1100',
        segmentStructureId: 'seg-3',
        segmentValue: '1100',
        description: 'Accounts Receivable',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 2,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'nat-1200',
        segmentStructureId: 'seg-3',
        segmentValue: '1200',
        description: 'Inventory',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 3,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'nat-1500',
        segmentStructureId: 'seg-3',
        segmentValue: '1500',
        description: 'Equipment',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 4,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Liabilities (2000-2999)
    {
        id: 'nat-2000',
        segmentStructureId: 'seg-3',
        segmentValue: '2000',
        description: 'Accounts Payable',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 5,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Equity (3000-3999)
    {
        id: 'nat-3000',
        segmentStructureId: 'seg-3',
        segmentValue: '3000',
        description: 'Share Capital',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 6,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Revenue (4000-4999)
    {
        id: 'nat-4000',
        segmentStructureId: 'seg-3',
        segmentValue: '4000',
        description: 'Sales Revenue',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 7,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },

    // Expenses (6000-6999)
    {
        id: 'nat-6000',
        segmentStructureId: 'seg-3',
        segmentValue: '6000',
        description: 'Salaries',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 8,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
    {
        id: 'nat-6100',
        segmentStructureId: 'seg-3',
        segmentValue: '6100',
        description: 'Rent',
        effectiveDate: '2024-01-01T00:00:00Z',
        isActive: true,
        displayOrder: 9,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
    },
];

// ===== ALL SEGMENT STRUCTURES =====
export const ALL_SEGMENT_STRUCTURES: SegmentStructure[] = [
    {
        ...SEGMENT_DEPARTMENT,
        lookupValues: DEPARTMENT_LOOKUP_VALUES,
    },
    {
        ...SEGMENT_COST_CENTER,
        lookupValues: COST_CENTER_LOOKUP_VALUES,
    },
    {
        ...SEGMENT_NATURAL_ACCOUNT,
        lookupValues: NATURAL_ACCOUNT_LOOKUP_VALUES,
    },
];
