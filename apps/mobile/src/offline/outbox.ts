import * as Crypto from "expo-crypto";
import type { SQLiteDatabase } from "expo-sqlite";
import { activateOfflineScope, openOfflineDatabase, type OfflineScope } from "@/src/offline/database";

export const outboxStates = [
  "DraftLocal",
  "Pending",
  "Syncing",
  "Synced",
  "Rejected",
  "Conflict",
  "ManualReview",
] as const;

export type OutboxState = (typeof outboxStates)[number];

export interface OutboxCommandInput<TPayload = unknown> {
  clientMutationId: string;
  localReference: string;
  commandType: string;
  schemaVersion: number;
  tillSessionId: string;
  offlineGrantId: string;
  payload: TPayload;
}

export interface OutboxMessage<TPayload = unknown, TResult = unknown> extends OutboxCommandInput<TPayload> {
  scopeKey: string;
  tenantId: string;
  deviceId: string;
  storeId: string;
  tillId: string;
  payloadHash: string;
  state: OutboxState;
  attemptCount: number;
  createdAtUtc: string;
  lastAttemptAtUtc?: string;
  retryAfterUtc?: string;
  serverResult?: TResult;
  errorCode?: string;
  errorDetail?: string;
}

interface OutboxRow {
  scope_key: string;
  client_mutation_id: string;
  local_reference: string;
  command_type: string;
  schema_version: number;
  tenant_id: string;
  device_id: string;
  store_id: string;
  till_id: string;
  till_session_id: string;
  offline_grant_id: string;
  payload_json: string;
  payload_hash: string;
  state: string;
  attempt_count: number;
  created_at_utc: string;
  last_attempt_at_utc: string | null;
  retry_after_utc: string | null;
  server_result_json: string | null;
  error_code: string | null;
  error_detail: string | null;
}

const allowedTransitions: Readonly<Record<OutboxState, readonly OutboxState[]>> = {
  DraftLocal: ["Pending"],
  Pending: ["Syncing"],
  Syncing: ["Pending", "Synced", "Rejected", "Conflict", "ManualReview"],
  Synced: [],
  Rejected: [],
  Conflict: ["ManualReview"],
  ManualReview: ["Pending"],
};

export function assertOutboxTransition(from: OutboxState, to: OutboxState): void {
  if (!allowedTransitions[from].includes(to)) {
    throw new Error(`The outbox transition ${from} -> ${to} is not allowed.`);
  }
}

export function calculateOutboxRetryDelayMs(attemptCount: number): number {
  const safeAttempt = Math.max(1, Math.min(Math.trunc(attemptCount), 10));
  return Math.min(15 * 60_000, 5_000 * 2 ** (safeAttempt - 1));
}

export function canonicalOutboxJson(value: unknown): string {
  const active = new Set<object>();
  const visit = (current: unknown, arrayValue = false): string | undefined => {
    if (current === null) return "null";
    if (typeof current === "string" || typeof current === "boolean") return JSON.stringify(current);
    if (typeof current === "number") {
      if (!Number.isFinite(current)) throw new Error("The outbox payload contains a non-finite number.");
      return JSON.stringify(current);
    }
    if (typeof current === "undefined" || typeof current === "function" || typeof current === "symbol") {
      return arrayValue ? "null" : undefined;
    }
    if (typeof current === "bigint") throw new Error("The outbox payload contains an unsupported bigint.");
    if (typeof current !== "object") throw new Error("The outbox payload contains an unsupported value.");
    if (active.has(current)) throw new Error("The outbox payload contains a circular reference.");
    active.add(current);
    try {
      if (Array.isArray(current)) {
        return `[${current.map(item => visit(item, true) ?? "null").join(",")}]`;
      }
      const prototype = Object.getPrototypeOf(current);
      if (prototype !== Object.prototype && prototype !== null) {
        throw new Error("The outbox payload contains an unsupported object type.");
      }
      const entries = Object.entries(current)
        .sort(([left], [right]) => left.localeCompare(right))
        .map(([key, item]) => {
          const serialized = visit(item);
          return serialized === undefined ? undefined : `${JSON.stringify(key)}:${serialized}`;
        })
        .filter((item): item is string => item !== undefined);
      return `{${entries.join(",")}}`;
    } finally {
      active.delete(current);
    }
  };
  return visit(value) ?? "null";
}

