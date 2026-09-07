import { apiService } from '@/services/api.service';
import {
  ProcurementExceptionalSourcingControlStatus as Status,
  type PrepareExceptionalSourcingRequest,
  type ProcurementExceptionalSourcingControl,
  type ProcurementExceptionalSourcingReadiness,
} from '@/types/procurement-exceptional-sourcing-control';

const root = (tenderId: string) => `/procurement/tenders/${tenderId}/exception-controls`;

type WireReadiness = Omit<ProcurementExceptionalSourcingReadiness, 'method'> & { method: number | string };
type WireControl = Omit<ProcurementExceptionalSourcingControl, 'method' | 'status'> & { method: number | string; status: number | string };

function methodNumber(value: number | string): number {
  const methods: Record<string, number> = { RestrictedTendering: 3, SingleSource: 4, PettyPurchase: 5 };
  const parsed = typeof value === 'number' ? value : methods[value] ?? (/^\d+$/.test(value) ? Number(value) : NaN);
  if (![3, 4, 5].includes(parsed)) throw new Error('Unrecognized sourcing method. Refresh the source before continuing.');
  return parsed;
}

function normalizeControl(value: WireControl): ProcurementExceptionalSourcingControl {
  const parsed = typeof value.status === 'number' ? value.status :
    /^\d+$/.test(value.status) ? Number(value.status) : Status[value.status as keyof typeof Status];
  if (typeof parsed !== 'number' || !Number.isInteger(parsed) || parsed < Status.Prepared || parsed > Status.Rejected)
    throw new Error('Unrecognized sourcing status. Refresh the source before continuing.');
  return { ...value, method: methodNumber(value.method), status: parsed };
}

const postControl = async (url: string, request: unknown) =>
  normalizeControl(await apiService.post<WireControl>(url, request));

export const procurementExceptionalSourcingControlService = {
  readiness: async (tenderId: string): Promise<ProcurementExceptionalSourcingReadiness> => {
    const value = await apiService.get<WireReadiness>(`${root(tenderId)}/readiness`);
    return { ...value, method: methodNumber(value.method) };
  },
  get: async (tenderId: string) => normalizeControl(await apiService.get<WireControl>(root(tenderId))),
  prepare: (tenderId: string, request: PrepareExceptionalSourcingRequest) =>
    postControl(`${root(tenderId)}/prepare`, request),
  submitApproval: (tenderId: string, rowVersion: string) =>
    postControl(`${root(tenderId)}/approval/submit`, { rowVersion }),
  decideApproval: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/approval/decision`, request),
  negotiation: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/negotiation`, request),
  recommendation: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/recommendation`, request),
  award: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/award`, request),
  contract: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/contract`, request),
  acceptance: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/acceptance`, request),
  filing: (tenderId: string, request: unknown) =>
    postControl(`${root(tenderId)}/post-award-filing`, request),
};
