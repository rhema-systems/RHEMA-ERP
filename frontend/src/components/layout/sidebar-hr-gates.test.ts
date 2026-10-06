import { describe, expect, it } from 'vitest';

import { navigationItems, type NavItem } from './sidebar';

/**
 * Every Human Resources and Safety (SHE) menu leaf must carry a permission of its own, or sit
 * under a group that carries one, unless it is on the KEEP_OPEN list below.
 *
 * Why: `hr.access` is granted to every internal role (its job is keeping external accounts
 * out), so an HR leaf with no gate of its own is visible to every employee — and most such
 * leaves are desk registers that answer a plain employee with a 403. The 2026-09-03 permissions
 * review found 79 of them. This test keeps the list from growing back: a new desk screen must
 * name its family's Read permission, and a new self or manager surface must be listed here on
 * purpose, with the reason.
 *
 * KEEP_OPEN is the list the product owner confirmed on 2026-09-03. Each entry is keyed on the
 * caller's own token at the API (self, team or approval queue), so it works for a plain
 * employee or their line manager without any HR permission.
 */
const KEEP_OPEN = new Set<string>([
  '/hr/organogram', // open org chart; only the People dimension is gated
  '/hr/leave/requests', // self-or-HR.Leave.Read, opens on the caller's own history
  '/hr/leave/approvals', // the line manager's queue
  '/hr/performance/team-appraisals', // manager view keyed on the token
  '/hr/performance/interim-reviews', // manager's team queue (self door is /me)
  '/hr/performance/conversations', // token-keyed diary for managers and employees
  '/hr/performance/pip', // supervising/mine tabs; "All plans" tab is HR-only inside the page
  '/hr/performance/unit-goals', // unit goals are visible to the whole tenant by design; unit heads write
  '/hr/performance/employee-goals', // self, line manager, or HR.Performance.Read
  '/hr/performance/team-goals', // manager view keyed on the token
  '/hr/training/schedules', // open training calendar
  '/hr/training/approvals', // line supervisors approve nominations and hold no HR permission
  '/hr/probation/reviews', // "reviews to conduct" — the reviewer's own queue
  '/hr/discipline/approvals', // "awaiting my confirmation" — headship is data, not a role
  '/hr/travel/approvals', // the traveller's line manager approves stage 1 and holds no travel permission (travel closure lane 2)
  // Added 2026-10-06 with the user's ruling (company-schedule final closure lane 5a, F-57):
  '/hr/company-schedule/my-schedule', // the caller's own diary — the server takes the employee from the token (round 4, D5)
  '/hr/leave/calendar', // "mine" and "my team" are open to all staff; "Everyone" needs HR.Leave.Read on the server
  // Lane 5b (R4-10B.3, the user's ruling):
  '/hr/company-schedule/team', // the HR desk, or a unit's head for their own subtree — headship is data, decided per unit on the server
]);

const MODULE_GATES = new Set(['hr.access', 'she.access']);

const hasRealGate = (item: NavItem) =>
  Boolean(item.roles?.length) ||
  Boolean(item.permissions?.some((permission) => !MODULE_GATES.has(permission)));

const collectUngatedLeaves = (items: NavItem[], inheritedGate: boolean, out: string[]) => {
  for (const item of items) {
    const gated = inheritedGate || hasRealGate(item);
    if (item.children?.length) {
      collectUngatedLeaves(item.children, gated, out);
    } else if (!gated) {
      out.push(item.href);
    }
  }
};

const hrModules = navigationItems.filter((item) => item.href === '/hr' || item.href === '/hr/safety');

describe('HR and SHE sidebar gates', () => {
  it('finds both module roots', () => {
    expect(hrModules.map((item) => item.title).sort()).toEqual(['Human Resources', 'Safety (SHE)']);
  });

  it('leaves without a permission of their own are exactly the confirmed keep-open list', () => {
    const ungated: string[] = [];
    collectUngatedLeaves(hrModules, false, ungated);
    const unexpected = ungated.filter((href) => !KEEP_OPEN.has(href));
    expect(unexpected, 'HR/SHE leaves visible to every employee but not on KEEP_OPEN').toEqual([]);
  });

  it('every keep-open entry still exists in the menu', () => {
    const hrefs = new Set<string>();
    const walk = (items: NavItem[]) =>
      items.forEach((item) => {
        hrefs.add(item.href);
        if (item.children) walk(item.children);
      });
    walk(hrModules);
    const stale = [...KEEP_OPEN].filter((href) => !hrefs.has(href));
    expect(stale, 'KEEP_OPEN entries no longer in the menu — prune them').toEqual([]);
  });

  it('the SHE module gates on its own permission, not on hr.access alone', () => {
    const she = hrModules.find((item) => item.href === '/hr/safety');
    expect(she?.permissions).toContain('she.access');
  });
});
