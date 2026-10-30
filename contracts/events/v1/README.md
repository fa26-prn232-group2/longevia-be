# Event Contracts v1 — Longevia Backend

Canonical index of versioned async event schemas. Implementation source of truth:
C# records in `src/Shared/Events/` (each carries `SchemaVersion = 1`).

## Events

| Event | Routing key | Record | Producer | Consumer |
| --- | --- | --- | --- | --- |
| `UserRegisteredEvent` | `identity.user.registered` | `src/Shared/Events/UserRegisteredEvent.cs` | Identity (via transactional outbox — obligation recorded in `specs/mavil-001-infra-observability/data-model.md` §1.1, implementation owned by Identity feature) | Worker `worker.email` queue → verification email |
| `PasswordResetRequestedEvent` | `identity.user.password-reset-requested` | `src/Shared/Events/PasswordResetRequestedEvent.cs` | Identity (via transactional outbox) | Worker `worker.email` queue → reset email |
| `UserProfileUpdatedEvent` | `identity.user.profile-updated` | `src/Shared/Events/UserProfileUpdatedEvent.cs` | Identity (via transactional outbox) | No current consumer; signals a profile change without health profile data |
| `MealLoggedEvent` | `diet.meal.logged` | `src/Shared/Events/MealLoggedEvent.cs` | Diet (via transactional outbox) | Progress meal activity (consumer implementation pending) |

## Meal activity (Diet → Progress)

`MealLoggedEvent` v1, source `src/Shared/Events/MealLoggedEvent.cs`, routing key
`diet.meal.logged` on topic exchange `longevia.events`. Producer: Diet EF bus
outbox in diet_db. Intended consumer: Progress (implementation owned by dev 3).

Six payload fields: EventId (Guid, equals MealLogId), UserId (Guid), MealLogId
(Guid), BusinessDate (DateOnly), CreatedAt (UTC instant), OccurredOn (UTC enqueue
time). SchemaVersion = 1 is the shared contract constant, not a seventh JSON field.
Create-only: multiple meals on a date contribute one active day. Replay keeps
EventId/MealLogId/BusinessDate/CreatedAt, refreshes OccurredOn; never order meal
activity by replay time. Inbox dedup EventId + unique MealLogId must be durable.
No edit/delete semantics exist in v1. Bind a durable consumer queue before replay.
Diet supports explicit startup replay of all existing logs via
`Diet__ReplayMealActivitiesOnStart=true`; queued count is not delivery/completeness.
Progress must drain/reconcile before claiming complete history.

## Versioning policy

- Additive optional field: backward compatible, version stays `1`. Consumers MUST ignore unknown fields.
- Rename/remove field or semantic change: new version + migration plan. Old version stays consumable until all consumers migrate.

## Topology

See `specs/mavil-001-infra-observability/data-model.md` §1.3: topic exchange
`longevia.events`, durable queue `worker.email`, DLX/DLQ `worker.email.dlx`,
retry 3x (2s, 5s, 10s), Redis dedup idempotency (TTL 7 days).
