import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import {
  FLIGHT_CABIN_CLASS_LABELS,
  TRAVEL_ATTACHMENT_TYPE_LABELS,
  TRAVEL_COMMENT_TYPE_LABELS,
  TRAVEL_INITIATOR_ROLE_LABELS,
  TRAVEL_PRIORITY_LABELS,
  TRAVEL_PURPOSE_LABELS,
  TRAVEL_REQUEST_STATUS_LABELS,
  TRAVEL_RISK_LEVEL_LABELS,
  TRAVEL_TYPE_LABELS,
  enumValues,
} from './travel-enums';

/**
 * Holds the staff-travel TypeScript unions to their C# enums (travel final closure, lane 0).
 *
 * Four unions had drifted from C# — the request form offered two values the API refused, the desk's
 * comment type failed every time, and five of six attachment types failed every upload — and
 * TypeScript was satisfied throughout, because a union written from examples type-checks as well as
 * one written from the enum. Only a comparison with the C# source can see it.
 */

const csharpSource = readFileSync(resolve(process.cwd(), '../src/ErpSystem.Core/Enums/HREnums.cs'), 'utf8');

/** The members of `public enum <name>`, in declaration order, with their [Description] text. */
function csharpEnum(name: string): { member: string; description: string | null }[] | null {
  const body = new RegExp(`public enum ${name}\\s*\\{([\\s\\S]*?)\\n\\}`).exec(csharpSource)?.[1];
  if (body === undefined) return null;
  const members: { member: string; description: string | null }[] = [];
  let pendingDescription: string | null = null;
  for (const raw of body.split('\n')) {
    const line = raw.trim();
    if (!line || line.startsWith('//')) continue;
    const description = /^\[Description\("([^"]*)"\)\]$/.exec(line);
    if (description) {
      pendingDescription = description[1];
      continue;
    }
    const member = /^([A-Za-z_][A-Za-z0-9_]*)/.exec(line);
    if (member) {
      members.push({ member: member[1], description: pendingDescription });
      pendingDescription = null;
    }
  }
  return members;
}

/** Every `export type X = 'a' | 'b' …;` string-literal union declared in the travel type files. */
function travelUnions(): Map<string, string[]> {
  const dir = resolve(process.cwd(), 'src/types/hr');
  const unions = new Map<string, string[]>();
  for (const file of readdirSync(dir).filter((f) => /^travel.*\.ts$/.test(f))) {
    const source = readFileSync(resolve(dir, file), 'utf8');
    const pattern = /export type (\w+)\s*=\s*((?:\s*\|?\s*'[^']*')+)\s*;/g;
    for (const match of source.matchAll(pattern)) {
      unions.set(match[1], [...match[2].matchAll(/'([^']*)'/g)].map((m) => m[1]));
    }
  }
  return unions;
}

describe('staff travel enums', () => {
  it('every string union in the travel types has exactly the members of its C# enum', () => {
    const unions = travelUnions();
    const compared: string[] = [];
    const drift: string[] = [];

    for (const [name, members] of unions) {
      const csharp = csharpEnum(name);
      if (!csharp) continue;
      compared.push(name);
      const expected = csharp.map((m) => m.member).sort();
      const actual = [...members].sort();
      if (JSON.stringify(expected) !== JSON.stringify(actual)) {
        drift.push(`${name}: TypeScript [${actual.join(', ')}] vs C# [${expected.join(', ')}]`);
      }
    }

    expect(drift).toEqual([]);
    // ⚠ Not vacuous: when this file was written, 36 unions had a C# enum of the same name. A parse
    // change that silently matched none would otherwise pass with nothing compared.
    expect(compared.length).toBeGreaterThanOrEqual(36);
  });

  it.each([
    ['StaffTravelType', TRAVEL_TYPE_LABELS],
    ['StaffTravelPurpose', TRAVEL_PURPOSE_LABELS],
    ['StaffTravelPriority', TRAVEL_PRIORITY_LABELS],
    ['TravelInitiatorRole', TRAVEL_INITIATOR_ROLE_LABELS],
    ['TravelRiskLevel', TRAVEL_RISK_LEVEL_LABELS],
    ['TravelRequestCommentType', TRAVEL_COMMENT_TYPE_LABELS],
    ['TravelAttachmentType', TRAVEL_ATTACHMENT_TYPE_LABELS],
    ['StaffTravelRequestStatus', TRAVEL_REQUEST_STATUS_LABELS],
    ['FlightCabinClass', FLIGHT_CABIN_CLASS_LABELS],
  ] as const)('the %s options list C#\'s members in its order, labelled with its descriptions', (name, labels) => {
    const csharp = csharpEnum(name) ?? [];
    expect(csharp.length, `C# enum ${name} not found`).toBeGreaterThan(0);
    expect(enumValues(labels as Record<string, string>)).toEqual(csharp.map((m) => m.member));
    expect(Object.values(labels)).toEqual(csharp.map((m) => m.description ?? m.member));
  });
});
