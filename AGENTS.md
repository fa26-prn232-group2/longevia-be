# Agent Rules & Navigation

Project: **Longevia Backend** — ASP.NET Core microservices (.NET 10), PostgreSQL
(EF Core Code First). Clients enter through Nginx (HTTP/HTTPS); services call each
other synchronously over gRPC; asynchronous events go through RabbitMQ.

## 1. Startup & Navigation

- Read this entire `AGENTS.md` before starting.
- Read only the documents relevant to the task. Do not scan all of `docs/`.
- Choose documents by task type:
    - Architecture: `docs/system-design/`
    - Development process: `docs/development/`
- **Skills are loadable workflows, not documentation**:
    - Canonical skills live in `.agents/skills/<name>/SKILL.md`. OpenCode, Codex,
      Cursor and Gemini CLI read them directly; **do not create copies** under
      `.opencode/skills`.
    - `.opencode/agents/*.md` is only a permission adapter for OpenCode; it must
      not contain a second workflow.
    - To run a whole feature end to end, use `speckit-orchestrate`. It drives
      analyze → implement → converge and owns git, gates and verification.
    - Individual `speckit-*` skills remain usable for one step at a time.
    - Invoke them with the host agent's syntax: `$name` (Codex), `/name`
      (OpenCode/Claude), or the skill tool. Skill names are the common standard,
      independent of syntax.
- Before each Spec-kit run, read `docs/development/agent-workflow.md` and
  `docs/development/team-workflow.md`. The repository is shared by the whole
  team: a feature is `specs/<owner>-<NNN>-<slug>` on branch `feat/<slug>`, and
  every skill must bind the feature with
  `pwsh scripts/speckit/team-feature.ps1 resolve -Json` before writing files.
- Before writing or editing code, read the `docs/system-design/` and
  `docs/development/` documents relevant to the task. `docs/system-design/` is
  the authoritative source for implementation and must be kept accurate.

### Authoritative sources

- Business and product requirements: `docs/requirements/` (BRD, PRD).
- Architecture decisions (ADR): `docs/decisions/`.
- Machine-readable contracts (REST/gRPC/event): `contracts/` and
  `docs/api/api-spec.md`.
- Source code and tests are implementation evidence; when they contradict an
  approved contract or document, STOP and report the affected scope.
- Priority order for conflicts:
  Constitution > `docs/system-design/` > `docs/development/` >
  `docs/requirements/` + `docs/decisions/` > `docs/api/` + `contracts/` >
  `specs/` > source.

## 2. Workflow Compliance

- **MANDATORY** — Follow
  [agent-workflow.md](docs/development/agent-workflow.md) in full:
    - **Large task / new feature**: follow the Spec-kit process step by step.
    - **Small task** (simple bug fix, doc or config change): Spec-kit is optional,
      but the core principles in §4 still apply.
    - **Propose and do the next step yourself** instead of handing work back.
      Stop only at the stop conditions the running workflow defines.
- **Run speckit only when asked**: if the developer does not invoke `speckit-*`,
  you do not need to run the speckit workflow, but you must still follow
  `docs/development/` and `docs/system-design/`.
- **MANDATORY** — Git:
    - **Do not touch git on your own**: the agent MUST NOT run any
      state-changing git command (`commit`, `push`, `tag`, `branch`, `checkout`,
      `merge`, `rebase`, `reset`, `amend`, history rewriting) without an explicit
      request or permission from the developer. Read-only commands
      (`git status/log/diff`) are allowed. This applies even when the developer
      has approved a plan that mentions commits — every git operation still
      needs its own instruction. EXCEPTION: invoking a skill that declares its
      own git policy (e.g. `speckit-orchestrate`) grants standing permission for
      that run; that skill's policy governs, including commit cadence and what
      must be asked first.
    - Commit messages in English, following Conventional Commits.
    - **Do not commit/push to `main` by default**: prefer a feature branch + PR
      with at least 1 reviewer. The agent MUST NOT **automatically** `push`,
      `tag`, create a PR, force-push or rebase someone else's commits; do these
      only when the developer requests or explicitly permits it for that run —
      once permitted, committing/pushing directly to `main` is allowed.
    - Stage only paths belonging to the bound feature. No `git add -A`, and do
      not stage pre-existing changes made by someone else.
    - **Gates scale with change size.** The table below is the single source of
      truth for gates; do not repeat it in other documents.

        | Change | Gate |
        | --- | --- |
        | Small change (typo, doc, config, one task) | `make lint` + tests for the project touched |
        | One feature phase | `make lint` + `make test PROJECTS=<path>` scoped to the projects touched |
        | Before `push` / `tag` | full `make check` |

        `make lint` = format-check + build (warnings as errors) — the **fast
        gate**. `make check` = format-check, Release build, full test suite and
        package vulnerability scan — the **close-out gate**. Neither belongs in
        the loop for every typo.

## 3. Code Compliance

- **MANDATORY** — Follow the documents in:
    - `docs/development/` — process, conventions, handler/validation/routing
      standards, testing, migrations.
    - `docs/system-design/` — overall architecture, design patterns,
      event-driven design, module boundaries.
- When writing code, you **must** read and check the documents relevant to the
  task first.
- **Service boundaries** (per `docs/system-design/diagrams/png/c4-model-lv2.png`):
    - Each service owns its own database; never access another service's DB.
      **Only exception**: Recommendation owns `recommendation_db`, which contains
      only the embedded knowledge corpus (RAG), **not** user data
      (Constitution §I, `docs/decisions/002-recommendation-knowledge-base.md`).
    - **Client-facing (through Nginx)**: Identity, Diet, Progress. Recommendation
      is an **internal, gRPC-only service**; it exposes no REST and is not routed
      through Nginx.
    - Synchronous **inter-service calls go over gRPC**; external clients enter
      through Nginx (HTTP/HTTPS). Do not use direct HTTP between two services,
      and do not call across modules via internal imports.
    - Asynchronous communication goes through RabbitMQ; publish and consume via
      contracts.
    - Schema is managed by EF Core **Code First** (migrations); never change the
      DB by hand.
- **MANDATORY** — Environment files: every comment in a file whose name starts
  with `.env` (e.g. `.env.example`) **must be written in English**.
- **MANDATORY** — Team coordination (details in
  `docs/development/team-workflow.md`):
    - Identity is **machine-local**: each developer has `.specify/owner.json`
      (gitignored, containing only `{"handle": "..."}`). If the file is
      missing/empty, the agent stops, asks the developer for a handle and writes
      it; MUST NOT guess a handle.
    - 1 feature = 1 owner = 1 branch. Two people must not edit the same feature's
      `tasks.md`.
    - Only **one** open feature may create migrations for a service; two features
      must not edit the same file under `contracts/` at the same time.
    - Shared files (`contracts/`, `Directory.Packages.props`, the solution, DI
      registration, migration projects) must be listed in `plan.md` under
      "Shared Files & Conflict Hotspots" before editing.
    - `.agents/skills/**` and `.specify/**` are shared infrastructure owned by
      the lead; feature branches MUST NOT change them (exception:
      `.specify/owner.json` is machine-local state, filled by each developer and
      never committed). Infrastructure changes go through `chore/skills-*`.
    - `meta.json` is the per-feature source of truth: update `services` and
      `status` there. View the aggregate with
      `pwsh scripts/speckit/team-feature.ps1 registry` (printed, not committed).
- If a feature changes an HTTP endpoint, event payload or client-facing DTO, it
  must update `specs/<owner>-<NNN>-<slug>/frontend-guide.md`; set
  `READY_FOR_FRONTEND` only when contract, implementation, test evidence and the
  counted sync check are all complete. A feature that does not change the client
  surface must record that conclusion in `tasks.md`.
- **MANDATORY — Observability & Metrics (Prometheus & Grafana)**:
  - Mọi microservice (Identity, Diet, Progress, Recommendation, Worker) khi khởi tạo MUST tích hợp endpoint metrics để Prometheus thu thập và hiển thị lên Grafana (Dashboard cổng `3001`).
  - Sử dụng extension chuẩn từ `Longevia.Shared` (2 dòng lệnh trong `Program.cs`):
    1. Đăng ký OpenTelemetry: `builder.Services.AddObservability("<ServiceName>");`
    2. Expose endpoint `/metrics`: `app.MapPrometheusScrapingEndpoint();`
  - Thêm scrape job của service vào `deployments/prometheus/prometheus.yml` (Prometheus dùng `static_configs`); thiếu bước này service không xuất hiện trong Targets dù đã có 2 dòng trên.
  - Tuyệt đối không tự viết logic thu thập metric riêng lẻ hay hardcode cổng Prometheus/Grafana trong mã nguồn service.
- If documents conflict, use the priority order in §1 (Authoritative sources).

## 4. Core Principles

- **Follow the Constitution (MANDATORY)**: for **every action** — whether or not
  it follows the Spec-kit process — read and comply with
  [`.specify/memory/constitution.md`](.specify/memory/constitution.md). It is the
  project's highest architectural authority. When the constitution conflicts
  with another document, the constitution wins at architectural boundaries and
  principles.
- **Resolve conflicts**: if documents and source code disagree about
  architecture, data model or user-visible behaviour, STOP, explain, and ask
  which one is authoritative. For details the documents leave open, decide, then
  record the decision and reason in the report.
- **Makefile**: always prefer a Makefile target over a manual command when an
  equivalent target exists.
- **Communication language**: talk to the developer in Vietnamese.
- **Ask with the question UI, not in prose**: when the developer must decide,
  use the `question` tool with concrete options, each with a short label and a
  consequence description, marking the preferred option with "(Recommended)".
  Use prose only for open questions where choices would lose information.
- **Documents under `specs/`**: written in Vietnamese per agent-workflow.md
  §IV.7.
- **Relative paths**: always use relative paths when referencing files in docs,
  specs and code; avoid absolute paths such as `file:///C:/...`.
