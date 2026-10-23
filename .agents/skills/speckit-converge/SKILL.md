---
name: "speckit-converge"
description: "Assess the current codebase against the feature's spec, plan, and tasks, then append any remaining unbuilt work as new tasks to tasks.md so implement can complete it."
compatibility: "Requires spec-kit project structure with .specify/ directory"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/converge.md"
---


## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

**Worker Git guard**: When this skill is invoked by `speckit-worker`, do not
execute an extension hook that performs any Git mutation. Report that hook to
the orchestrator for execution under the orchestrator's Git policy. Continue
to execute non-Git hooks normally.

## Feature Binding (team)

This repository is worked on by a team, so a feature is identified by the branch
`feat/<slug>` and the directory `specs/<owner>-<NNN>-<slug>`. There is no
repository-wide counter.

1. Run `pwsh scripts/speckit/team-feature.ps1 resolve -Json` from the repo root
   and use the returned `FEATURE_DIR`, `FEATURE_SPEC`, `IMPL_PLAN`, `TASKS`,
   `OWNER`, `SLUG` and `BRANCH` for the rest of this skill.
2. If it fails, STOP and report the error verbatim. Never fall back to the newest
   directory under `specs/`, and never edit `.specify/feature.json` by hand. The
   usual fixes are: check out `feat/<slug>`, or run
   `pwsh scripts/speckit/team-feature.ps1 bind`.
3. Never work when the current branch is `main` or when the bound branch differs
   from the current branch.
4. If the developer names a feature different from the bound one, STOP: one
   checkout handles one feature at a time.

## Pre-Execution Checks

**Check for extension hooks (before convergence)**:

- Check if `.specify/extensions.yml` exists in the project root.
- If it exists, read it and look for entries under the `hooks.before_converge` key
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- When constructing command invocations from hook command names, replace dots (`.`) with hyphens (`-`). For example, `speckit.git.commit` → `$speckit-git-commit`.
- For each executable hook, output the following based on its `optional` flag:
  - **Optional hook** (`optional: true`):

    ```text
    ## Extension Hooks

    **Optional Pre-Hook**: {extension}
    Command: `/{command}`
    Description: {description}

    Prompt: {prompt}
    To execute: `/{command}`
    ```

  - **Mandatory hook** (`optional: false`):

    ```text
    ## Extension Hooks

    **Automatic Pre-Hook**: {extension}
    Executing: `/{command}`
    EXECUTE_COMMAND: {command}

    Wait for the result of the hook command before proceeding to the Goal.
    ```
    After emitting the block above you MUST actually invoke the hook and wait for it to finish before continuing. Run it the same way you would run the command yourself in this agent/session (the invocation may differ from the literal `{command}` id shown above, e.g. a skills-mode agent runs it as `/skill:speckit-...` or `$speckit-...`). Emitting the block alone does not run the hook.

- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently

## Goal

Close the gap between approved intent and what the codebase currently implements.
Use the source hierarchy from `AGENTS.md`: relevant requirements in
`docs/requirements/` and architecture decisions in `docs/decisions/`,
machine-readable contracts in repository `contracts/`, then
feature `spec.md`, `plan.md`, and `tasks.md` as the implementation breakdown,
with the constitution as governing constraints. If the authoritative
requirement source is unavailable, report that limitation and do not claim full
convergence. Determine which requirements, acceptance criteria, contract obligations, plan decisions, and
existing tasks are unmet, incomplete, or only partially satisfied, and **append each piece
of remaining work as a new, traceable task** at the bottom of `tasks.md` so that
`$speckit-implement` can complete it. This command MUST run only after
`$speckit-implement` has run on the current `tasks.md`, and after `$speckit-tasks` has produced a complete `tasks.md`.

This is **not** a diff tool and does **not** track changes. It assesses the present state
of the code relative to the feature's artifacts — no git, no branch comparison, no history.

## Operating Constraints

