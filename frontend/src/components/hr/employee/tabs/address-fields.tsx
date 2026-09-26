'use client';

/**
 * The two shared controls round-2 lane D2 added to the employee sub-resource dialogs: the
 * administrative-address cascade, and the relationship picker.
 *
 * Both are react-hook-form adapters. `AddressFields` and the catalogue services are controlled
 * components that take values and callbacks; the sub-resource tabs describe a whole dialog through
 * `renderFields={(form) => …}`. Without an adapter each of the five tabs would wire `setValue`,
 * `watch` and the query itself — five copies of the same twenty lines, which is how the four
 * free-text relationship columns this lane replaces came to disagree in the first place.
 */

import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { FieldValues, Path, PathValue, UseFormReturn } from 'react-hook-form';
import { AddressFields } from '@/components/reference/AddressFields';
import { relationshipTypeService } from '@/services/hr/relationship-type.service';
import type { RelationshipCategory } from '@/types/hr/relationship-type';
import { SelectField, TextField } from './fields';

/**
 * Country + the cascading area pickers, bound to two form fields.
 *
 * ⚠ **City and Region are rendered here, disabled, whenever a scheme is loaded.** The server
 * rewrites both from the chosen area on every save, so an editable box would be a field that
 * silently discards what you type — the defect lane A existed to remove from the employee form.
 * Where the country has no scheme, they are ordinary text inputs and the only address the record
 * will have.
 */
export function AddressCascadeField<T extends FieldValues>({
  form,
  countryName,
  geoAreaName,
  cityName,
  regionName,
  /** Rendered under the cascade — street lines, digital address, anything the tree does not hold. */
  children,
}: {
  form: UseFormReturn<T>;
  countryName: Path<T>;
  geoAreaName: Path<T>;
  cityName: Path<T>;
  /** Omit on a record with no region column. */
  regionName?: Path<T>;
  children?: React.ReactNode;
}) {
  const countryId = (form.watch(countryName) as unknown as string) || '';
  const geoAreaId = (form.watch(geoAreaName) as unknown as string) || '';

  const set = (name: Path<T>, value: string) =>
    form.setValue(name, value as PathValue<T, Path<T>>, { shouldValidate: true, shouldDirty: true });

  return (
    <AddressFields
      countryId={countryId}
      onCountryChange={(value) => set(countryName, value)}
      geoAreaId={geoAreaId}
      onGeoAreaChange={(value) => set(geoAreaName, value)}
      fallback={(schemeLoaded) => (
        <div className="mt-4 space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <TextField
              form={form}
              name={cityName}
              label="City / Town"
              disabled={schemeLoaded}
              hint={schemeLoaded ? 'Set from the address above' : undefined}
            />
            {regionName && (
              <TextField
                form={form}
                name={regionName}
                label="Region"
                disabled={schemeLoaded}
                hint={schemeLoaded ? 'Set from the address above' : undefined}
              />
            )}
          </div>
          {children}
        </div>
      )}
    />
  );
}

/**
 * The relationship picker: a dropdown of the catalogue values this screen accepts, beside the
 * free-text box that is still the record's stored wording.
 *
 * ⚠ **Both are shown on purpose.** Picking from the catalogue overwrites the typed words on save —
 * the server mirrors the row's name into the column — and the text box is what a tie nobody has
 * catalogued still goes in. Hiding the text when an id is chosen would make the mirroring
 * invisible; hiding the dropdown would make the catalogue pointless.
 */
export function RelationshipField<T extends FieldValues>({
  form,
  typeIdName,
  textName,
  categories,
  label = 'Relationship',
  required,
}: {
  form: UseFormReturn<T>;
  typeIdName: Path<T>;
  textName: Path<T>;
  /** What this screen accepts. Mirrors the server's set — see RELATIONSHIP_SCOPES. */
  categories: readonly RelationshipCategory[];
  label?: string;
  required?: boolean;
}) {
  const { data: types = [], isSuccess } = useQuery({
    queryKey: ['hr', 'relationship-types', 'screen', [...categories].sort().join(',')],
    queryFn: () => relationshipTypeService.getForScreen(categories),
  });

  const selectedId = (form.watch(typeIdName) as unknown as string) || '';
  const selected = types.find((t) => t.id === selectedId);

  /**
   * Drop a selection this screen no longer accepts.
   *
   * ⚠ Needed because one caller's category set is LIVE: the referee tab's set depends on the
   * referee type, so switching a professional referee to Personal leaves "Former manager"
   * selected. Without this the dropdown would show nothing selected while the id was still in the
   * form, and the save would come back 400 from a field that looked empty.
   *
   * ⚠ Guarded on `isSuccess`, not on `types.length`: the list is empty while the query is in
   * flight, and clearing on that would wipe a perfectly good id every time the dialog opened. It
   * is also why a RETIRED value clears itself here — `getForScreen` asks for active rows only.
   */
  useEffect(() => {
    if (!isSuccess || !selectedId || selected) return;
    form.setValue(typeIdName, '' as PathValue<T, Path<T>>, { shouldDirty: true });
    // `form` is stable across renders; listing it would re-run this on every keystroke elsewhere.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isSuccess, selectedId, selected, typeIdName]);

  /**
   * Mirror the chosen row's name into the text field, so the box shows exactly what will be stored.
   *
   * ⚠ Not cosmetic. The server writes the catalogue name over whatever the payload sent, and the
   * text field is REQUIRED on three of the four screens — a user who picked from the dropdown and
   * never typed would otherwise be stopped by a validation message about a box they were not meant
   * to fill in. Mirroring here makes the client show the same value the server is about to store,
   * which is the whole reason the free text is kept beside the id rather than hidden.
   */
  useEffect(() => {
    if (!selected) return;
    const current = form.getValues(textName) as unknown as string;
    if (current === selected.name) return;
    form.setValue(textName, selected.name as PathValue<T, Path<T>>, {
      shouldValidate: true,
      shouldDirty: true,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected, textName]);

  return (
    <div className="space-y-4">
      <SelectField
        form={form}
        name={typeIdName}
        label={label}
        options={types.map((t) => ({ value: t.id, label: t.name }))}
        allowEmpty
        emptyLabel="Not from the list"
        placeholder="Choose a relationship"
      />
      <TextField
        form={form}
        name={textName}
        label={selected ? `${label} (as recorded)` : `${label} — in your own words`}
        required={required && !selected}
        disabled={!!selected}
        hint={
          selected
            ? 'Set from the list above when this is saved.'
            : 'Used when the tie is not in the list. An administrator can add values under Administration → HR → Relationship Types.'
        }
      />
    </div>
  );
}
