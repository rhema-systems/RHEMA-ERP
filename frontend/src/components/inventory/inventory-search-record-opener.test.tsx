import React, { StrictMode, useState } from 'react';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GlobalSearchRecordOpener } from '@/components/global-search/GlobalSearchRecordOpener';

const navigation = vi.hoisted(() => ({
  params: new URLSearchParams(), pathname: '/inventory/requisitions',
  router: { replace: vi.fn() }, error: vi.fn(),
}));
vi.mock('next/navigation', () => ({
  useSearchParams: () => navigation.params,
  usePathname: () => navigation.pathname,
  useRouter: () => navigation.router,
}));
vi.mock('sonner', () => ({ toast: { error: navigation.error } }));

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

describe('Inventory search record links', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    navigation.params = new URLSearchParams('recordId=older-record&status=approved');
    navigation.pathname = '/inventory/requisitions';
  });
  afterEach(cleanup);

  it('loads and opens a selected record once, including StrictMode effect replay, without saving', async () => {
    const pending = deferred<{ id: string }>();
    const load = vi.fn(() => pending.promise);
    const save = vi.fn();
    const opened = vi.fn();
    function Page() {
      const [record, setRecord] = useState<{ id: string }>();
      return <><GlobalSearchRecordOpener load={load} onOpen={value => { opened(value); setRecord(value); }} />
        {record && <section aria-label="Selected record"><span>{record.id}</span><button onClick={save}>Save</button></section>}</>;
    }
    render(<StrictMode><Page /></StrictMode>);
    await act(async () => pending.resolve({ id: 'older-record' }));
    expect(await screen.findByRole('region', { name: 'Selected record' })).toHaveTextContent('older-record');
    expect(opened).toHaveBeenCalledTimes(1);
    expect(save).not.toHaveBeenCalled();
    expect(navigation.router.replace).toHaveBeenCalledTimes(1);
    expect(navigation.router.replace).toHaveBeenCalledWith('/inventory/requisitions?status=approved', { scroll: false });
  });

  it('uses the latest callback without restarting an in-flight record read on rerender', async () => {
    const pending = deferred<{ id: string }>();
    const load = vi.fn(() => pending.promise);
    const firstOpen = vi.fn(); const latestOpen = vi.fn(); const replacementLoad = vi.fn();
    const page = render(<GlobalSearchRecordOpener load={load} onOpen={firstOpen} />);
    page.rerender(<GlobalSearchRecordOpener load={replacementLoad} onOpen={latestOpen} />);
    await act(async () => pending.resolve({ id: 'older-record' }));
    expect(load).toHaveBeenCalledTimes(1);
    expect(replacementLoad).not.toHaveBeenCalled();
    expect(firstOpen).not.toHaveBeenCalled();
    expect(latestOpen).toHaveBeenCalledOnce();
  });

  it('does not reopen a record when navigation returns equivalent search parameters', async () => {
    const load = vi.fn(async () => ({ id: 'older-record' })); const onOpen = vi.fn();
    const page = render(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    await waitFor(() => expect(onOpen).toHaveBeenCalledOnce());
    navigation.params = new URLSearchParams('recordId=older-record&status=approved');
    page.rerender(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    await act(async () => {});
    expect(load).toHaveBeenCalledOnce(); expect(onOpen).toHaveBeenCalledOnce();
  });

  it('reports a rejected read and leaves the record closed', async () => {
    const load = vi.fn().mockRejectedValue(new Error('This record is outside your warehouse scope.'));
    const onOpen = vi.fn();
    render(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    await waitFor(() => expect(navigation.error).toHaveBeenCalledWith('This record is outside your warehouse scope.'));
    expect(onOpen).not.toHaveBeenCalled();
    expect(navigation.router.replace).toHaveBeenCalledOnce();
  });

  it('opens a new selected id and ignores the late response for the previous id', async () => {
    const oldRead = deferred<{ id: string }>(); const nextRead = deferred<{ id: string }>();
    const load = vi.fn((id: string) => id === 'older-record' ? oldRead.promise : nextRead.promise);
    const onOpen = vi.fn();
    const page = render(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    navigation.params = new URLSearchParams('recordId=next-record&status=approved');
    page.rerender(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    await act(async () => nextRead.resolve({ id: 'next-record' }));
    await act(async () => oldRead.resolve({ id: 'older-record' }));
    expect(load.mock.calls.map(call => call[0])).toEqual(['older-record', 'next-record']);
    expect(onOpen).toHaveBeenCalledExactlyOnceWith({ id: 'next-record' });
    expect(navigation.router.replace).toHaveBeenCalledOnce();
  });

  it('does nothing without a selected id and permits reopening it after the link was consumed', async () => {
    navigation.params = new URLSearchParams('status=approved');
    const load = vi.fn(async (id: string) => ({ id })); const onOpen = vi.fn();
    const page = render(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    expect(load).not.toHaveBeenCalled();
    for (let selection = 0; selection < 2; selection++) {
      navigation.params = new URLSearchParams('recordId=same-record');
      page.rerender(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
      await waitFor(() => expect(onOpen).toHaveBeenCalledTimes(selection + 1));
      navigation.params = new URLSearchParams();
      page.rerender(<GlobalSearchRecordOpener load={load} onOpen={onOpen} />);
    }
    expect(load).toHaveBeenCalledTimes(2);
  });
});