export async function hashOutboxPayload(
  payload: unknown,
  digest: (canonicalJson: string) => Promise<string> = value =>
    Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, value),
): Promise<{ payloadJson: string; payloadHash: string }> {
  const payloadJson = canonicalOutboxJson(payload);
  return { payloadJson, payloadHash: (await digest(payloadJson)).toLocaleLowerCase() };
}

export class SqliteOutbox {
  private constructor(
    private readonly database: SQLiteDatabase,
    readonly scope: OfflineScope,
  ) {}

  static async open(scope: OfflineScope): Promise<SqliteOutbox> {
    await activateOfflineScope(scope);
    return new SqliteOutbox(await openOfflineDatabase(), scope);
  }

  static fromDatabase(database: SQLiteDatabase, scope: OfflineScope): SqliteOutbox {
    return new SqliteOutbox(database, scope);
  }

  async createDraft<TPayload>(input: OutboxCommandInput<TPayload>): Promise<OutboxMessage<TPayload>> {
    return this.persist(input, "DraftLocal");
  }

  async enqueue<TPayload>(input: OutboxCommandInput<TPayload>): Promise<OutboxMessage<TPayload>> {
    return this.persist(input, "Pending");
  }

  async queueDraft(clientMutationId: string): Promise<OutboxMessage> {
    return this.transition(clientMutationId, "DraftLocal", "Pending");
  }

  async claimNext(now = new Date()): Promise<OutboxMessage | null> {
    const nowUtc = validDate(now, "dispatch time").toISOString();
    return exclusiveValue(this.database, async transaction => {
      const row = await transaction.getFirstAsync<OutboxRow>(
        `SELECT * FROM outbox_message
         WHERE scope_key = ? AND state = 'Pending'
           AND (retry_after_utc IS NULL OR retry_after_utc <= ?)
         ORDER BY created_at_utc, client_mutation_id
         LIMIT 1`,
        this.scope.scopeKey,
        nowUtc,
      );
      if (!row) return null;
      assertOutboxTransition("Pending", "Syncing");
      const result = await transaction.runAsync(
        `UPDATE outbox_message
         SET state = 'Syncing', attempt_count = attempt_count + 1,
             last_attempt_at_utc = ?, retry_after_utc = NULL,
             error_code = NULL, error_detail = NULL
         WHERE scope_key = ? AND client_mutation_id = ? AND state = 'Pending'`,
        nowUtc,
        this.scope.scopeKey,
        row.client_mutation_id,
      );
      if (result.changes !== 1) return null;
      return this.requireByMutationId(transaction, row.client_mutation_id);
    });
  }

  async markSynced(clientMutationId: string, serverResult: unknown): Promise<OutboxMessage> {
    return this.transition(clientMutationId, "Syncing", "Synced", {
      serverResultJson: canonicalOutboxJson(serverResult),
    });
  }

  async markRejected(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    return this.transition(clientMutationId, "Syncing", "Rejected", {
      errorCode: required(errorCode, 100, "outbox error code"),
      errorDetail: required(errorDetail, 1_000, "outbox error detail"),
    });
  }

  async markConflict(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    return this.transition(clientMutationId, "Syncing", "Conflict", {
      errorCode: required(errorCode, 100, "outbox error code"),
      errorDetail: required(errorDetail, 1_000, "outbox error detail"),
    });
  }