**BOUNDED WRITES**: The command may only:

- append a new `## Phase N: Convergence` section to `tasks.md` for unfinished
  work that belongs to this feature;
- create or append `deferred.md` for a finding that is genuinely outside the
  feature; and
- synchronize existing required `frontend-guide.md` and
  `backend-integration-guide.md` against canonical contracts/source during
  close-out.

It MUST NOT:

- modify `spec.md` or `plan.md` in any way;
- rewrite, renumber, reorder, or delete any existing task (including tasks from a prior
  Convergence phase);
- modify, create, or delete any application code — completing the appended tasks is the
  job of `$speckit-implement`.
- move in-scope work to deferred.md merely to reach a clean result.

When the codebase already satisfies everything, the command MUST leave `tasks.md`
**byte-for-byte unchanged** (no empty Convergence header) and report a clean result.

**Constitution Authority**: The project constitution (`.specify/memory/constitution.md`) is
**non-negotiable**. Code that violates a MUST principle is the highest-severity finding and
produces a corresponding remediation task. If the constitution is an unfilled template,
skip constitution checks gracefully rather than failing.

## Execution Steps

### 1. Initialize Convergence Context

Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks` once from repo root and parse JSON for FEATURE_DIR and AVAILABLE_DOCS. Derive absolute paths:

- SPEC = FEATURE_DIR/spec.md
- PLAN = FEATURE_DIR/plan.md
- TASKS = FEATURE_DIR/tasks.md
- CONSTITUTION = `.specify/memory/constitution.md` (if present)
Resolve and read the requirement/workflow/ADR links named by the feature
artifacts and the relevant canonical files under repository `contracts/`. A
conflict between feature artifacts and an authoritative source is not a code
gap: stop and report the artifact conflict for correction.
If `spec.md`, `plan.md`, or `tasks.md` is missing, STOP with a clear, actionable message naming the
prerequisite command to run (`$speckit-specify` for a missing spec, `$speckit-plan` for a missing plan,
`$speckit-tasks` for missing tasks). Do not produce partial output.
For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

### 2. Load Artifacts (Progressive Disclosure)

Load only the minimal necessary context from each artifact:

**From spec.md:**

- Functional Requirements (FR-###)
- Success Criteria (SC-###) — include only items requiring buildable work; exclude
  post-launch outcome metrics and business KPIs
- User Stories and their Acceptance Scenarios
- Edge Cases (if present)

**From plan.md:**

- Architecture/stack choices and technical decisions
- Data Model references
- Phases and named touch-points (files/components the plan says will be created or edited)
- Technical constraints

**From tasks.md:**

- Task IDs (to compute the next ID and next phase number)
- Descriptions, phase grouping, and referenced file paths

**From constitution (if not an unfilled template):**

- Principle names and MUST/SHOULD normative statements

**From authoritative requirements and contracts:**

- Approved business rules, workflow states, acceptance obligations, API/event/
  gRPC schemas, compatibility requirements, and unresolved decisions explicitly
  marked by those sources

### 3. Build the Intent Inventory

Create an internal model (do not echo raw artifacts):

- **Requirements inventory**: one stable key per FR-### / SC-### / user-story acceptance
  scenario (e.g. `US1/AC2`), plus the plan decisions and constitution principles that
  impose buildable obligations.
- **Code-scope map**: from the file paths named in `plan.md` and `tasks.md`, plus a keyword
  search for the concepts each requirement describes, derive the set of source files and
  components in scope for assessment. Bound the assessment to these — do **not** infer
  scope beyond what the artifacts define.
- **Integration-surface inventory**: determine whether the feature adds,
  changes, or removes HTTP endpoints, event payloads, or client-facing DTOs
  (frontend surface), and gRPC methods/messages, event contracts/topology, or
  other service-to-service contracts (backend integration surface); locate
  frontend-guide.md or backend-integration-guide.md when required.

### 4. Assess the Codebase and Classify Findings

For each item in the intent inventory, inspect the current code in scope and produce a
`Finding` only where there is a gap. Classify every finding by **gap type**:

- **`missing`**: the required work is absent from the code entirely.
- **`partial`**: the work exists but does not yet fully satisfy the requirement /
  acceptance criterion / plan decision.
- **`contradicts`**: the code does something that conflicts with stated intent or a
  constitution MUST principle.
- **`unrequested`**: the code contains work not called for by the spec, plan, or tasks
  (surfaced for awareness — converge does **not** delete code, it only appends a task to
  review/justify or remove it).

Each `Finding` records: a stable id, the `source-ref` it traces to, the `gap-type`, a
severity, and a short human-readable description with the evidence (the file/area observed).

Before treating a finding as actionable work for this feature, classify its
scope. A finding owned by another feature, shared test infrastructure, or a
third-party constraint is not appended to tasks.md. Record it in deferred.md
with the concrete issue, scope boundary, unblocking condition, and affected
checked task IDs. If it is required to satisfy this feature's spec/plan, it is
in scope regardless of cost or difficulty.
If it invalidates acceptance evidence for a checked task, reopen that task and
treat the finding as in-scope/blocking; it cannot remain only in deferred.md.

**Edge cases:**

- **Little or no code yet**: treat the entire specified scope as `missing` remaining work
  rather than failing.
- **Nothing remains**: produce zero findings and follow the converged branch in Step 7.

### 5. Assign Severity

- **CRITICAL**: violates a constitution MUST principle, or a `missing`/`contradicts` gap
  that blocks baseline functionality of a P1 user story.
- **HIGH**: a `missing` or `partial` gap on a core functional requirement or acceptance
  criterion.
- **MEDIUM**: a `partial` gap on a secondary requirement, or an `unrequested` addition with
  unclear justification.
- **LOW**: minor partial gaps, polish, or low-risk `unrequested` additions.

### 6. Present the In-Session Findings Summary

Before appending anything, output a compact, severity-graded summary (no file writes yet):

## Convergence Findings

| ID | Gap Type | Severity | Source | Evidence | Remaining Work |
|----|----------|----------|--------|----------|----------------|
| F1 | missing  | HIGH     | FR-008 | Example: no append-only guard detected in path/to/module.py when writing tasks.md | Add append-only enforcement |

**Summary metrics:**

- Requirements / acceptance criteria checked
- Plan decisions checked
- Constitution principles checked (or "skipped — template")
- Findings by gap type (missing / partial / contradicts / unrequested)
- Findings by severity
- Findings routed to deferred.md and why they are outside scope
- Frontend and backend integration handoff requirements and current guide status

### 7. Append Convergence Tasks (or report converged)

**If there are one or more actionable findings** (`tasks_appended` outcome):

Append to the **end** of `tasks.md`, per the append contract:

1. Scan all existing task IDs; let `M` be the maximum. Determine the next phase number `N`
   (highest existing phase + 1).
2. Write a single new section header `## Phase N: Convergence`.
3. Emit one checklist item per actionable finding, ordered CRITICAL/HIGH first, assigning
   zero-padded IDs `T{M+1:03d}, T{M+2:03d}, …`:

   ```markdown
   - [ ] T042 <imperative description> per <source-ref> (<gap-type>)
   ```

   `<source-ref>` traces the task to its origin: e.g. `FR-003`, `SC-002`,
   `US1/AC2`, `plan: storage decision`, `Constitution II`.

   `<gap-type>` is one of `missing`, `partial`, `contradicts`, `unrequested`.

   Constitution-violation tasks MUST be emitted first and described as
   `CRITICAL`.
