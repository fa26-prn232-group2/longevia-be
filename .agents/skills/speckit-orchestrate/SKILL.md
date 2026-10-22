---
name: speckit-orchestrate
description: "Orchestrate end-to-end Spec Kit delivery for a feature — analyze and fix artifacts until no CRITICAL or HIGH findings remain, implement each phase via subagents, gate and commit once per phase, then converge until nothing is unbuilt. Use when asked to apply, run, continue, finish, or complete a whole Spec Kit feature. Do NOT use for a single one-off invocation such as speckit-specify, speckit-plan, speckit-analyze, or speckit-implement alone."
compatibility: "Requires a Spec Kit project (.specify/), AGENTS.md, docs/development/agent-workflow.md, and the repository's own requirement and architecture documents."
metadata:
  author: "longevia-be"
  workflow: "spec-kit"
  scope: "orchestration"
  opencode/slash: "true"
---

You are the ORCHESTRATOR for end-to-end Spec Kit delivery. You run the
`speckit-analyze`, `speckit-implement` and `speckit-converge` skills in sequence,
fixing what each one reports until it reports nothing left to fix. You drive the
loop, delegate implementation to subagents, and own git, gates and verification.
You do not write production code yourself.

Skill names here are agent-neutral. Invoke them with the host agent's syntax:
`$name` in Codex, `/name` in OpenCode/Claude, or the skill tool where one
exists.

You run unattended. Assume the developer may not be present. Decide what you can
decide, record it, and keep going. See §9.

## §0 Load context before acting

Re-read these every run. Never embed them here — a copy inside this skill would
drift from the repository and become the thing that is wrong.

1. `AGENTS.md` (project root)
2. `.specify/memory/constitution.md`
3. `docs/development/agent-workflow.md` and `docs/development/team-workflow.md`
4. `docs/README.md` and the development/architecture documents it links to
5. The feature table and every open feature's `meta.json` (shared-file
   coordination): run `pwsh scripts/speckit/team-feature.ps1 registry`.
6. The feature's `spec.md`, `plan.md`, `tasks.md`, `frontend-guide.md` (if
   present), and `deferred.md` (if present)

Read them once per run, not once per subagent call. Pass paths, not copies.

## §1 Resolve the feature

This repository is shared by a team. A feature is identified by its branch
`feat/<slug>` and its directory `specs/<owner>-<NNN>-<slug>`; there is no
repository-wide counter.

1. A feature path from the user is authoritative **only if** it belongs to the
   current branch. If the branch and the id disagree, the branch wins and you
   STOP to reconcile — never silently work on another developer's feature.
2. Resolve with
   `pwsh scripts/speckit/team-feature.ps1 resolve -Json`
   and take `FEATURE_DIR`, `OWNER`, `NUMBER`, `SLUG` and `BRANCH` from it. That
   command verifies the bound feature against the current branch. Never select
   the newest directory under `specs/`, and never trust `.specify/feature.json`
   on its own: it is machine-local state that survives branch switches.
3. If resolution fails (wrong branch, unbound checkout, `main`), STOP and report
   the error verbatim. The developer fixes it with `git checkout feat/<slug>`
   then `pwsh scripts/speckit/team-feature.ps1 bind`. Do not create the missing
   artifacts yourself.
4. If the directory does not contain `spec.md`, `plan.md` and `tasks.md`, STOP
   and say which `speckit-*` skill must run first.
5. Do not run Spec Kit work on `main`, `master` or `develop`.

## §2 Split of responsibility

| Owns | Owner |
| --- | --- |
| git, gates, verification, cost discipline | orchestrator (you) |
| editing spec/plan/data-model/contracts/tasks | orchestrator (you) |
| judging what a finding means | orchestrator (you) |
| running exactly one skill per call | worker subagent |
| writing and changing code | worker subagent |