  async markManualReview(clientMutationId: string, errorCode: string, errorDetail: string): Promise<OutboxMessage> {
    const current = await this.requireByMutationId(this.database, clientMutationId);
    if (current.state !== "Syncing" && current.state !== "Conflict") {
      throw new Error(`The outbox message is ${current.state}, not Syncing or Conflict.`);
    }
    return this.transition(clientMutationId, current.state, "ManualReview", {
      errorCode: required(errorCode, 100, "outbox error code"),
      errorDetail: required(errorDetail, 1_000, "outbox error detail"),
    });
  }

  async retry(clientMutationId: string, errorCode: string, errorDetail: string, now = new Date()): Promise<OutboxMessage> {
    const attemptedAt = validDate(now, "retry time");
    return exclusiveValue(this.database, async transaction => {
      const current = await this.requireByMutationId(transaction, clientMutationId);
      if (current.state !== "Syncing") throw new Error(`The outbox message is ${current.state}, not Syncing.`);
      assertOutboxTransition("Syncing", "Pending");
      const retryAfter = new Date(attemptedAt.getTime() + calculateOutboxRetryDelayMs(current.attemptCount));
      await this.updateState(transaction, clientMutationId, "Syncing", "Pending", {
        retryAfterUtc: retryAfter.toISOString(),
        errorCode: required(errorCode, 100, "outbox error code"),
        errorDetail: required(errorDetail, 1_000, "outbox error detail"),
      });
      return this.requireByMutationId(transaction, clientMutationId);
    });
  }

  async requeueManualReview(clientMutationId: string): Promise<OutboxMessage> {
    return this.transition(clientMutationId, "ManualReview", "Pending");
  }

  async recoverInterruptedClaims(staleBefore: Date, now = new Date()): Promise<number> {
    const staleBeforeUtc = validDate(staleBefore, "stale claim cutoff").toISOString();
    const nowUtc = validDate(now, "recovery time").toISOString();
    const result = await this.database.runAsync(
      `UPDATE outbox_message
       SET state = 'Pending', retry_after_utc = ?,
           error_code = 'OUTBOX_DISPATCH_INTERRUPTED',
           error_detail = 'The previous synchronization attempt was interrupted and will be retried.'
       WHERE scope_key = ? AND state = 'Syncing'
         AND last_attempt_at_utc IS NOT NULL AND last_attempt_at_utc <= ?`,
      nowUtc,
      this.scope.scopeKey,
      staleBeforeUtc,
    );
    return result.changes;
  }

  async get(clientMutationId: string): Promise<OutboxMessage | null> {
    const row = await this.database.getFirstAsync<OutboxRow>(
      "SELECT * FROM outbox_message WHERE scope_key = ? AND client_mutation_id = ?",
      this.scope.scopeKey,
      required(clientMutationId, 128, "client mutation ID"),
    );
    return row ? mapRow(row) : null;
  }

  async list(states?: readonly OutboxState[], limit = 100): Promise<OutboxMessage[]> {
    const take = Math.max(1, Math.min(Math.trunc(limit), 500));
    if (!states || states.length === 0) {
      const rows = await this.database.getAllAsync<OutboxRow>(
        "SELECT * FROM outbox_message WHERE scope_key = ? ORDER BY created_at_utc, client_mutation_id LIMIT ?",
        this.scope.scopeKey,
        take,
      );
      return rows.map(mapRow);
    }
    for (const state of states) requireOutboxState(state);
    const placeholders = states.map(() => "?").join(",");
    const rows = await this.database.getAllAsync<OutboxRow>(
      `SELECT * FROM outbox_message WHERE scope_key = ? AND state IN (${placeholders})
       ORDER BY created_at_utc, client_mutation_id LIMIT ?`,
      this.scope.scopeKey,
      ...states,
      take,
    );
    return rows.map(mapRow);
  }

