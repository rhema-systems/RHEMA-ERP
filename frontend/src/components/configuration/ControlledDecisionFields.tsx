'use client';

import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import type {
  ControlledFieldSchema,
  LookupOption,
} from '@/types/quantity-survey-configuration';

type Props = {
  fields: ControlledFieldSchema[];
  value: Record<string, unknown>;
  lookups: Record<string, LookupOption[]>;
  disabled?: boolean;
  onChange: (value: Record<string, unknown>) => void;
};

const normalized = (value: unknown) => String(value ?? '').toLowerCase();

export function ControlledDecisionFields({
  fields,
  value,
  lookups,
  disabled,
  onChange,
}: Props) {
  const setField = (name: string, next: unknown) =>
    onChange({ ...value, [name]: next });
  const toggle = (
    field: ControlledFieldSchema,
    option: string,
    checked: boolean
  ) => {
    const selected = Array.isArray(value[field.name])
      ? (value[field.name] as unknown[]).map(String)
      : [];
    const without = selected.filter(
      (item) => normalized(item) !== normalized(option)
    );
    setField(field.name, checked ? [...without, option] : without);
  };

  return (
    <div className="grid gap-4 md:grid-cols-2">
      {fields.map((field) => {
        const current = value[field.name];
        const options: LookupOption[] = field.lookupSource
          ? (lookups[field.lookupSource] ?? [])
              .filter(
                (item) => !field.lookupGroup || item.group === field.lookupGroup
              )
              .map((item) => ({
                value: item.value,
                label: item.label,
                group: item.group,
              }))
          : field.options.map((item) => ({ value: item, label: item }));
        const isMulti =
          field.control === 'multiselect' || field.control === 'multilookup';
        return (
          <div
            key={field.name}
            className={isMulti ? 'space-y-2 md:col-span-2' : 'space-y-2'}
          >
            <Label htmlFor={`controlled-${field.name}`}>
              {field.label}
              {field.required ? ' *' : ''}
            </Label>
            {field.control === 'boolean' ? (
              <div className="flex h-10 items-center gap-3 rounded-md border px-3">
                <Switch
                  id={`controlled-${field.name}`}
                  checked={Boolean(current)}
                  disabled={disabled}
                  onCheckedChange={(next) => setField(field.name, next)}
                />
                <span className="text-sm text-muted-foreground">
                  {current ? 'Enabled' : 'Disabled'}
                </span>
              </div>
            ) : field.control === 'number' ? (
              <Input
                id={`controlled-${field.name}`}
                type="number"
                min={field.minimum}
                max={field.maximum}
                disabled={disabled}
                value={
                  current === undefined || current === null
                    ? ''
                    : String(current)
                }
                onChange={(event) =>
                  setField(
                    field.name,
                    event.target.value === ''
                      ? undefined
                      : Number(event.target.value)
                  )
                }
              />
            ) : field.control === 'date' ? (
              <Input
                id={`controlled-${field.name}`}
                type="date"
                disabled={disabled}
                value={typeof current === 'string' ? current.slice(0, 10) : ''}
                onChange={(event) =>
                  setField(field.name, event.target.value || undefined)
                }
              />
            ) : isMulti ? (
              <div className="grid max-h-64 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2 lg:grid-cols-3">
                {options.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No permitted options are currently configured.
                  </p>
                ) : (
                  options.map((option) => {
                    const checked =
                      Array.isArray(current) &&
                      current.some(
                        (item) => normalized(item) === normalized(option.value)
                      );
                    return (
                      <Label
                        key={option.value}
                        className="flex cursor-pointer items-start gap-2 rounded border p-2 font-normal hover:bg-muted/50"
                      >
                        <Checkbox
                          checked={checked}
                          disabled={disabled}
                          onCheckedChange={(next) =>
                            toggle(field, option.value, next === true)
                          }
                        />
                        <span className="min-w-0 text-sm">
                          <span className="block truncate">{option.label}</span>
                          {option.group && (
                            <span className="block truncate text-xs text-muted-foreground">
                              {option.group}
                            </span>
                          )}
                        </span>
                      </Label>
                    );
                  })
                )}
              </div>
            ) : (
              <Select
                disabled={disabled || options.length === 0}
                value={
                  current === undefined || current === null || current === ''
                    ? '__none__'
                    : String(current)
                }
                onValueChange={(next) =>
                  setField(field.name, next === '__none__' ? undefined : next)
                }
              >
                <SelectTrigger id={`controlled-${field.name}`}>
                  <SelectValue placeholder="Select a controlled value" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Not selected</SelectItem>
                  {options.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
            {field.helpText && (
              <p className="text-xs text-muted-foreground">{field.helpText}</p>
            )}
          </div>
        );
      })}
    </div>
  );
}
