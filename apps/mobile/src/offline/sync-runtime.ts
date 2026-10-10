import { sessionScope } from "@/src/offline/catalogue-runtime";
import { MobilePosOutboxDispatcher } from "@/src/offline/dispatcher";
import { SqliteOutbox, type OutboxMessage, type OutboxState } from "@/src/offline/outbox";
import { hydrateSynchronizedSessionReceipts } from "@/src/offline/receipt-runtime";
import type { MobilePosBootstrap, UserInfo } from "@/src/types/api";

const visibleStates: readonly OutboxState[] = [
  "DraftLocal",
  "Pending",
  "Syncing",
  "Rejected",
  "Conflict",
  "ManualReview",
];

export interface OutboxSummary {
  total: number;
  pending: number;
  rejected: number;
  conflict: number;
  manualReview: number;
  messages: OutboxMessage[];
  receiptCacheFailures: number;
}

export async function openSessionOutbox(user: UserInfo, bootstrap: MobilePosBootstrap): Promise<SqliteOutbox> {
  return SqliteOutbox.open(sessionScope(user, bootstrap));
}

export async function loadSessionOutboxSummary(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
): Promise<OutboxSummary> {
  const messages = await (await openSessionOutbox(user, bootstrap)).list(visibleStates, 500);
  return summarize(messages);
}

export async function synchronizeSessionOutbox(
  user: UserInfo,
  bootstrap: MobilePosBootstrap,
  maximumCommands = 25,
): Promise<OutboxSummary> {
  const outbox = await openSessionOutbox(user, bootstrap);
  await outbox.recoverInterruptedClaims(new Date(Date.now() - 5 * 60_000));
  const dispatcher = new MobilePosOutboxDispatcher(outbox);
  for (let index = 0; index < maximumCommands; index += 1) {
    const outcome = await dispatcher.dispatchNext();
    if (outcome === "Idle" || outcome === "RetryScheduled") break;
  }
  const summary = summarize(await outbox.list(visibleStates, 500));
  const receiptHydration = await hydrateSynchronizedSessionReceipts(user, bootstrap, 500);
  return { ...summary, receiptCacheFailures: receiptHydration.failed };
}

function summarize(messages: OutboxMessage[]): OutboxSummary {
  return {
    total: messages.length,
    pending: messages.filter(message => ["DraftLocal", "Pending", "Syncing"].includes(message.state)).length,
    rejected: messages.filter(message => message.state === "Rejected").length,
    conflict: messages.filter(message => message.state === "Conflict").length,
    manualReview: messages.filter(message => message.state === "ManualReview").length,
    messages,
    receiptCacheFailures: 0,
  };
}
