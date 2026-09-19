import type { AccountClassification } from '@/types/finance';

export interface ClassificationTreeRow {
    classification: AccountClassification;
    depth: number;
}

export function flattenClassificationHierarchy(items: AccountClassification[]): ClassificationTreeRow[] {
    const children = new Map<string | null, AccountClassification[]>();
    for (const item of items) {
        const key = item.parentClassificationId ?? null;
        children.set(key, [...(children.get(key) ?? []), item]);
    }
    const compare = (a: AccountClassification, b: AccountClassification) =>
        a.displayOrder - b.displayOrder || a.code.localeCompare(b.code);
    const result: ClassificationTreeRow[] = [];
    const visited = new Set<string>();
    const visit = (item: AccountClassification, depth: number) => {
        if (visited.has(item.id)) return;
        visited.add(item.id);
        result.push({ classification: item, depth });
        for (const child of (children.get(item.id) ?? []).sort(compare)) visit(child, depth + 1);
    };
    for (const root of (children.get(null) ?? []).sort(compare)) visit(root, 0);
    // Surface malformed/orphaned data instead of silently hiding it.
    for (const item of [...items].sort(compare)) if (!visited.has(item.id)) visit(item, 0);
    return result;
}

export function filterClassificationHierarchy(
    rows: ClassificationTreeRow[],
    search: string,
    status: string,
    accountType: string,
) {
    const term = search.trim().toLowerCase();
    return rows.filter(({ classification }) =>
        (!term || `${classification.code} ${classification.name} ${classification.description ?? ''}`.toLowerCase().includes(term))
        && (status === 'all' || classification.status === status)
        && (accountType === 'all' || classification.coreAccountType === accountType));
}
