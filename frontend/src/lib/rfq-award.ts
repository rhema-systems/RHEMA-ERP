import type { RfqItemDto, RfqQuoteDto } from '@/services/rfqService';

export const isAwardableRfqQuote = (quote: Pick<RfqQuoteDto, 'status'>) =>
  quote.status.trim().toLowerCase() === 'submitted';

export const getAwardableRfqQuotes = (quotes: RfqQuoteDto[] | undefined) =>
  (quotes || []).filter(isAwardableRfqQuote);

export const buildSplitAwardLines = (
  items: RfqItemDto[],
  quotes: RfqQuoteDto[],
  quoteIdByItemId: Record<string, string>,
  reasonByItemId: Record<string, string>
) => {
  const awardableQuotes = new Map(
    getAwardableRfqQuotes(quotes).map((quote) => [quote.id, quote])
  );

  return items.map((item) => {
    const quoteId = quoteIdByItemId[item.id];
    const quote = quoteId ? awardableQuotes.get(quoteId) : undefined;
    if (!quote) {
      throw new Error(`Select a submitted supplier quote for RFQ line ${item.lineNumber}.`);
    }
    if (!(quote.items || []).some((line) => line.rfqItemId === item.id)) {
      throw new Error(`${quote.partnerName} has no submitted price for RFQ line ${item.lineNumber}.`);
    }

    return {
      rfqItemId: item.id,
      quoteId,
      awardReason: reasonByItemId[item.id]?.trim() || undefined,
    };
  });
};