4. Never reuse or renumber existing IDs. If a prior Convergence phase exists, add a new,
   separately-numbered one below it — do not touch the old one.
5. If client-facing change is required but frontend-guide.md is missing, append
   a convergence task to create it from
   `.specify/templates/frontend-guide-template.md`.
6. If backend integration change is required but
   backend-integration-guide.md is missing, append a convergence task to create
   it from `.specify/templates/backend-integration-guide-template.md`.

**If there are no actionable findings** (`converged` outcome):

- Do **not** modify `tasks.md` at all — no empty phase header.
- Report: **"✅ Converged — the implementation satisfies the spec, plan, and tasks."**
- Include the summary counts of what was checked.

### 7a. Synchronize the integration guides

When the feature changes the client surface and frontend-guide.md exists:

1. Compare routes with OpenAPI and router/handler code after normalizing path params.
2. Compare request/response fields with canonical schemas/generated types.
3. Compare every documented enum in both directions with its canonical definition.
4. Compare success and error statuses with handler behavior and Problem Details contracts.
5. Record numeric counts and test evidence in the guide.
6. Set `READY_FOR_FRONTEND` only when all checks are complete; otherwise retain
   the truthful non-ready status and report the missing evidence.
   A previously ready surface follows `READY_FOR_FRONTEND` → `CHANGED` while
   revised, then `CHANGED` → `READY_FOR_FRONTEND` after the counted sync and
   evidence complete; no frontend acknowledgement is required.

