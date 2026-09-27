import React from 'react';
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { GlobalSearchRecordSource } from '@/lib/global-search';
import type { GlobalSearchNavigationEntry } from '@/lib/global-search-navigation';
import { GlobalSearch } from './GlobalSearch';

const mocks = vi.hoisted(() => ({
  user: { id: 'user-1', roles: ['Reader'], permissions: ['invoice.read'] },
  tenant: 'TENANT-A',
  navigation: [] as GlobalSearchNavigationEntry[],
  request: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({
  user: mocks.user,
  hasAnyRole: (roles: string[]) => roles.some(role => mocks.user.roles.includes(role)),
  hasAnyPermission: (permissions: string[]) => permissions.some(permission => mocks.user.permissions.includes(permission)),
}) }));
vi.mock('@/contexts/TenantContext', () => ({ useTenant: () => ({ currentTenantCode: mocks.tenant }) }));
vi.mock('@/services/api.service', () => ({ apiService: { silentRequest: mocks.request } }));
vi.mock('@/lib/global-search-navigation', async importOriginal => ({
  ...await importOriginal<typeof import('@/lib/global-search-navigation')>(),
  buildGlobalSearchNavigation: () => mocks.navigation,
}));
vi.mock('@/lib/global-search-record-sources', () => ({ GLOBAL_SEARCH_RECORD_SOURCES: [{
  id: 'invoices', module: 'Finance', label: 'Invoice', route: '/finance/invoices',
  endpoint: '/api/invoices', searchParam: 'search', idField: 'id', titleFields: ['number'],
  detailPath: '/finance/invoices/:id', permissions: ['invoice.read'],
}] satisfies GlobalSearchRecordSource[] }));
vi.mock('@/lib/global-search-procurement-sources', () => ({ GLOBAL_SEARCH_PROCUREMENT_SOURCES: [{
  id: 'orders', module: 'Procurement', label: 'Purchase order', route: '/procurement/orders',
  endpoint: '/api/orders', searchParam: 'search', idField: 'id', titleFields: ['number'],
}] satisfies GlobalSearchRecordSource[] }));
vi.mock('@/lib/global-search-inventory-sources', () => ({ GLOBAL_SEARCH_INVENTORY_SOURCES: [{
  id: 'items', module: 'Inventory', label: 'Item', route: '/inventory/items',
  endpoint: '/api/items', searchParam: 'search', idField: 'id', titleFields: ['number'],
  permissions: ['inventory.read'],
}] satisfies GlobalSearchRecordSource[] }));
vi.mock('next/link', () => ({ default: ({ href, onClick, children, prefetch: _prefetch, ...props }: React.ComponentProps<'a'> & { prefetch?: boolean }) => (
  <a {...props} href={href} onClick={event => { event.preventDefault(); mocks.navigate(href); onClick?.(event); }}>{children}</a>
) }));

const page = (title: string, href: string, module: string): GlobalSearchNavigationEntry => ({ title, href, module, breadcrumbs: [module] });
const input = () => screen.getByRole('combobox', { name: 'Search across the app' });
const type = (value: string) => fireEvent.change(input(), { target: { value } });
const advance = async (milliseconds = 350) => act(async () => { await vi.advanceTimersByTimeAsync(milliseconds); });
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(complete => { resolve = complete; });
  return { promise, resolve };
}

describe('global header search', () => {
  beforeEach(() => {
    vi.stubGlobal('React', React);
    vi.useFakeTimers();
    mocks.user = { id: 'user-1', roles: ['Reader'], permissions: ['invoice.read'] };
    mocks.tenant = 'TENANT-A';
    mocks.navigation = [page('Invoices', '/finance/invoices', 'Finance')];
    mocks.request.mockReset();
    mocks.request.mockResolvedValue({ items: [] });
    mocks.navigate.mockReset();
  });
  afterEach(() => {
    cleanup();
    vi.clearAllTimers();
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('shows local pages immediately and debounces record requests until typing pauses', async () => {
    render(<GlobalSearch />);
    type('i');
    await advance(500);
    expect(mocks.request).not.toHaveBeenCalled();
    type('inv');
    expect(screen.getByRole('option', { name: /Invoices/ })).toBeInTheDocument();
    await advance(300);
    type('invo');
    await advance(349);
    expect(mocks.request).not.toHaveBeenCalled();
    await advance(1);
    expect(mocks.request).toHaveBeenCalledTimes(1);
    expect(mocks.request).toHaveBeenCalledWith('/api/invoices?search=invo', expect.objectContaining({ signal: expect.any(AbortSignal) }));
  });

  it('aborts superseded requests and ignores late results from the earlier query', async () => {
    const old = deferred<{ items: { id: string; number: string }[] }>();
    mocks.request.mockImplementation((endpoint: string) => endpoint.includes('old') ? old.promise : Promise.resolve({ items: [{ id: '2', number: 'New invoice' }] }));
    render(<GlobalSearch />);
    type('old');
    await advance();
    const oldSignal: AbortSignal = mocks.request.mock.calls[0][1].signal;
    type('new');
    expect(oldSignal.aborted).toBe(true);
    await advance();
    expect(screen.getByText('New invoice')).toBeInTheDocument();
    await act(async () => old.resolve({ items: [{ id: '1', number: 'Old invoice' }] }));
    expect(screen.queryByText('Old invoice')).not.toBeInTheDocument();
    expect(screen.getByText('New invoice')).toBeInTheDocument();
  });

  it.each(['tenant', 'user', 'permissions'] as const)('clears prior results and pending work when the %s changes', async boundary => {
    mocks.request.mockResolvedValueOnce({ items: [{ id: '1', number: 'Private invoice' }] });
    const pending = deferred<{ items: { id: string; number: string }[] }>();
    mocks.request.mockImplementationOnce(() => pending.promise);
    const view = render(<GlobalSearch />);
    type('private');
    await advance();
    expect(screen.getByText('Private invoice')).toBeInTheDocument();
    type('pending');
    await advance();
    const oldSignal: AbortSignal = mocks.request.mock.calls[1][1].signal;
    if (boundary === 'tenant') mocks.tenant = 'TENANT-B';
    else if (boundary === 'user') mocks.user = { ...mocks.user, id: 'user-2' };
    else mocks.user = { ...mocks.user, permissions: [] };
    view.rerender(<GlobalSearch />);
    expect(oldSignal.aborted).toBe(true);
    expect(input()).toHaveValue('');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    await act(async () => pending.resolve({ items: [{ id: '2', number: 'Old session result' }] }));
    fireEvent.focus(input());
    expect(screen.queryByText('Private invoice')).not.toBeInTheDocument();
    expect(screen.queryByText('Old session result')).not.toBeInTheDocument();
  });

  it('keeps the selected record when a slower module inserts a result ahead of it', async () => {
    mocks.navigation.push(page('Orders', '/procurement/orders', 'Procurement'));
    const later = deferred<{ items: { id: string; number: string }[] }>();
    mocks.request.mockResolvedValueOnce({ items: [{ id: 'chosen', number: 'INV-Z' }] }).mockReturnValueOnce(later.promise);
    render(<GlobalSearch />);
    type('INV'); await advance();
    fireEvent.keyDown(input(), { key: 'ArrowDown' });
    await act(async () => later.resolve({ items: [{ id: 'earlier', number: 'INV-A' }] }));
    expect(screen.getByRole('option', { name: /INV-Z/ })).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(input(), { key: 'Enter' });
    expect(mocks.navigate).toHaveBeenCalledWith('/finance/invoices/chosen');
  });

  it('opens the keyboard selection with Enter and dismisses with Escape', () => {
    mocks.navigation = [page('Invoices', '/finance/invoices', 'Finance'), page('Invoice adjustments', '/finance/adjustments', 'Finance')];
    render(<GlobalSearch />);
    type('invoice');
    fireEvent.keyDown(input(), { key: 'ArrowDown' });
    fireEvent.keyDown(input(), { key: 'ArrowDown' });
    expect(screen.getByRole('option', { name: /Invoice adjustments/ })).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(input(), { key: 'Enter' });
    expect(mocks.navigate).toHaveBeenCalledWith('/finance/adjustments');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    fireEvent.focus(input());
    fireEvent.keyDown(input(), { key: 'Escape' });
    expect(input()).toHaveAttribute('aria-expanded', 'false');
    fireEvent.keyDown(input(), { key: 'Enter' });
    expect(mocks.navigate).toHaveBeenCalledTimes(1);
  });

  it('reopens on a click after Escape without requiring the focused input to blur', async () => {
    const pending = deferred<{ items: { id: string; number: string }[] }>();
    mocks.request.mockReturnValueOnce(pending.promise)
      .mockResolvedValue({ items: [{ id: 'current', number: 'Current invoice' }] });
    render(<GlobalSearch />);
    act(() => input().focus());
    type('invoice');
    await advance();
    const closedSignal: AbortSignal = mocks.request.mock.calls[0][1].signal;

    fireEvent.keyDown(input(), { key: 'Escape' });
    expect(input()).toHaveFocus();
    expect(input()).toHaveAttribute('aria-expanded', 'false');
    expect(closedSignal.aborted).toBe(true);

    fireEvent.click(input());
    expect(input()).toHaveFocus();
    expect(input()).toHaveValue('invoice');
    expect(input()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    await advance(349);
    expect(mocks.request).toHaveBeenCalledTimes(1);
    await advance(1);
    expect(mocks.request).toHaveBeenCalledTimes(2);
    expect(screen.getByText('Current invoice')).toBeInTheDocument();

    await act(async () => pending.resolve({ items: [{ id: 'old', number: 'Stale invoice' }] }));
    expect(screen.queryByText('Stale invoice')).not.toBeInTheDocument();
    expect(screen.getByText('Current invoice')).toBeInTheDocument();
  });

  it('never requests providers for hidden routes or missing provider permissions', async () => {
    mocks.navigation.push(page('Items', '/inventory/items', 'Inventory'));
    render(<GlobalSearch />);
    type('sample');
    await advance();
    expect(mocks.request.mock.calls.map(call => call[0])).toEqual(['/api/invoices?search=sample']);
  });

  it('selects the final result when ArrowUp starts keyboard navigation', () => {
    mocks.navigation = [page('Invoices', '/finance/invoices', 'Finance'), page('Invoice adjustments', '/finance/adjustments', 'Finance')];
    render(<GlobalSearch />);
    type('invoice');
    fireEvent.keyDown(input(), { key: 'ArrowUp' });
    expect(screen.getByRole('option', { name: /Invoice adjustments/ })).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(input(), { key: 'Enter' });
    expect(mocks.navigate).toHaveBeenCalledWith('/finance/adjustments');
  });

  it('shows server failure status while retaining accessible matching pages', async () => {
    mocks.request.mockRejectedValue({ status: 503 });
    render(<GlobalSearch />);
    type('invoice');
    await advance();
    expect(screen.getByRole('status')).toHaveTextContent('Some record searches are unavailable. Try again.');
    expect(screen.getByRole('option', { name: /Invoices/ })).toBeInTheDocument();
    expect(screen.queryByLabelText('Searching')).not.toBeInTheDocument();
  });

  it('restricts record requests to the selected module', async () => {
    mocks.navigation.push(page('Orders', '/procurement/orders', 'Procurement'));
    render(<GlobalSearch />);
    type('sample');
    fireEvent.change(screen.getByRole('combobox', { name: 'Search module' }), { target: { value: 'Procurement' } });
    await advance();
    expect(mocks.request.mock.calls.map(call => call[0])).toEqual(['/api/orders?search=sample']);
  });
});
