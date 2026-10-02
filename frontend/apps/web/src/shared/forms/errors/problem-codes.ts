export const problemCodes = {
  requestInvalid: "request.invalid",
  requestMalformed: "request.malformed",
  featureDisabled: "feature.disabled",
  idempotencyKeyMissing: "idempotency.key-missing",
  idempotencyKeyInvalid: "idempotency.key-invalid",
  idempotencyInProgress: "idempotency.in-progress",
  idempotencyKeyReused: "idempotency.key-reused",
  idempotencyReplayUnavailable: "idempotency.replay-unavailable",
  antiforgeryTokenMissing: "antiforgery.token-missing",
  antiforgeryTokenInvalid: "antiforgery.token-invalid",
} as const;

export const queryCodePrefix = "query.";