When the feature changes the backend integration surface and
backend-integration-guide.md exists:

1. Compare every RPC and message field with the canonical protobuf, including
   field numbers and enum values in both directions.
2. Compare error codes/trailers and deadlines with server implementation and
   configuration.
3. Compare every event, exchange, queue, routing key, and producer/consumer
   pair with the canonical event contract/topology.
4. Compare retry, idempotency, outbox, DLQ, and compatibility claims with code.
5. Record numeric counts and test evidence in the guide.
6. Set `READY_FOR_BACKEND` only when all checks are complete; otherwise retain
   the truthful non-ready status and report the missing evidence.
   A previously ready surface follows `READY_FOR_BACKEND` → `CHANGED` while
   revised, then `CHANGED` → `READY_FOR_BACKEND` after the counted sync and
   evidence complete.

When a surface does not change, confirm tasks.md says so and do not create the
corresponding guide.

### 8. Provide Next Actions (Handoff)

- On `tasks_appended`: state how many tasks were appended under which phase, and recommend
  running `$speckit-implement` to complete them; note that a follow-up converge
  run will find fewer or no remaining items.
- On `converged`: recommend proceeding to review / opening a PR. No further implement pass
  is needed for this feature's specified scope.
- In both outcomes: list deferred entries created or observed, and report the
  frontend/backend guide paths and statuses or the explicit no-change decisions.

### 9. Check for extension hooks

After producing the result, check if `.specify/extensions.yml` exists in the project root.

- If it exists, read it and look for entries under the `hooks.after_converge` key
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- Report the convergence outcome (`converged` or `tasks_appended`) in-session before listing
  any hooks, so users can decide whether to run optional follow-up commands.
- When constructing command invocations from hook command names, replace dots (`.`) with hyphens (`-`). For example, `speckit.git.commit` → `$speckit-git-commit`.
- For each executable hook, output the following based on its `optional` flag:
  - **Optional hook** (`optional: true`):

    ```text
    ## Extension Hooks

    **Optional Hook**: {extension}
    Command: `/{command}`
    Description: {description}

    Prompt: {prompt}
    To execute: `/{command}`
    ```

  - **Mandatory hook** (`optional: false`):

    ```text
    ## Extension Hooks

    **Automatic Hook**: {extension}
    Executing: `/{command}`
    EXECUTE_COMMAND: {command}
    ```
    After emitting the block above you MUST actually invoke the hook and wait for it to finish before continuing. Run it the same way you would run the command yourself in this agent/session (the invocation may differ from the literal `{command}` id shown above, e.g. a skills-mode agent runs it as `/skill:speckit-...` or `$speckit-...`). Emitting the block alone does not run the hook.

- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently
