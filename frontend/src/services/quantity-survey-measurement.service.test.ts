import { afterEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { quantitySurveyMeasurementService as service } from './quantity-survey-measurement.service';

vi.mock('./api.service', () => ({ apiService: { post: vi.fn() } }));

const request = () => ({
  clientRequestId: 'retry-key',
  evidenceType: 'SupportingDocument' as const,
  title: 'Synthetic evidence',
  file: new File(['UAT ONLY'], 'evidence.txt', { type: 'text/plain' }),
});

afterEach(() => { vi.useRealTimers(); vi.clearAllMocks(); });

describe('measurement evidence upload', () => {
  it('allows scanning beyond the ordinary timeout, then cancels at the upload limit', async () => {
    vi.useFakeTimers();
    let signal: AbortSignal | undefined;
    vi.mocked(apiService.post).mockImplementation((_url, _data, suppliedSignal) => {
      signal = suppliedSignal;
      return new Promise((_resolve, reject) => signal?.addEventListener('abort', () => reject(new Error('aborted'))));
    });
    const result = service.addAttachment('sheet', request());
    const rejected = expect(result).rejects.toThrow('120 seconds');
    await vi.advanceTimersByTimeAsync(60_000);
    expect(signal?.aborted).toBe(false);
    await vi.advanceTimersByTimeAsync(60_000);
    await rejected;
    expect(signal?.aborted).toBe(true);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('preserves retry identity and clears the timer after a successful upload', async () => {
    vi.useFakeTimers();
    vi.mocked(apiService.post).mockResolvedValue({ id: 'attachment' });
    await expect(service.addAttachment('sheet', request())).resolves.toEqual({ id: 'attachment' });
    const [, data, signal] = vi.mocked(apiService.post).mock.calls[0];
    expect((data as FormData).get('clientRequestId')).toBe('retry-key');
    expect(signal?.aborted).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
  });
});
