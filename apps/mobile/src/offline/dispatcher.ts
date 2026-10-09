import { ApiProblem, mobileApi } from "@/src/api/client";
import type { MobilePosOfflineGrant, MobilePosSyncPushRequest, MobilePosSyncPushResult } from "@/src/types/api";
import { loadOfflineGrant } from "@/src/storage/secure-session";
import type { OutboxMessage } from "./outbox";
import { SqliteOutbox } from "./outbox";

export interface OutboxDispatchStore {
  claimNext(now?: Date): Promise<OutboxMessage | null>;
  markSynced(clientMutationId: string, serverResult: unknown): Promise<OutboxMessage>;
  markRejected(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage>;
  markConflict(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage>;
  markManualReview(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage>;
  retry(clientMutationId: string, errorCode: string, errorDetail: string, now?: Date): Promise<OutboxMessage>;
}

export type OfflineGrantLoader = () => Promise<MobilePosOfflineGrant | null>;
export type OfflinePushTransport = (request: MobilePosSyncPushRequest) => Promise<MobilePosSyncPushResult>;

export type DispatchOutcome = "Idle" | "Synced" | "Rejected" | "Conflict" | "RetryScheduled" | "ManualReview";

export class MobilePosOutboxDispatcher {
  constructor(
    private readonly outbox: OutboxDispatchStore,
    private readonly grantLoader: OfflineGrantLoader = loadOfflineGrant,
    private readonly push: OfflinePushTransport = request => mobileApi.pushOfflineCommand(request),
  ) {}

  async dispatchNext(now = new Date()): Promise<DispatchOutcome> {
    const message = await this.outbox.claimNext(now);
    if (!message) return "Idle";

    try {
      const grant = await this.grantLoader();
      if (!grant) {
        await this.outbox.markManualReview(
          message.clientMutationId,
          "MOBILE_POS_OFFLINE_GRANT_MISSING",
          "The secure offline authorization is no longer available for this queued command.",
        );
        return "ManualReview";
      }
      const mismatch = grant.id !== message.offlineGrantId
        || grant.mobilePosDeviceId !== message.deviceId
        || grant.mobilePosStoreId !== message.storeId
        || grant.mobilePosTillId !== message.tillId
        || grant.cashierTillSessionId !== message.tillSessionId;
      if (mismatch) {
        await this.outbox.markManualReview(
          message.clientMutationId,
          "MOBILE_POS_OFFLINE_GRANT_SCOPE_MISMATCH",
          "The secure offline authorization does not match the queued command scope.",
        );
        return "ManualReview";
      }

      const result = await this.push({
        offlineGrantToken: grant.token,
        offlineGrantId: message.offlineGrantId,
        deviceId: message.deviceId,
        storeId: message.storeId,
        tillId: message.tillId,
        tillSessionId: message.tillSessionId,
        clientMutationId: message.clientMutationId,
        localReference: message.localReference,
        commandType: message.commandType,
        schemaVersion: message.schemaVersion,
        payloadHash: message.payloadHash,
        payload: message.payload,
      });
      if (result.clientMutationId !== message.clientMutationId) {
        await this.outbox.markManualReview(
          message.clientMutationId,
          "MOBILE_POS_SYNC_RESPONSE_IDENTITY_MISMATCH",
          "The server response did not match the queued command identity.",
        );
        return "ManualReview";
      }
      if (result.state === "Synced" && result.sale) {
        await this.outbox.markSynced(message.clientMutationId, result);
        return "Synced";
      }
      if (result.state === "Rejected") {
        await this.outbox.markRejected(
          message.clientMutationId,
          result.errorCode ?? "MOBILE_POS_SYNC_REJECTED",
          result.errorDetail ?? "The server rejected the queued command.",
        );
        return "Rejected";
      }
      if (result.state === "Conflict") {
        await this.outbox.markConflict(
          message.clientMutationId,
          result.errorCode ?? "MOBILE_POS_SYNC_CONFLICT",
          result.errorDetail ?? "The queued command conflicts with an earlier server command.",
        );
        return "Conflict";
      }
      await this.outbox.markManualReview(
        message.clientMutationId,
        "MOBILE_POS_SYNC_RESPONSE_INVALID",
        "The server returned an incomplete synchronization result.",
      );
      return "ManualReview";
    } catch (caught) {
      const problem = caught instanceof ApiProblem ? caught : null;
      const retryable = !problem || problem.status === 0 || problem.status === 401
        || problem.status === 408 || problem.status === 429 || problem.status >= 500;
      if (retryable) {
        await this.outbox.retry(
          message.clientMutationId,
          problem?.code ?? "MOBILE_POS_SYNC_TRANSPORT_FAILED",
          problem?.message ?? "The synchronization request did not complete.",
          now,
        );
        return "RetryScheduled";
      }
      await this.outbox.markManualReview(
        message.clientMutationId,
        problem.code ?? "MOBILE_POS_SYNC_UNEXPECTED_RESPONSE",
        problem.message,
      );
      return "ManualReview";
    }
  }
}

export async function openOutboxDispatcher(
  outbox: SqliteOutbox,
): Promise<MobilePosOutboxDispatcher> {
  return new MobilePosOutboxDispatcher(outbox);
}
