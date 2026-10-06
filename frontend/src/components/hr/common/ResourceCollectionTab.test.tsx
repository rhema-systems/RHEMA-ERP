import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { z } from 'zod';

// Vitest compiles JSX the classic way, and the component does not import React itself (as layout.test.tsx does).
vi.stubGlobal('React', React);

// The menu is Radix's, which opens on pointer events jsdom does not drive. Rendered flat instead, so what each row's
// menu holds can be read directly — the rule under test is which rows get a ⋯ and what is in it, not how it opens.
vi.mock('@/components/ui/dropdown-menu', async () => {
  const { createElement: h } = await import('react');
  return {
    DropdownMenu: ({ children }: { children: React.ReactNode }) => h('div', null, children),
    DropdownMenuTrigger: ({ children }: { children: React.ReactNode }) => h('div', null, children),
    DropdownMenuContent: ({ children }: { children: React.ReactNode }) => h('div', { 'data-testid': 'menu' }, children),
    DropdownMenuItem: ({ children, onClick }: { children: React.ReactNode; onClick?: () => void }) =>
      h('button', { type: 'button', onClick }, children),
    DropdownMenuSeparator: () => h('hr', { 'data-testid': 'separator' }),
  };
});

vi.mock('@/components/ui/use-toast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

import { ResourceCollectionTab, type ResourceCollectionTabProps } from './ResourceCollectionTab';

interface Guest {
  id: string;
  name: string;
  answered: boolean;
}

const guests: Guest[] = [
  { id: 'g1', name: 'Ama Owusu', answered: true },
  { id: 'g2', name: 'Kofi Mensah', answered: false },
];

type Form = { name: string };

function renderTab(props: Partial<ResourceCollectionTabProps<Guest, Form>>) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ResourceCollectionTab<Guest, Form>
        parentId="event-1"
        title="participants"
        singular="participant"
        queryKey={['test', 'guests', Math.random()]}
        list={async () => guests}
        create={async () => undefined}
        update={async () => undefined}
        remove={async () => undefined}
        getId={(g) => g.id}
        columns={[{ header: 'Participant', cell: (g) => g.name }]}
        schema={z.object({ name: z.string() })}
        emptyForm={{ name: '' }}
        toForm={(g) => ({ name: g.name })}
        renderFields={() => null}
        {...props}
      />
    </QueryClientProvider>,
  );
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr') as HTMLElement;
const menuButton = (row: HTMLElement) => within(row).queryByText('Actions');

describe('ResourceCollectionTab row menu', () => {
  it('gives no ⋯ to a row whose every item is hidden, and keeps it on a row that has one', async () => {
    // A closed event's guest list: read-only, so no Edit or Remove; its one action shows only for an unanswered guest.
    renderTab({
      readOnly: true,
      actions: [{ label: 'Chase', visible: (g) => !g.answered, run: async () => undefined }],
    });

    const answered = await rowOf('Ama Owusu');
    expect(menuButton(answered)).toBeNull();
    expect(within(answered).queryByTestId('menu')).toBeNull();

    const unanswered = await rowOf('Kofi Mensah');
    expect(menuButton(unanswered)).not.toBeNull();
    expect(within(unanswered).getByText('Chase')).not.toBeNull();
  });

  it('gives no ⋯ to any row when nothing applies to any of them, and keeps the column', async () => {
    renderTab({
      readOnly: true,
      actions: [{ label: 'Chase', visible: () => false, run: async () => undefined }],
    });

    for (const name of ['Ama Owusu', 'Kofi Mensah']) {
      const row = await rowOf(name);
      expect(menuButton(row)).toBeNull();
      // The table still has its menu column, so the rows keep their shape.
      expect(row.querySelectorAll('td')).toHaveLength(2);
    }
  });

  it('shows Remove alone, with no separator above it, on a row where it is the only item', async () => {
    renderTab({
      allowUpdate: false,
      canRemoveItem: (g) => g.id === 'g2',
    });

    const kept = await rowOf('Ama Owusu');
    expect(menuButton(kept)).toBeNull();

    const removable = await rowOf('Kofi Mensah');
    expect(within(removable).getByText('Remove')).not.toBeNull();
    expect(within(removable).queryByTestId('separator')).toBeNull();
  });

  it('keeps the separator between the other items and Remove', async () => {
    renderTab({});

    const row = await rowOf('Ama Owusu');
    expect(within(row).getByText('Edit')).not.toBeNull();
    expect(within(row).getByText('Remove')).not.toBeNull();
    expect(within(row).getByTestId('separator')).not.toBeNull();
  });
});
