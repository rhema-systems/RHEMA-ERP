/**
 * Unit Accounts Data Service
 * Unified service layer that checks demo mode and returns either mock data or makes API calls
 * 
 * DEMO MODE PERSISTENCE:
 * - Unit Types: Persisted to localStorage
 * - Unit Accounts: Persisted to localStorage with hierarchy support
 * - Unit Journal Entries: Persisted to localStorage with workflow status
 * - Ratio Definitions: Persisted to localStorage
 * - Unit Budgets: Persisted to localStorage
 * - Allocation Rules: Persisted to localStorage
 */

import type {
    UnitType,
    UnitAccount,
    UnitJournalEntry,
    RatioDefinition,
    UnitAccountBudget,
    AllocationRule,
    BudgetVariance,
    CreateUnitTypeDto,
    UpdateUnitTypeDto,
    CreateUnitAccountDto,
    UpdateUnitAccountDto,
    CreateUnitJournalEntryDto,
    CreateRatioDefinitionDto,
    CreateBudgetDto,
    UpdateBudgetDto,
    CreateAllocationRuleDto,
    AllocationType,
} from '@/types/unit-accounts';
import { apiService } from '@/services/api.service';

// Delay to simulate API latency in demo mode
const simulateApiDelay = (ms: number = 300) => new Promise(resolve => setTimeout(resolve, ms));

// Check if finance demo mode is enabled
function isFinanceDemoMode(): boolean {
    if (typeof window === 'undefined') return true;
    try {
        const stored = localStorage.getItem('rhema-erp-demo-mode');
        if (stored) {
            const parsed = JSON.parse(stored);
            return parsed.finance ?? true;
        }
    } catch (error) {
        console.error('Failed to read demo mode state:', error);
    }
    return true;
}

// =============================================================================
// DEFAULT MOCK DATA
// =============================================================================

const DEFAULT_UNIT_TYPES: UnitType[] = [
    { id: 'ut-1', code: 'EMP', name: 'Employees', description: 'Full-time equivalent headcount', decimalPlaces: 0, isActive: true, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ut-2', code: 'SQFT', name: 'Square Feet', description: 'Office/warehouse space', decimalPlaces: 0, isActive: true, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ut-3', code: 'HRS', name: 'Hours', description: 'Labor or machine hours', decimalPlaces: 2, isActive: true, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ut-4', code: 'UNITS', name: 'Units', description: 'Generic quantity count', decimalPlaces: 0, isActive: true, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ut-5', code: 'KWH', name: 'Kilowatt Hours', description: 'Energy consumption', decimalPlaces: 2, isActive: true, createdAt: '2024-01-01', createdBy: 'system' },
];

const DEFAULT_UNIT_ACCOUNTS: UnitAccount[] = [
    { id: 'ua-1', accountNumber: 'U-1000', name: 'Total Employees', unitTypeId: 'ut-1', unitTypeCode: 'EMP', isPostingAccount: false, isActive: true, currentBalance: 75, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ua-2', accountNumber: 'U-1100', name: 'Sales Department', unitTypeId: 'ut-1', unitTypeCode: 'EMP', parentAccountId: 'ua-1', isPostingAccount: true, isActive: true, currentBalance: 23, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ua-3', accountNumber: 'U-1200', name: 'Engineering Department', unitTypeId: 'ut-1', unitTypeCode: 'EMP', parentAccountId: 'ua-1', isPostingAccount: true, isActive: true, currentBalance: 52, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ua-4', accountNumber: 'U-2000', name: 'Total Office Space', unitTypeId: 'ut-2', unitTypeCode: 'SQFT', isPostingAccount: false, isActive: true, currentBalance: 15000, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ua-5', accountNumber: 'U-2100', name: 'HQ Office Space', unitTypeId: 'ut-2', unitTypeCode: 'SQFT', parentAccountId: 'ua-4', isPostingAccount: true, isActive: true, currentBalance: 10000, createdAt: '2024-01-01', createdBy: 'system' },
    { id: 'ua-6', accountNumber: 'U-2200', name: 'Warehouse Space', unitTypeId: 'ut-2', unitTypeCode: 'SQFT', parentAccountId: 'ua-4', isPostingAccount: true, isActive: true, currentBalance: 5000, createdAt: '2024-01-01', createdBy: 'system' },
];