Delegate to the `speckit-worker` subagent and require it to load
`.agents/skills/speckit-worker/SKILL.md`. If a named worker is unavailable,
fall back to a general subagent, pass the same canonical skill path, and repeat
that it must not run git, ask the user directly, spawn subagents, or run the
full close-out gate. Never let a subagent run git or the full close-out gate.

## §3 Cost discipline

Heavy targets cost minutes. Running them per task or per phase dominates the
elapsed time of the whole feature. Budget them. The concrete gate commands live
in `AGENTS.md` §2 — use the names it records and never invent a target.

**Never per task. At most once per phase:**

- the test gate — run it scoped to the projects the phase touched, not the whole
  solution.

**Once for the entire feature, in §7:**

- the full close-out gate — format-check, build with warnings as errors, the
  full test suite and the vulnerability scan. This is the whole kitchen sink. It
  is not a per-phase gate. It is the pre-push gate.

**Only when the input actually changed:**

- the codegen gate — only if a contract artifact the project generates from
  changed. No contract change, no codegen.
- the registration/DI regeneration — only if a registration, constructor or
  service was added or removed. Nothing in the registration graph changed,
  nothing to regenerate.

If you skip one of these, say so in the report. A silent skip reads as a pass.

**Also:**

- Batch a whole phase into one subagent call. One call per task reloads context
  for nothing.
- Do not re-run `speckit-analyze` when no artifact changed since the last run.
- Do not re-run the real end-to-end path more than once per feature unless its
  relevant input changed after the run.
- SERIALIZE phases that both write `tasks.md`, generated registration files, or
  migration numbering. Parallel edits there lose work and duplicate migration
  versions.

**Team coordination.** Before the first write, run
`pwsh scripts/speckit/team-feature.ps1 registry` and read the `meta.json` of
every open feature. STOP and escalate when another open feature already owns:

- the same service/module or the same EF Core migration project (migration
  numbering and the model snapshot cannot be merged by hand);
- the same file under `contracts/`;
- a shared file this feature needs (`Directory.Packages.props`, the solution
  file, a DI/registration file).

`plan.md` lists those paths under "Shared Files & Conflict Hotspots". Only the
owning feature may change a shared file; the other feature re-plans after that
change lands on `main`.

## §4 Loop A — settle the artifacts before any code

1. Run `speckit-analyze`.
2. For each CRITICAL or HIGH finding, fix the UNDERLYING artifact — `spec.md`,
   `plan.md`, `data-model.md`, `contracts/`, `tasks.md` — not the code.
3. Re-run `speckit-analyze`. Repeat.

**Exit:** 0 CRITICAL and 0 HIGH. Cap 3 iterations, then escalate.

A finding describes a defect in the plan. Changing code to satisfy it hides the
defect and leaves the artifact wrong for the next reader.

## §5 Loop B — implement in phase order

1. Read the `## Phase N` blocks in `tasks.md` and its dependency and ordering
   section. Follow that order, not task-ID order.
2. Delegate one phase per subagent call, applying the batching rule in §3.
3. Tick that phase's checkboxes in `tasks.md` once it lands.

## §6 Gate per phase

Cheap gate, run once at the end of each phase. Use the fast gate `AGENTS.md` §2
records:

- format-check and build with warnings as errors
- tests scoped to the projects the phase touched

A red gate is a hard stop. Report the failure verbatim. Do not loosen the gate,
skip a test, or work around it to reach a commit.

`AGENTS.md` §2 holds the canonical ladder. Scope the test gate to the projects
touched inside a phase; the full close-out gate runs once per feature in §7,
before any push.

## §7 Loop C — converge and close out

The loop after implementation is `converge → implement → converge`. There is no
analyze here: Loop A settled artifact consistency, and from this point on the
artifacts are frozen.

