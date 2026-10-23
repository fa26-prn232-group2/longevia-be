# Event Contracts v1 — Longevia Backend

Canonical index of versioned async event schemas. Implementation source of truth:
C# records in `src/Shared/Events/` (each carries `SchemaVersion = 1`).

## Events

| Event | Routing key | Record | Producer | Consumer |
| --- | --- | --- | --- | --- |
| `UserRegisteredEvent` | `identity.user.registered` | `src/Shared/Events/UserRegisteredEvent.cs` | Identity (via transactional outbox — obligation recorded in `specs/mavil-001-infra-observability/data-model.md` §1.1, implementation owned by Identity feature) | Worker `worker.email` queue → verification email |
| `PasswordResetRequestedEvent` | `identity.user.password-reset-requested` | `src/Shared/Events/PasswordResetRequestedEvent.cs` | Identity (via transactional outbox) | Worker `worker.email` queue → reset email |

## Versioning policy

- Additive optional field: backward compatible, version stays `1`. Consumers MUST ignore unknown fields.
- Rename/remove field or semantic change: new version + migration plan. Old version stays consumable until all consumers migrate.

## Topology

See `specs/mavil-001-infra-observability/data-model.md` §1.3: topic exchange
`longevia.events`, durable queue `worker.email`, DLX/DLQ `worker.email.dlx`,
retry 3x (2s, 5s, 10s), Redis dedup idempotency (TTL 7 days).
