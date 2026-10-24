# Meal activity snapshot format v1

Operator export owned by Diet. This is not an HTTP/gRPC endpoint or broker event.
Used to reconcile the create-only MealLoggedEvent v1 projection in Progress.

## JSON contract

Root fields: schemaVersion (integer, 1), snapshotId (UUID), capturedAtUtc (UTC
ISO instant), entries (array), recordCount (nonnegative integer), entriesSha256
(64 uppercase hex characters).

Each entry has exactly mealLogId (UUID), userId (UUID), businessDate (ISO date),
createdAt (UTC ISO instant). EventId for the corresponding event equals mealLogId.
Entries are ordered by PostgreSQL UUID ordering. No names, email or tokens.

Checksum: SHA-256 over the exact compact UTF-8 JSON text of each entry followed
by one LF byte (including the last entry). Header/footer are not hashed. Empty
entries have the SHA-256 digest of empty bytes. Consumers should hash the raw JSON
entry text, not reformat/reserialize it. Reformatting the file may invalidate it.
Unknown fields may be ignored after verifying the original bytes; unknown schema
versions must be rejected rather than guessed.

A complete file parses as one JSON object and includes the count and checksum
footer. Diet writes a temporary file and atomically publishes the final path only
on success. Existing destination files are never overwritten. capturedAtUtc is
an audit timestamp, not an event cursor or a global transaction watermark.

## Bootstrap/reconciliation

1. Upgrade all Diet writers to the event-producing version, then bind a durable
   Progress queue to longevia.events / diet.meal.logged before exporting/replay.
2. Export a RepeatableRead snapshot and verify count, checksum and unique IDs.
3. Replay from Diet while the queue stays bound. All snapshot IDs/business dates
   must exist in Progress, deduplicated by EventId and unique MealLogId.
4. Events committed after the snapshot are supplied by live producers. Do not
   discard late events based on capturedAtUtc or OccurredOn; use businessDate.
5. Progress owns its inbox, projection and completeness decision. Export success
   does not prove broker delivery or consumer import/reconciliation succeeded.

Store snapshots under ignored artifacts/diet. They contain user IDs and dates;
share through an authorized operator channel and delete when no longer needed.
There is no implicit public download route, cross-database access or DB mutation.