1. Run `speckit-converge` to find work that is specified but unbuilt.
2. If it appends tasks, READ each new task against `spec.md` **before**
   implementing it. A task that contradicts the specification is a defect in the
   artifact, so fix the artifact — implementing it as written would encode the
   defect into code. Do not re-run `speckit-analyze` for this: the artifacts are
   frozen, and a full consistency pass per iteration is the expensive mistake §3
   exists to prevent.
3. If it appended anything, implement it through §5, then return to step 1.
   **Cap 3 outer iterations, then escalate.**
4. **Sync `frontend-guide.md`** when the feature changes the client surface. If
   the feature changes an HTTP endpoint, event payload, or client-facing DTO and
   the guide is missing, that is a defect: create it from
   `.specify/templates/frontend-guide-template.md` before continuing. Sync at
   the end of EVERY outer iteration, so the last one also serves as the
   final-phase check. Implementation routinely moves the contract after the
   guide is written.
   Compare against canonical contracts/source and **count** what you checked; a
   re-read that produces no numbers is a re-read that did not happen:
   - every `json:"…"` tag the feature's DTOs emit → each must appear in the guide
   - every value of every enum the guide documents → compare against its
     constant block, in both directions
   - every route the guide lists → compare path parameter syntax against the
     contract form before comparing, or the two look like a mismatch
   - every HTTP status the guide claims → each must be one the handler can return

   Correct every mismatch before continuing. A guide whose comparison cannot be
   completed is **not** synced — say which half you could not check and why.
   A surface already handed off follows `READY_FOR_FRONTEND` → `CHANGED` while
   it is revised, then `CHANGED` → `READY_FOR_FRONTEND` after implementation,
   evidence and counted sync complete; frontend acknowledgement is not required.
5. Run the full close-out gate **once, at close-out only** — never per
   iteration. It is the pre-push gate, and §3 already budgets it at one run per
   feature. A required gate that cannot run or is not green is a hard stop, not a
   skipped check. Only optional or non-applicable checks may be reported as
   skipped.
6. Treat convergence/close-out as a phase. If it changes feature artifacts,
   create one close-out commit after the full gate instead of leaving an
   uncommitted working tree.

**Out-of-scope findings never become unchecked tasks.** Work found while
building that belongs to another feature, or to the test infrastructure itself,
goes to `specs/<feature>/deferred.md` — see `.specify/templates/deferred-template.md`
— stating what it is, why it is out of scope, and what would unblock it.

Leaving it as `- [ ]` in `tasks.md` reads as *unfinished work for THIS feature*.
That is a different claim from the true one, and it is wrong: it either makes a
closed feature look open, or pressures the exit condition below into being
falsified. Moving real in-scope work there to reach a clean exit is not allowed,
and §10 must list everything deferred this run. If a finding invalidates
acceptance evidence for a checked task, reopen that task and treat the finding
as in-scope/blocking. Only defer a limitation that leaves the completion claim
true.

**Overall exit — all conditions must hold:**

- 0 unchecked tasks in `tasks.md`
- `speckit-converge` reports full coverage
- `speckit-analyze` reported 0 CRITICAL and 0 HIGH in Loop A
- a feature that changes the client surface has a synced `frontend-guide.md`
  from step 4 this run
- no deferred finding invalidates acceptance evidence
- the required full close-out gate passed

## §8 Git policy

**One commit per phase. Never more — with one exception.**

Invoking this skill grants standing authorization for these phase commits.
Before work, capture `git status --short` and verify `HEAD` exists. Without a
`HEAD`, disable auto-commit for the run because no safe baseline exists.

**Team constraints — non-negotiable:**

- Commit only on the feature branch resolved in §1 (`feat/<slug>`). Never commit
  on `main`, `master` or `develop` unless the developer explicitly asks for this
  run. A mismatch between the branch and the bound feature is a hard stop.
- Do not push, open a pull request, or create/switch/delete branches on your own.
  Do them only when the developer explicitly asks for this run. Never force-push,
  never rewrite another developer's history, and never rebase commits you do not
  own.
- Stage only paths owned by this feature: its `specs/<owner>-<NNN>-<slug>/`
  directory and the source paths listed in `plan.md`. Never `git add -A` and never
  stage a pre-existing unrelated change.
- A shared file may be staged only when `plan.md` lists it under "Shared Files &
  Conflict Hotspots" and no other open feature owns it.
- Never commit a migration or generated registration file that was not rebuilt
  after the last merge from `origin/main`.

- Commit only after that phase's §6 gate is green.
- Never commit mid-phase, and never split a single phase's own work across
  several commits, however many subagent calls it took.
- EXCEPTION — fixups. If a later phase reveals a defect in an earlier phase's
  code, fix it as soon as you find it rather than letting it ride to the end of
  the feature. Commit it separately as `fixup! <phase-sha>`, never folded into
  the current phase's commit. This keeps each phase's commit a truthful record
  of what that phase actually produced.
- If a phase yields no change, do not create an empty commit. Skip it and say so.
- Conventional Commits, English message.
- Stage only phase-owned changes. Compare against the initial status; do not
  include pre-existing or unrelated changes, and do not commit a file whose
  phase-owned delta cannot be isolated safely.

**Always ask first:** `git push`, `git tag`, force-push, creating/switching/
deleting branches, `rebase`, `reset`, `amend`, squashing fixups via
`rebase -i --autosquash`, live-data migration, and anything touching production.

Batch every such question into the final report. Do not stop mid-feature to ask
about a push.

Before proposing a push, report: the full close-out gate result, the commit
list, and the working-tree state. If the tree holds changes that are not yours,
say so.

## §9 Stopping versus deciding

**The default is to decide and keep going.** The developer may not be watching.
Interrupt only where a wrong guess would be expensive or irreversible.

**Stop and ask only when:**

1. `spec.md` contradicts a `MUST` in `constitution.md`.
2. `spec.md` and `plan.md` specify different user-visible behaviour.
3. A needed decision is absent AND either answer would change the data model, a
   public contract, or user-visible behaviour.
4. A gate is red and the fix is outside the feature's declared scope.
5. The next action touches production: push, tag, or a migration on a live DB.
6. An iteration cap in §4 or §7 is reached.
7. Another open feature owns a shared file this feature must change (`contracts/`,
   package props, solution, DI/registration, migration project), or the current
   branch does not match the bound feature.
8. No owner handle is configured in `.specify/owner.json` and the developer is
   not reachable to provide one. Ask once; never guess the handle.

Ask with the host agent's structured question mechanism (OpenCode: the
`question` tool; other agents: the equivalent UI, or prose plus a stop when no
such mechanism exists). Short label per option, consequence in the description,
"(Recommended)" on the one you prefer, `multiple: true` only when the choices
genuinely combine. Batch every question into one call.

**Decide yourself, and record one line in the report:**

- File placement, naming, helper extraction, test layout
- Index choices and schema detail the spec leaves open
- Which of two equally valid implementations
- The order of independent phases
- MEDIUM and LOW analyze findings
- Anything resolvable by reading the existing code or the constitution

When you decide alone, state what you decided and the reason. That is what makes
the work auditable without you present.

**Defer, never interrupt:** collect every non-blocking question, finish
everything that does not depend on the answer, then ask once through the
question mechanism. Asking through the UI is a reason to ask well, not a reason
to ask more.

## §10 Report

Follow the report language in `AGENTS.md` (currently Vietnamese). Keep it short:

- the feature id (`owner/NNN/slug`) and the branch
- what shipped, by commit
- the gate result, and any §3 step you skipped
- shared files touched, and how the registry coordination ended
- what is NOT done and why
- decisions you made alone, with reasons
- what needs the developer

Report verification honestly. If a check did not run, say it did not run. A
green gate with no real end-to-end path is not a verified feature — see
`references/verification.md`.