const DEFAULT_UNIT_JOURNAL_ENTRIES: UnitJournalEntry[] = [
    {
        id: 'uje-1', entryNumber: 'UJE-2024-001', entryDate: '2024-03-01', description: 'March headcount update',
        fiscalPeriodId: 'fp-2024-03', status: 'Posted', createdAt: '2024-03-01', createdBy: 'admin',
        lines: [
            { id: 'ujel-1', unitAccountId: 'ua-2', unitAccountNumber: 'U-1100', unitAccountName: 'Sales Department', quantity: 3, description: 'New hires' },
            { id: 'ujel-2', unitAccountId: 'ua-3', unitAccountNumber: 'U-1200', unitAccountName: 'Engineering Department', quantity: 5, description: 'Contractor conversions' },
        ],
    },
];

const DEFAULT_RATIO_DEFINITIONS: RatioDefinition[] = [
    {
        id: 'rd-1', code: 'REV-EMP', name: 'Revenue per Employee', description: 'Total revenue divided by headcount',
        ratioType: 'Unit', numeratorType: 'Financial', denominatorType: 'Unit', numeratorUnitAccountId: 'ua-1',
        resultFormat: 'Currency', formatPrecision: 2, isActive: true, createdAt: '2024-01-01', createdBy: 'system',
    },
    {
        id: 'rd-2', code: 'COST-SQFT', name: 'Cost per Square Foot', description: 'Rent expense by space',
        ratioType: 'Unit', numeratorType: 'Financial', denominatorType: 'Unit', denominatorUnitAccountId: 'ua-4',
        resultFormat: 'Currency', formatPrecision: 2, isActive: true, createdAt: '2024-01-01', createdBy: 'system',
    },
];

const DEFAULT_UNIT_BUDGETS: UnitAccountBudget[] = [
    { id: 'ub-1', unitAccountId: 'ua-2', fiscalYearId: 'fy-2024', fiscalPeriodId: 'fp-2024-03', periodName: 'March 2024', budgetQuantity: 25, budgetVersion: 'Original', isActive: true, createdAt: '2024-01-01', createdBy: 'admin' },
    { id: 'ub-2', unitAccountId: 'ua-3', fiscalYearId: 'fy-2024', fiscalPeriodId: 'fp-2024-03', periodName: 'March 2024', budgetQuantity: 50, budgetVersion: 'Original', isActive: true, createdAt: '2024-01-01', createdBy: 'admin' },
    { id: 'ub-3', unitAccountId: 'ua-5', fiscalYearId: 'fy-2024', fiscalPeriodId: 'fp-2024-03', periodName: 'March 2024', budgetQuantity: 10000, budgetVersion: 'Original', isActive: true, createdAt: '2024-01-01', createdBy: 'admin' },
];

const DEFAULT_ALLOCATION_RULES: AllocationRule[] = [
    {
        id: 'ar-1', code: 'RENT-ALLOC', name: 'Rent Allocation by Square Feet',
        description: 'Allocate rent expense across departments based on square footage',
        sourceAccountId: 'gl-rent', sourceAccountNumber: '6100-000', sourceAccountName: 'Rent Expense',
        allocationType: 'UnitAccountBased', driverUnitAccountId: 'ua-4', driverUnitAccountNumber: 'U-2000', driverUnitAccountName: 'Total Office Space',
        isActive: true, autoReverse: false, lastRunDate: '2024-03-31',
        targets: [
            { id: 'at-1', allocationRuleId: 'ar-1', targetAccountId: 'gl-rent-sales', targetAccountNumber: '6100-100', targetAccountName: 'Rent - Sales' },
            { id: 'at-2', allocationRuleId: 'ar-1', targetAccountId: 'gl-rent-eng', targetAccountNumber: '6100-200', targetAccountName: 'Rent - Engineering' },
        ],
        createdAt: '2024-01-15', createdBy: 'admin',
    },
];

// =============================================================================
// DEMO STORAGE MANAGER
// =============================================================================

class UnitAccountsDemoStorage {
    private readonly STORAGE_KEYS = {
        unitTypes: 'rhema-erp-demo-unit-types',
        unitAccounts: 'rhema-erp-demo-unit-accounts',
        unitJournalEntries: 'rhema-erp-demo-unit-journal-entries',
        ratioDefinitions: 'rhema-erp-demo-ratio-definitions',
        unitBudgets: 'rhema-erp-demo-unit-budgets',
        allocationRules: 'rhema-erp-demo-allocation-rules',
    };

    private get<T>(key: string): T | null {
        if (typeof window === 'undefined') return null;
        try {
            const stored = localStorage.getItem(key);
            return stored ? JSON.parse(stored) : null;
        } catch (error) {
            console.error(`Failed to read ${key}:`, error);
            return null;
        }
    }

    private save<T>(key: string, data: T): void {
        if (typeof window === 'undefined') return;
        try {
            localStorage.setItem(key, JSON.stringify(data));
        } catch (error) {
            console.error(`Failed to save ${key}:`, error);
        }
    }

    // ===== UNIT TYPES =====
    getUnitTypes(): UnitType[] {
        const stored = this.get<UnitType[]>(this.STORAGE_KEYS.unitTypes);
        if (!stored) {
            this.save(this.STORAGE_KEYS.unitTypes, DEFAULT_UNIT_TYPES);
            return DEFAULT_UNIT_TYPES;
        }
        return stored;
    }

    saveUnitTypes(types: UnitType[]): void {
        this.save(this.STORAGE_KEYS.unitTypes, types);
    }

    addUnitType(type: UnitType): UnitType {
        const types = this.getUnitTypes();
        types.push(type);
        this.saveUnitTypes(types);
        return type;
    }

    updateUnitType(id: string, updates: Partial<UnitType>): UnitType | null {
        const types = this.getUnitTypes();
        const index = types.findIndex(t => t.id === id);
        if (index === -1) return null;
        types[index] = { ...types[index], ...updates };
        this.saveUnitTypes(types);
        return types[index];
    }

    deleteUnitType(id: string): boolean {
        const types = this.getUnitTypes();
        const filtered = types.filter(t => t.id !== id);
        if (filtered.length === types.length) return false;
        this.saveUnitTypes(filtered);
        return true;
    }

    // ===== UNIT ACCOUNTS =====
    getUnitAccounts(): UnitAccount[] {
        const stored = this.get<UnitAccount[]>(this.STORAGE_KEYS.unitAccounts);
        if (!stored) {
            this.save(this.STORAGE_KEYS.unitAccounts, DEFAULT_UNIT_ACCOUNTS);
            return DEFAULT_UNIT_ACCOUNTS;
        }
        return stored;
    }

    saveUnitAccounts(accounts: UnitAccount[]): void {
        this.save(this.STORAGE_KEYS.unitAccounts, accounts);
    }

    addUnitAccount(account: UnitAccount): UnitAccount {
        const accounts = this.getUnitAccounts();
        accounts.push(account);
        this.saveUnitAccounts(accounts);
        return account;
    }

    updateUnitAccount(id: string, updates: Partial<UnitAccount>): UnitAccount | null {
        const accounts = this.getUnitAccounts();
        const index = accounts.findIndex(a => a.id === id);
        if (index === -1) return null;
        accounts[index] = { ...accounts[index], ...updates };
        this.saveUnitAccounts(accounts);
        return accounts[index];
    }

    deleteUnitAccount(id: string): boolean {
        const accounts = this.getUnitAccounts();
        const filtered = accounts.filter(a => a.id !== id);
        if (filtered.length === accounts.length) return false;
        this.saveUnitAccounts(filtered);
        return true;
    }

    // ===== UNIT JOURNAL ENTRIES =====
    getUnitJournalEntries(): UnitJournalEntry[] {
        const stored = this.get<UnitJournalEntry[]>(this.STORAGE_KEYS.unitJournalEntries);
        if (!stored) {
            this.save(this.STORAGE_KEYS.unitJournalEntries, DEFAULT_UNIT_JOURNAL_ENTRIES);
            return DEFAULT_UNIT_JOURNAL_ENTRIES;
        }
        return stored;
    }

    saveUnitJournalEntries(entries: UnitJournalEntry[]): void {
        this.save(this.STORAGE_KEYS.unitJournalEntries, entries);
    }

    addUnitJournalEntry(entry: UnitJournalEntry): UnitJournalEntry {
        const entries = this.getUnitJournalEntries();
        entries.unshift(entry);
        this.saveUnitJournalEntries(entries);
        return entry;
    }

    updateUnitJournalEntry(id: string, updates: Partial<UnitJournalEntry>): UnitJournalEntry | null {
        const entries = this.getUnitJournalEntries();
        const index = entries.findIndex(e => e.id === id);
        if (index === -1) return null;
        entries[index] = { ...entries[index], ...updates };
        this.saveUnitJournalEntries(entries);
        return entries[index];
    }

    deleteUnitJournalEntry(id: string): boolean {
        const entries = this.getUnitJournalEntries();
        const filtered = entries.filter(e => e.id !== id);
        if (filtered.length === entries.length) return false;
        this.saveUnitJournalEntries(filtered);
        return true;
    }

    // ===== RATIO DEFINITIONS =====
    getRatioDefinitions(): RatioDefinition[] {
        const stored = this.get<RatioDefinition[]>(this.STORAGE_KEYS.ratioDefinitions);
        if (!stored) {
            this.save(this.STORAGE_KEYS.ratioDefinitions, DEFAULT_RATIO_DEFINITIONS);
            return DEFAULT_RATIO_DEFINITIONS;
        }
        return stored;
    }

    saveRatioDefinitions(definitions: RatioDefinition[]): void {
        this.save(this.STORAGE_KEYS.ratioDefinitions, definitions);
    }

    addRatioDefinition(definition: RatioDefinition): RatioDefinition {
        const definitions = this.getRatioDefinitions();
        definitions.push(definition);
        this.saveRatioDefinitions(definitions);
        return definition;
    }

    updateRatioDefinition(id: string, updates: Partial<RatioDefinition>): RatioDefinition | null {
        const definitions = this.getRatioDefinitions();
        const index = definitions.findIndex(d => d.id === id);
        if (index === -1) return null;
        definitions[index] = { ...definitions[index], ...updates };
        this.saveRatioDefinitions(definitions);
        return definitions[index];
    }

    deleteRatioDefinition(id: string): boolean {
        const definitions = this.getRatioDefinitions();
        const filtered = definitions.filter(d => d.id !== id);
        if (filtered.length === definitions.length) return false;
        this.saveRatioDefinitions(filtered);
        return true;
    }

    // ===== UNIT BUDGETS =====
    getUnitBudgets(): UnitAccountBudget[] {
        const stored = this.get<UnitAccountBudget[]>(this.STORAGE_KEYS.unitBudgets);
        if (!stored) {
            this.save(this.STORAGE_KEYS.unitBudgets, DEFAULT_UNIT_BUDGETS);
            return DEFAULT_UNIT_BUDGETS;
        }
        return stored;
    }

    saveUnitBudgets(budgets: UnitAccountBudget[]): void {
        this.save(this.STORAGE_KEYS.unitBudgets, budgets);
    }

    addUnitBudget(budget: UnitAccountBudget): UnitAccountBudget {
        const budgets = this.getUnitBudgets();
        budgets.push(budget);
        this.saveUnitBudgets(budgets);
        return budget;
    }

    updateUnitBudget(id: string, updates: Partial<UnitAccountBudget>): UnitAccountBudget | null {
        const budgets = this.getUnitBudgets();
        const index = budgets.findIndex(b => b.id === id);
        if (index === -1) return null;
        budgets[index] = { ...budgets[index], ...updates };
        this.saveUnitBudgets(budgets);
        return budgets[index];
    }

    deleteUnitBudget(id: string): boolean {
        const budgets = this.getUnitBudgets();
        const filtered = budgets.filter(b => b.id !== id);
        if (filtered.length === budgets.length) return false;
        this.saveUnitBudgets(filtered);
        return true;
    }

    // ===== ALLOCATION RULES =====
    getAllocationRules(): AllocationRule[] {
        const stored = this.get<AllocationRule[]>(this.STORAGE_KEYS.allocationRules);
        if (!stored) {
            this.save(this.STORAGE_KEYS.allocationRules, DEFAULT_ALLOCATION_RULES);
            return DEFAULT_ALLOCATION_RULES;
        }
        return stored;
    }