  private async persist<TPayload>(
    input: OutboxCommandInput<TPayload>,
    initialState: "DraftLocal" | "Pending",
  ): Promise<OutboxMessage<TPayload>> {
    const normalized = normalizeInput(input);
    assertNoOutboxSecrets(input.payload);
    const { payloadJson, payloadHash } = await hashOutboxPayload(input.payload);
    const createdAtUtc = new Date().toISOString();
    return exclusiveValue(this.database, async transaction => {
      const existing = await transaction.getFirstAsync<OutboxRow>(
        `SELECT * FROM outbox_message
         WHERE scope_key = ? AND (client_mutation_id = ? OR local_reference = ?)
         LIMIT 1`,
        this.scope.scopeKey,
        normalized.clientMutationId,
        normalized.localReference,
      );
      if (existing) {
        const sameIdentity = existing.client_mutation_id === normalized.clientMutationId
          && existing.local_reference === normalized.localReference
          && existing.command_type === normalized.commandType
          && existing.schema_version === normalized.schemaVersion
          && existing.till_session_id === normalized.tillSessionId
          && existing.offline_grant_id === normalized.offlineGrantId
          && existing.payload_hash === payloadHash;
        if (!sameIdentity) throw new Error("OUTBOX_IDENTITY_CONFLICT: the mutation ID or local reference already belongs to different work.");
        return mapRow(existing) as OutboxMessage<TPayload>;
      }
      await transaction.runAsync(
        `INSERT INTO outbox_message(
           scope_key, client_mutation_id, local_reference, command_type, schema_version,
           tenant_id, device_id, store_id, till_id, till_session_id, offline_grant_id,
           payload_json, payload_hash, state, attempt_count, created_at_utc)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, ?)`,
        this.scope.scopeKey,
        normalized.clientMutationId,
        normalized.localReference,
        normalized.commandType,
        normalized.schemaVersion,
        this.scope.tenantId,
        this.scope.deviceId,
        this.scope.storeId,
        this.scope.tillId,
        normalized.tillSessionId,
        normalized.offlineGrantId,
        payloadJson,
        payloadHash,
        initialState,
        createdAtUtc,
      );
      return this.requireByMutationId(transaction, normalized.clientMutationId) as Promise<OutboxMessage<TPayload>>;
    });
  }

  private async transition(
    clientMutationId: string,
    from: OutboxState,
    to: OutboxState,
    updates: StateUpdates = {},
  ): Promise<OutboxMessage> {
    assertOutboxTransition(from, to);
    return exclusiveValue(this.database, async transaction => {
      await this.updateState(transaction, clientMutationId, from, to, updates);
      return this.requireByMutationId(transaction, clientMutationId);
    });
  }

  private async updateState(
    database: SQLiteDatabase,
    clientMutationId: string,
    from: OutboxState,
    to: OutboxState,
    updates: StateUpdates,
  ): Promise<void> {
    const result = await database.runAsync(
      `UPDATE outbox_message
       SET state = ?, retry_after_utc = ?, server_result_json = ?, error_code = ?, error_detail = ?
       WHERE scope_key = ? AND client_mutation_id = ? AND state = ?`,
      to,
      updates.retryAfterUtc ?? null,
      updates.serverResultJson ?? null,
      updates.errorCode ?? null,
      updates.errorDetail ?? null,
      this.scope.scopeKey,
      required(clientMutationId, 128, "client mutation ID"),
      from,
    );
    if (result.changes !== 1) {
      const current = await this.getState(database, clientMutationId);
      throw new Error(`The outbox message is ${current ?? "missing"}, not ${from}.`);
    }
  }

  private async requireByMutationId(database: SQLiteDatabase, clientMutationId: string): Promise<OutboxMessage> {
    const row = await database.getFirstAsync<OutboxRow>(
      "SELECT * FROM outbox_message WHERE scope_key = ? AND client_mutation_id = ?",
      this.scope.scopeKey,
      required(clientMutationId, 128, "client mutation ID"),
    );
    if (!row) throw new Error("The outbox message could not be found in the active scope.");
    return mapRow(row);
  }

  private async getState(database: SQLiteDatabase, clientMutationId: string): Promise<string | null> {
    const row = await database.getFirstAsync<{ state: string }>(
      "SELECT state FROM outbox_message WHERE scope_key = ? AND client_mutation_id = ?",
      this.scope.scopeKey,
      required(clientMutationId, 128, "client mutation ID"),
    );
    return row?.state ?? null;
  }
}

