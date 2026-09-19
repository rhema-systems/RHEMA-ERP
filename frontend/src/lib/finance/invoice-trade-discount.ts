const roundMoney = (amount: number) => Math.round((amount + Number.EPSILON) * 100) / 100;

export const calculateLineTradeDiscount = (grossAmount: number, percentage: number) =>
  roundMoney((roundMoney(grossAmount) * percentage) / 100);

export const calculateNetTradeDiscountLineAmount = (grossAmount: number, percentage: number) =>
  roundMoney(roundMoney(grossAmount) - calculateLineTradeDiscount(grossAmount, percentage));

export const allocateDocumentTradeDiscount = (
  sourceLines: Array<{ sourceLineId: string; netAmount: number }>,
  documentDiscount: number
) => {
  const eligible = sourceLines
    .map((line, index) => ({
      index,
      sourceLineId: line.sourceLineId,
      amount: roundMoney(Math.max(0, line.netAmount)),
    }))
    .filter((line) => line.amount > 0)
    .sort((left, right) => left.sourceLineId.localeCompare(right.sourceLineId));
  const total = eligible.reduce((sum, line) => sum + line.amount, 0);
  const allocations = sourceLines.map(() => 0);
  const amountToAllocate = Math.min(roundMoney(Math.max(0, documentDiscount)), total);
  let allocated = 0;

  eligible.forEach((line, eligibleIndex) => {
    const amount =
      eligibleIndex === eligible.length - 1
        ? roundMoney(amountToAllocate - allocated)
        : roundMoney((amountToAllocate * line.amount) / total);
    allocations[line.index] = amount;
    allocated += amount;
  });

  return allocations;
};