    saveAllocationRules(rules: AllocationRule[]): void {
        this.save(this.STORAGE_KEYS.allocationRules, rules);
    }

    addAllocationRule(rule: AllocationRule): AllocationRule {
        const rules = this.getAllocationRules();
        rules.push(rule);
        this.saveAllocationRules(rules);
        return rule;
    }

    updateAllocationRule(id: string, updates: Partial<AllocationRule>): AllocationRule | null {
        const rules = this.getAllocationRules();
        const index = rules.findIndex(r => r.id === id);
        if (index === -1) return null;
        rules[index] = { ...rules[index], ...updates };
        this.saveAllocationRules(rules);
        return rules[index];
    }

    deleteAllocationRule(id: string): boolean {
        const rules = this.getAllocationRules();
        const filtered = rules.filter(r => r.id !== id);
        if (filtered.length === rules.length) return false;
        this.saveAllocationRules(filtered);
        return true;
    }
}

const demoStorage = new UnitAccountsDemoStorage();

// =============================================================================
// UNIT ACCOUNTS DATA SERVICE
// =============================================================================

class UnitAccountsDataService {
    // ===== UNIT TYPES =====
    async getUnitTypes(): Promise<UnitType[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getUnitTypes();
        }
        // API call
        return apiService.get<UnitType[]>('/finance/unit-types');
    }

    async getUnitTypeById(id: string): Promise<UnitType | null> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const types = demoStorage.getUnitTypes();
            return types.find(t => t.id === id) || null;
        }
        // API call
        return apiService.get<UnitType>(`/finance/unit-types/${id}`);
    }

    async createUnitType(dto: CreateUnitTypeDto): Promise<UnitType> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const newType: UnitType = {
                id: `ut-${Date.now()}`,
                ...dto,
                isActive: true,
                createdAt: new Date().toISOString(),
                createdBy: 'demo-user',
            };
            return demoStorage.addUnitType(newType);
        }
        // API call
        return apiService.post<UnitType>('/finance/unit-types', dto);
    }

    async updateUnitType(id: string, dto: UpdateUnitTypeDto): Promise<UnitType> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const updated = demoStorage.updateUnitType(id, dto);
            if (!updated) throw new Error(`Unit type ${id} not found`);
            return updated;
        }
        // API call
        return apiService.put<UnitType>(`/finance/unit-types/${id}`, dto);
    }

    async deleteUnitType(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            if (!demoStorage.deleteUnitType(id)) {
                throw new Error(`Unit type ${id} not found`);
            }
            return;
        }
        // API call
        await apiService.delete(`/finance/unit-types/${id}`);
    }

    // ===== UNIT ACCOUNTS =====
    async getUnitAccounts(): Promise<UnitAccount[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getUnitAccounts();
        }
        // API call
        return apiService.get<UnitAccount[]>('/finance/unit-accounts');
    }

    async getUnitAccountById(id: string): Promise<UnitAccount | null> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const accounts = demoStorage.getUnitAccounts();
            return accounts.find(a => a.id === id) || null;
        }
        // API call
        return apiService.get<UnitAccount>(`/finance/unit-accounts/${id}`);
    }

    async createUnitAccount(dto: CreateUnitAccountDto): Promise<UnitAccount> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const types = demoStorage.getUnitTypes();
            const unitType = types.find(t => t.id === dto.unitTypeId);
            const newAccount: UnitAccount = {
                id: `ua-${Date.now()}`,
                ...dto,
                unitTypeCode: unitType?.code || 'UNITS',
                currentBalance: 0,
                isActive: true,
                createdAt: new Date().toISOString(),
                createdBy: 'demo-user',
            };
            return demoStorage.addUnitAccount(newAccount);
        }
        // API call
        return apiService.post<UnitAccount>('/finance/unit-accounts', dto);
    }

    async updateUnitAccount(id: string, dto: UpdateUnitAccountDto): Promise<UnitAccount> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const updated = demoStorage.updateUnitAccount(id, dto);
            if (!updated) throw new Error(`Unit account ${id} not found`);
            return updated;
        }
        // API call
        return apiService.put<UnitAccount>(`/finance/unit-accounts/${id}`, dto);
    }

    async deleteUnitAccount(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            if (!demoStorage.deleteUnitAccount(id)) {
                throw new Error(`Unit account ${id} not found`);
            }
            return;
        }
        // API call
        await apiService.delete(`/finance/unit-accounts/${id}`);
    }

    // ===== UNIT JOURNAL ENTRIES =====
    async getUnitJournalEntries(): Promise<UnitJournalEntry[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getUnitJournalEntries();
        }
        // API call
        return apiService.get<UnitJournalEntry[]>('/finance/unit-journal-entries');
    }

    async getUnitJournalEntryById(id: string): Promise<UnitJournalEntry | null> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const entries = demoStorage.getUnitJournalEntries();
            return entries.find(e => e.id === id) || null;
        }
        // API call
        return apiService.get<UnitJournalEntry>(`/finance/unit-journal-entries/${id}`);
    }

    async createUnitJournalEntry(dto: CreateUnitJournalEntryDto): Promise<UnitJournalEntry> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(800);
            const count = demoStorage.getUnitJournalEntries().length;
            const newEntry: UnitJournalEntry = {
                id: `uje-${Date.now()}`,
                entryNumber: `UJE-${new Date().getFullYear()}-${String(count + 1).padStart(3, '0')}`,
                ...dto,
                status: 'Draft',
                createdAt: new Date().toISOString(),
                createdBy: 'demo-user',
            };
            return demoStorage.addUnitJournalEntry(newEntry);
        }
        // API call
        return apiService.post<UnitJournalEntry>('/finance/unit-journal-entries', dto);
    }

    async updateUnitJournalEntryStatus(id: string, status: string): Promise<UnitJournalEntry> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const updated = demoStorage.updateUnitJournalEntry(id, { status: status as any });
            if (!updated) throw new Error(`Journal entry ${id} not found`);
            return updated;
        }
        // API call - Use PATCH for status update
        return apiService.patch<UnitJournalEntry>(`/finance/unit-journal-entries/${id}/${status.toLowerCase()}`, {});
    }

    async deleteUnitJournalEntry(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            if (!demoStorage.deleteUnitJournalEntry(id)) {
                throw new Error(`Journal entry ${id} not found`);
            }
            return;
        }
        // API call
        await apiService.delete(`/finance/unit-journal-entries/${id}`);
    }

    // ===== RATIO DEFINITIONS =====
    async getRatioDefinitions(): Promise<RatioDefinition[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getRatioDefinitions();
        }
        // API call
        return apiService.get<RatioDefinition[]>('/finance/ratio-definitions');
    }

    async createRatioDefinition(dto: CreateRatioDefinitionDto): Promise<RatioDefinition> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const newDef: RatioDefinition = {
                id: `rd-${Date.now()}`,
                ...dto,
                isActive: true,
                createdAt: new Date().toISOString(),
                createdBy: 'demo-user',
            };
            return demoStorage.addRatioDefinition(newDef);
        }
        // API call
        return apiService.post<RatioDefinition>('/finance/ratio-definitions', dto);
    }

    async deleteRatioDefinition(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            if (!demoStorage.deleteRatioDefinition(id)) {
                throw new Error(`Ratio definition ${id} not found`);
            }
            return;
        }
        // API call
        await apiService.delete(`/finance/ratio-definitions/${id}`);
    }

    // ===== UNIT BUDGETS =====
    async getUnitBudgets(): Promise<UnitAccountBudget[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getUnitBudgets();
        }
        // API call
        return apiService.get<UnitAccountBudget[]>('/finance/unit-budgets');
    }

    async getBudgetVariances(periodId?: string): Promise<BudgetVariance[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const budgets = demoStorage.getUnitBudgets();
            const accounts = demoStorage.getUnitAccounts();

            return budgets.map(b => {
                const account = accounts.find(a => a.id === b.unitAccountId);
                const actual = account?.currentBalance || 0;
                const variance = actual - b.budgetQuantity;
                return {
                    unitAccountId: b.unitAccountId,
                    accountNumber: account?.accountNumber || '',
                    accountName: account?.name || '',
                    unitTypeCode: account?.unitType?.code || '',
                    periodName: b.periodName || '',
                    budgetQuantity: b.budgetQuantity,
                    actualQuantity: actual,
                    variance,
                    variancePercent: b.budgetQuantity ? (variance / b.budgetQuantity) * 100 : 0,
                    isFavorable: variance <= 0, // Under budget is favorable for costs
                };
            });
        }
        // API call
        const periodQuery = periodId ? `?periodId=${periodId}` : '';
        return apiService.get<BudgetVariance[]>(`/finance/unit-budgets/variances${periodQuery}`);
    }

    async createUnitBudget(dto: CreateBudgetDto): Promise<UnitAccountBudget> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const newBudget: UnitAccountBudget = {
                id: `ub-${Date.now()}`,
                ...dto,
                budgetVersion: dto.budgetVersion || 'Original',
                isActive: true,
                createdAt: new Date().toISOString(),
                createdBy: 'demo-user',
            };
            return demoStorage.addUnitBudget(newBudget);
        }
        // API call
        return apiService.post<UnitAccountBudget>('/finance/unit-budgets', dto);
    }

    async updateUnitBudget(id: string, dto: UpdateBudgetDto): Promise<UnitAccountBudget> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const updated = demoStorage.updateUnitBudget(id, dto);
            if (!updated) throw new Error(`Budget ${id} not found`);
            return updated;
        }
        // API call
        return apiService.put<UnitAccountBudget>(`/finance/unit-budgets/${id}`, dto);
    }

    async deleteUnitBudget(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            if (!demoStorage.deleteUnitBudget(id)) {
                throw new Error(`Budget ${id} not found`);
            }
            return;
        }
        // API call
        await apiService.delete(`/finance/unit-budgets/${id}`);
    }

    // ===== ALLOCATION RULES =====
    async getAllocationRules(): Promise<AllocationRule[]> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            return demoStorage.getAllocationRules();
        }
        // API call
        return apiService.get<AllocationRule[]>('/finance/allocations/rules');
    }

    async getAllocationRuleById(id: string): Promise<AllocationRule | null> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay();
            const rules = demoStorage.getAllocationRules();
            return rules.find(r => r.id === id) || null;
        }
        // API call
        return apiService.get<AllocationRule>(`/finance/allocations/rules/${id}`);
    }

    async createAllocationRule(dto: CreateAllocationRuleDto): Promise<AllocationRule> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const newRule: AllocationRule = {
                id: `ar-${Date.now()}`,
                ...dto,
                isActive: true,
                targets: dto.targets.map((t, i) => ({
                    id: `at-${Date.now()}-${i}`,
                    allocationRuleId: `ar-${Date.now()}`,
                    ...t,
                })),
                createdAt: new Date().toISOString(),
                createdBy: 'demo-user',
            };
            return demoStorage.addAllocationRule(newRule);
        }
        // API call
        return apiService.post<AllocationRule>('/finance/allocations/rules', dto);
    }

    async updateAllocationRule(id: string, updates: Partial<AllocationRule>): Promise<AllocationRule> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(600);
            const updated = demoStorage.updateAllocationRule(id, updates);
            if (!updated) throw new Error(`Allocation rule ${id} not found`);
            return updated;
        }
        // API call
        return apiService.put<AllocationRule>(`/finance/allocations/rules/${id}`, updates);
    }

    async deleteAllocationRule(id: string): Promise<void> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(400);
            if (!demoStorage.deleteAllocationRule(id)) {
                throw new Error(`Allocation rule ${id} not found`);
            }
            return;
        }
        // API call
        await apiService.delete(`/finance/allocations/rules/${id}`);
    }

    async runAllocation(ruleId: string): Promise<{ success: boolean; message: string }> {
        if (isFinanceDemoMode()) {
            await simulateApiDelay(1500);
            demoStorage.updateAllocationRule(ruleId, { lastRunDate: new Date().toISOString() });
            return { success: true, message: 'Allocation completed successfully (demo mode)' };
        }
        // API call
        const dto = { allocationRuleId: ruleId, fiscalPeriodId: '', allocationDate: new Date().toISOString() };
        await apiService.post(`/finance/allocations/rules/${ruleId}/run`, dto);
        return { success: true, message: 'Allocation completed successfully' };
    }
}

export const unitAccountsDataService = new UnitAccountsDataService();