interface StateUpdates {
  retryAfterUtc?: string;
  serverResultJson?: string;
  errorCode?: string;
  errorDetail?: string;
}

function normalizeInput<TPayload>(input: OutboxCommandInput<TPayload>): OutboxCommandInput<TPayload> {
  const schemaVersion = Math.trunc(input.schemaVersion);
  if (schemaVersion < 1 || schemaVersion > 100) throw new Error("The outbox schema version is invalid.");
  return {
    ...input,
    clientMutationId: required(input.clientMutationId, 128, "client mutation ID"),
    localReference: required(input.localReference, 100, "local reference"),
    commandType: required(input.commandType, 100, "command type"),
    schemaVersion,
    tillSessionId: required(input.tillSessionId, 100, "till session ID"),
    offlineGrantId: required(input.offlineGrantId, 100, "offline grant ID"),
  };
}

function required(value: string, maximum: number, label: string): string {
  const normalized = value?.trim();
  if (!normalized || normalized.length > maximum) throw new Error(`The ${label} is required and must not exceed ${maximum} characters.`);
  return normalized;
}

export function assertNoOutboxSecrets(value: unknown): void {
  const forbiddenKeys = new Set([
    "token",
    "accesstoken",
    "refreshtoken",
    "offlinegranttoken",
    "password",
    "signingsecret",
    "clientsecret",
    "authorization",
  ]);
  const seen = new Set<object>();
  const visit = (current: unknown): void => {
    if (!current || typeof current !== "object") return;
    if (seen.has(current)) return;
    seen.add(current);
    if (Array.isArray(current)) {
      current.forEach(visit);
      return;
    }
    for (const [key, item] of Object.entries(current)) {
      const normalizedKey = key.replace(/[^a-z0-9]/gi, "").toLocaleLowerCase();
      if (forbiddenKeys.has(normalizedKey)) {
        throw new Error(`The outbox payload must not persist the sensitive field ${key}.`);
      }
      visit(item);
    }
  };
  visit(value);
}

function validDate(value: Date, label: string): Date {
  if (!(value instanceof Date) || !Number.isFinite(value.getTime())) throw new Error(`The ${label} is invalid.`);
  return value;
}

function requireOutboxState(value: string): asserts value is OutboxState {
  if (!(outboxStates as readonly string[]).includes(value)) throw new Error(`Unknown outbox state: ${value}.`);
}

function mapRow(row: OutboxRow): OutboxMessage {
  requireOutboxState(row.state);
  return {
    scopeKey: row.scope_key,
    clientMutationId: row.client_mutation_id,
    localReference: row.local_reference,
    commandType: row.command_type,
    schemaVersion: row.schema_version,
    tenantId: row.tenant_id,
    deviceId: row.device_id,
    storeId: row.store_id,
    tillId: row.till_id,
    tillSessionId: row.till_session_id,
    offlineGrantId: row.offline_grant_id,
    payload: JSON.parse(row.payload_json),
    payloadHash: row.payload_hash,
    state: row.state,
    attemptCount: row.attempt_count,
    createdAtUtc: row.created_at_utc,
    lastAttemptAtUtc: row.last_attempt_at_utc ?? undefined,
    retryAfterUtc: row.retry_after_utc ?? undefined,
    serverResult: row.server_result_json ? JSON.parse(row.server_result_json) : undefined,
    errorCode: row.error_code ?? undefined,
    errorDetail: row.error_detail ?? undefined,
  };
}

async function exclusiveValue<T>(
  database: SQLiteDatabase,
  action: (transaction: SQLiteDatabase) => Promise<T>,
): Promise<T> {
  let completed = false;
  let value!: T;
  await database.withExclusiveTransactionAsync(async transaction => {
    value = await action(transaction);
    completed = true;
  });
  if (!completed) throw new Error("The outbox transaction did not complete.");
  return value;
}
