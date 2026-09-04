import type { SegmentStructure } from '@/types/finance';

export function composeAccountIdentityPreview(
    segments: SegmentStructure[],
    values: Record<string, string>,
    fallbackSeparator: string,
) {
    const ordered = [...segments].sort((left, right) => left.segmentPosition - right.segmentPosition);
    const parts: string[] = [];
    let naturalAccountCode = '';
    ordered.forEach((segment, index) => {
        const value = (values[segment.id] || '').trim().toUpperCase();
        if (segment.isNaturalAccount) naturalAccountCode = value;
        parts.push(value || '0'.repeat(segment.segmentLength));
        if (index < ordered.length - 1) parts.push(segment.separatorCharacter ?? fallbackSeparator);
    });
    return { accountNumber: parts.join(''), naturalAccountCode };
}
