'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type {
  AccountClassification,
  FinancialStatementRowInputDto,
  FinancialStatementRowType,
} from '@/types/finance';

const rowTypes: FinancialStatementRowType[] = [
  'Header',
  'Account',
  'Formula',
  'Total',
  'Spacer',
];

/** Edits a complete version locally; the existing revision-checked API validates the atomic save. */
export function StatementLayoutDraftEditor({
  initialRows,
  classifications,
  busy,
  onSave,
  onCancel,
}: {
  initialRows: FinancialStatementRowInputDto[];
  classifications: AccountClassification[];
  busy: boolean;
  onSave: (rows: FinancialStatementRowInputDto[]) => Promise<boolean>;
  onCancel: () => void;
}) {
  const [rows, setRows] = useState(() =>
    initialRows
      .map((row) => ({
        ...row,
        mappings: row.mappings.map((mapping) => ({ ...mapping })),
      }))
      .sort((a, b) => a.displayOrder - b.displayOrder)
  );
  const update = (
    index: number,
    changes: Partial<FinancialStatementRowInputDto>
  ) =>
    setRows((current) =>
      current.map((row, position) =>
        position === index ? { ...row, ...changes } : row
      )
    );
  const move = (index: number, direction: number) =>
    setRows((current) => {
      const next = [...current];
      [next[index], next[index + direction]] = [
        next[index + direction],
        next[index],
      ];
      return next;
    });
  const invalid =
    rows.length === 0 ||
    rows.some((row) => !row.rowCode.trim() || !row.label.trim()) ||
    new Set(rows.map((row) => row.rowCode.trim().toUpperCase())).size !==
      rows.length;
  return (
    <section
      className="space-y-4 rounded-md border p-4"
      aria-label="Draft layout designer"
    >
      <p className="text-sm text-muted-foreground">
        Edit rows, hierarchy, formulas, display and classification mappings
        here. Changes remain local until Save draft design. Published versions
        are not changed. Formulas and mappings are validated by Finance on save.
      </p>
      <fieldset disabled={busy} className="space-y-3">
        {rows.map((row, index) => (
          <div key={index} className="space-y-3 rounded-md border p-3">
            <div className="flex flex-wrap items-center gap-2">
              <strong>Row {index + 1}</strong>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={index === 0}
                onClick={() => move(index, -1)}
                aria-label={`Move row ${index + 1} up`}
              >
                Up
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={index === rows.length - 1}
                onClick={() => move(index, 1)}
                aria-label={`Move row ${index + 1} down`}
              >
                Down
              </Button>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() =>
                  setRows((current) =>
                    current.filter((_, position) => position !== index)
                  )
                }
              >
                Remove row {index + 1}
              </Button>
            </div>
            <div className="grid gap-3 sm:grid-cols-3">
              <label className="text-sm">
                Code
                <Input
                  value={row.rowCode}
                  maxLength={50}
                  aria-label={`Row ${index + 1} code`}
                  onChange={(event) =>
                    update(index, { rowCode: event.target.value.toUpperCase() })
                  }
                />
              </label>
              <label className="text-sm">
                Label
                <Input
                  value={row.label}
                  maxLength={200}
                  aria-label={`Row ${index + 1} label`}
                  onChange={(event) =>
                    update(index, { label: event.target.value })
                  }
                />
              </label>
              <label className="text-sm">
                Type
                <select
                  className="h-10 w-full rounded-md border bg-background px-2"
                  value={row.rowType}
                  aria-label={`Row ${index + 1} type`}
                  onChange={(event) =>
                    update(index, {
                      rowType: event.target.value as FinancialStatementRowType,
                    })
                  }
                >
                  {rowTypes.map((type) => (
                    <option key={type}>{type}</option>
                  ))}
                </select>
              </label>
              <label className="text-sm">
                Parent row code
                <Input
                  value={row.parentRowCode || ''}
                  maxLength={50}
                  onChange={(event) =>
                    update(index, {
                      parentRowCode:
                        event.target.value.toUpperCase() || undefined,
                    })
                  }
                />
              </label>
              <label className="text-sm">
                Formula
                <Input
                  value={row.formula || ''}
                  maxLength={1000}
                  placeholder="Reference row codes"
                  onChange={(event) =>
                    update(index, { formula: event.target.value || undefined })
                  }
                />
              </label>
              <label className="text-sm">
                Indent
                <Input
                  type="number"
                  min={0}
                  max={20}
                  value={row.indentLevel}
                  onChange={(event) =>
                    update(index, { indentLevel: Number(event.target.value) })
                  }
                />
              </label>
              <label className="text-sm">
                Sign
                <select
                  className="h-10 w-full rounded-md border bg-background px-2"
                  value={row.signMultiplier}
                  onChange={(event) =>
                    update(index, {
                      signMultiplier: Number(event.target.value),
                    })
                  }
                >
                  <option value={1}>Normal</option>
                  <option value={-1}>Reverse</option>
                </select>
              </label>
            </div>
            <div className="flex flex-wrap gap-4 text-sm">
              {(
                [
                  'isVisible',
                  'isBold',
                  'isItalic',
                  'isUnderlined',
                  'suppressIfZero',
                  'showAccountDetails',
                ] as const
              ).map((field, fieldIndex) => (
                <label key={field} className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={Boolean(row[field])}
                    onChange={(event) =>
                      update(index, { [field]: event.target.checked })
                    }
                  />
                  {
                    [
                      'Visible',
                      'Bold',
                      'Italic',
                      'Underlined',
                      'Suppress zero',
                      'Show accounts',
                    ][fieldIndex]
                  }
                </label>
              ))}
            </div>
            {row.mappings.map((mapping, mappingIndex) => (
              <div
                key={mappingIndex}
                className="flex items-center gap-2 text-sm"
              >
                <span>
                  {mapping.mappingType}:{' '}
                  {mapping.accountClassificationCode ||
                    mapping.accountNumber ||
                    [mapping.fromAccountNumber, mapping.toAccountNumber]
                      .filter(Boolean)
                      .join(' – ') ||
                    mapping.accountId}
                </span>
                {mapping.mappingType === 'Classification' && (
                  <label>
                    <input
                      type="checkbox"
                      checked={Boolean(
                        mapping.includeClassificationDescendants
                      )}
                      onChange={(event) =>
                        update(index, {
                          mappings: row.mappings.map((item, position) =>
                            position === mappingIndex
                              ? {
                                  ...item,
                                  includeClassificationDescendants:
                                    event.target.checked,
                                }
                              : item
                          ),
                        })
                      }
                    />{' '}
                    Include descendants
                  </label>
                )}
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  onClick={() =>
                    update(index, {
                      mappings: row.mappings.filter(
                        (_, position) => position !== mappingIndex
                      ),
                    })
                  }
                >
                  Remove mapping
                </Button>
              </div>
            ))}
            {row.rowType === 'Account' && (
              <label className="block text-sm">
                Add classification mapping
                <select
                  aria-label={`Row ${index + 1} classification`}
                  className="mt-1 h-10 w-full rounded-md border bg-background px-2"
                  value=""
                  onChange={(event) => {
                    const classification = classifications.find(
                      (item) => item.id === event.target.value
                    );
                    if (
                      classification &&
                      !row.mappings.some(
                        (mapping) =>
                          mapping.accountClassificationId === classification.id
                      )
                    )
                      update(index, {
                        mappings: [
                          ...row.mappings,
                          {
                            mappingType: 'Classification',
                            accountClassificationId: classification.id,
                            accountClassificationCode: classification.code,
                            includeClassificationDescendants: true,
                          },
                        ],
                      });
                  }}
                >
                  <option value="">Select classification</option>
                  {classifications.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.code} — {item.name}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          onClick={() =>
            setRows((current) => [
              ...current,
              {
                rowCode: '',
                label: '',
                rowType: 'Header',
                displayOrder: (current.length + 1) * 10,
                signMultiplier: 1,
                isVisible: true,
                suppressIfZero: false,
                showAccountDetails: false,
                isBold: false,
                isItalic: false,
                isUnderlined: false,
                indentLevel: 0,
                mappings: [],
              },
            ])
          }
        >
          Add row
        </Button>
      </fieldset>
      {invalid && (
        <p className="text-sm text-destructive">
          Each row needs a unique code and label. At least one row is required.
        </p>
      )}
      <div className="sticky bottom-0 flex gap-2 border-t bg-background py-3">
        <Button variant="outline" disabled={busy} onClick={onCancel}>
          Cancel design changes
        </Button>
        <Button
          disabled={busy || invalid}
          onClick={() =>
            void onSave(
              rows.map((row, index) => ({
                ...row,
                rowCode: row.rowCode.trim(),
                label: row.label.trim(),
                displayOrder: (index + 1) * 10,
              }))
            )
          }
        >
          Save draft design
        </Button>
      </div>
    </section>
  );
}
