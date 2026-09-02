import type {
  FinanceDimensionDefinition,
  FinanceDimensionFilterDto,
} from '@/types/finance';

export type ReportDimensionSelections = Record<string, string>;
export type ReportDimensionQueryParameters = Record<string, string>;

export function buildFinanceDimensionFilters(
  definitions: FinanceDimensionDefinition[],
  selections: ReportDimensionSelections
): FinanceDimensionFilterDto[] {
  return [...definitions]
    .sort(
      (left, right) =>
        left.displayOrder - right.displayOrder ||
        left.code.localeCompare(right.code)
    )
    .flatMap((definition) => {
      const valueCode = selections[definition.id]?.trim();
      return valueCode
        ? [
            {
              financeDimensionDefinitionId: definition.id,
              dimensionCode: definition.code,
              valueCodes: [valueCode],
            },
          ]
        : [];
    });
}

export function appendFinanceDimensionFilters(
  queryParams: URLSearchParams,
  filters: FinanceDimensionFilterDto[] | undefined
): void {
  (filters ?? []).forEach((filter, index) => {
    const values = filter.valueCodes
      .map((value) => value.trim())
      .filter(Boolean);
    if (values.length === 0) return;
    const prefix = `dimensionFilters[${index}]`;
    if (filter.financeDimensionDefinitionId) {
      queryParams.append(
        `${prefix}.financeDimensionDefinitionId`,
        filter.financeDimensionDefinitionId
      );
    }
    if (filter.dimensionCode) {
      queryParams.append(`${prefix}.dimensionCode`, filter.dimensionCode);
    }
    values.forEach((value) =>
      queryParams.append(`${prefix}.valueCodes`, value)
    );
  });
}

export function toFinanceDimensionFilterQueryParameters(
  filters: FinanceDimensionFilterDto[] | undefined
): ReportDimensionQueryParameters {
  const parameters: ReportDimensionQueryParameters = {};
  (filters ?? [])
    .filter((filter) =>
      filter.valueCodes.some((value) => value.trim().length > 0)
    )
    .forEach((filter, index) => {
      const prefix = `dimensionFilters[${index}]`;
      if (filter.financeDimensionDefinitionId) {
        parameters[`${prefix}.financeDimensionDefinitionId`] =
          filter.financeDimensionDefinitionId;
      }
      if (filter.dimensionCode) {
        parameters[`${prefix}.dimensionCode`] = filter.dimensionCode;
      }
      parameters[`${prefix}.valueCodes`] = filter.valueCodes
        .map((value) => value.trim())
        .filter(Boolean)
        .join(',');
    });
  return parameters;
}

export function areFinanceDimensionFiltersEqual(
  left: FinanceDimensionFilterDto[] | undefined,
  right: FinanceDimensionFilterDto[] | undefined
): boolean {
  const normalize = (filters: FinanceDimensionFilterDto[] | undefined) =>
    (filters ?? [])
      .map((filter) => ({
        definitionId: filter.financeDimensionDefinitionId ?? '',
        code: filter.dimensionCode ?? '',
        values: filter.valueCodes
          .map((value) => value.trim())
          .filter(Boolean)
          .sort(),
      }))
      .sort(
        (a, b) =>
          a.definitionId.localeCompare(b.definitionId) ||
          a.code.localeCompare(b.code)
      );
  return JSON.stringify(normalize(left)) === JSON.stringify(normalize(right));
}

export function readFinanceDimensionSelections(
  searchParams: Pick<URLSearchParams, 'forEach'>,
  definitions: FinanceDimensionDefinition[]
): ReportDimensionSelections {
  const rawByIndex = new Map<
    number,
    { definitionId?: string; code?: string; valueCodes?: string }
  >();
  searchParams.forEach((value, key) => {
    const match =
      /^dimensionFilters\[(\d+)]\.(financeDimensionDefinitionId|dimensionCode|valueCodes)$/i.exec(
        key
      );
    if (!match) return;
    const index = Number(match[1]);
    const raw = rawByIndex.get(index) ?? {};
    const property = match[2].toLowerCase();
    if (property === 'financedimensiondefinitionid') raw.definitionId = value;
    if (property === 'dimensioncode') raw.code = value;
    // The current report UI selects one value per dimension. Preserve the
    // first value when a drill-down URL carries the backend's repeatable
    // valueCodes contract instead of silently selecting the last value.
    if (property === 'valuecodes' && raw.valueCodes === undefined)
      raw.valueCodes = value;
    rawByIndex.set(index, raw);
  });

  const selections: ReportDimensionSelections = {};
  rawByIndex.forEach((raw) => {
    const definition = definitions.find(
      (candidate) =>
        candidate.id === raw.definitionId ||
        candidate.code.toLowerCase() === raw.code?.toLowerCase()
    );
    const valueCode = raw.valueCodes
      ?.split(',')
      .map((value) => value.trim())
      .find(Boolean);
    if (definition && valueCode) selections[definition.id] = valueCode;
  });
  return selections;
}
