# Longevia Backend - Make targets wrap the .NET CLI.
# The gate ladder these targets form is the single source of truth in AGENTS.md §2.
#
#   make lint    fast gate   -> format-check + build (warnings as errors)
#   make check   close-out   -> format-check + build Release + full test + vulnerable scan
#
# GNU Make is required. If `make` is not installed, run the underlying
# `dotnet` commands directly.

DOTNET ?= dotnet
PWSH ?= pwsh
SLN := $(firstword $(wildcard *.slnx *.sln))
SLN_OR_DIR := $(if $(SLN),$(SLN),.)
PROJECTS ?=
TEST_SCOPE := $(if $(PROJECTS),$(PROJECTS),$(SLN_OR_DIR))

.PHONY: help restore format format-check build build-release lint test test-all vuln check ef-add ef-update jwt-keys clean team-check \
	infra-up infra-down docker-up docker-down docker-stop docker-logs \
	book-ocr-build book-ocr-info book-ocr-render book-ocr-vision book-ocr-ocr book-ocr-assemble book-ocr-qa \
	book-ocr-coverage book-ocr-night book-ocr-resume book-ocr-logs book-ocr-status book-ocr-clean \
	kb-build kb-load kb-verify kb-eval kb-seed kb-ensure kb-smoke

# Docker Compose for the full stack (deployments/docker-compose.yml).
# Local == production: the same file is used in both.
DOCKER ?= docker
COMPOSE := $(DOCKER) compose -f deployments/docker-compose.yml
# Core infra only (explicit list, no profiles): later phases add app/monitoring
# services (identity/worker, prometheus/grafana) WITHOUT touching infra-up.
INFRA_SERVICES := postgres redis rabbitmq mailpit nginx swagger-ui
# Optional filter for docker-logs: make docker-logs SERVICE=identity
SERVICE ?=

# Book OCR toolchain (scripts/book-ocr). Docker-only, writes to artifacts/book-ocr.
# Never part of the .NET gate.
BOOK_OCR := $(DOCKER) compose -f scripts/book-ocr/compose.yml
PAGES ?=
BOOK_OCR_PAGES := $(if $(PAGES),--pages $(PAGES),)
WORKERS ?=
BATCH ?=
BOOK_OCR_VISION_ARGS := $(if $(WORKERS),--workers $(WORKERS),) $(if $(BATCH),--batch $(BATCH),)
BOOK_OCR_CONTAINER := book-ocr-night

# Book knowledge-base pipeline (scripts/book-kb). Docker-only, writes to
# artifacts/book-kb and loads the corpus into recommendation_db.
# Never part of the .NET gate.
BOOK_KB := $(DOCKER) compose --env-file .env -f scripts/book-kb/compose.yml

help:
	@echo Targets:
	@echo   restore          dotnet restore
	@echo   format           apply formatting
	@echo   format-check     verify formatting is clean
	@echo   build            build Debug, warnings as errors
	@echo   build-release    build Release, warnings as errors
	@echo   lint             format-check + build          [fast gate]
	@echo   test             dotnet test, scope with PROJECTS=path
	@echo   test-all         full test suite in Release
	@echo   vuln             scan packages for known vulnerabilities
	@echo   check            format-check + build-release + test-all + vuln   [close-out gate]
	@echo   team-check       validate specs/<owner>-<NNN>-<slug> layout (PWSH=powershell on 5.1)
	@echo   ef-add           NAME=migration PROJECT=path
	@echo   ef-update        PROJECT=path
	@echo   jwt-keys         print a local ES256 JWT keypair, scope with KID=...
	@echo   clean            remove build output
	@echo.
	@echo   infra-up         start core infra only (postgres redis rabbitmq mailpit nginx swagger-ui)
	@echo   infra-down       remove core infra containers, KEEP volumes (data preserved)
	@echo   docker-up        build + start the full stack
	@echo   docker-down      full cleanup: remove containers AND volumes
	@echo   docker-stop      stop all containers, keep data
	@echo   docker-logs      follow logs, filter with SERVICE=name
	@echo.
	@echo   book-ocr-build   build the book OCR image
	@echo   book-ocr-info    show input PDF metadata
	@echo   book-ocr-render  render pages, PAGES=1-8
	@echo   book-ocr-vision  Gemini vision for pages, PAGES=1-12
	@echo   book-ocr-ocr     Tesseract cross-check, PAGES=1-12
	@echo   book-ocr-assemble  build chapters in artifacts/book-ocr/out
	@echo   book-ocr-qa      compare Gemini with Tesseract
	@echo   book-ocr-coverage check every page is fully transcribed
	@echo   book-ocr-night   run the whole pipeline detached (overnight)
	@echo   book-ocr-resume  rerun the pipeline, skipping finished pages
	@echo   book-ocr-logs    follow the detached pipeline log
	@echo   book-ocr-status  print status.json
	@echo   book-ocr-clean   remove container, image and artifacts/book-ocr
	@echo.
	@echo   kb-build         build the book knowledge-base image
	@echo   kb-load          load the corpus into recommendation_db
	@echo   kb-verify        verify chapters, chunks and page ranges
	@echo   kb-eval          run the golden-set evaluation
	@echo   kb-seed          export the compressed seed under resources/knowledge
	@echo   kb-ensure        load the committed seed into recommendation_db (no API key)
	@echo   kb-smoke         prove an empty database is loaded by kb-init (isolated project)

restore:
	$(DOTNET) restore $(SLN_OR_DIR)

format:
	$(DOTNET) format $(SLN_OR_DIR)

format-check:
	$(DOTNET) format $(SLN_OR_DIR) --verify-no-changes

build:
	$(DOTNET) build $(SLN_OR_DIR) --no-restore -warnaserror

build-release:
	$(DOTNET) build $(SLN_OR_DIR) -c Release --no-restore -warnaserror

lint: format-check build

test:
	$(DOTNET) test $(TEST_SCOPE)

test-all:
	$(DOTNET) test $(SLN_OR_DIR) -c Release

vuln:
	$(DOTNET) list $(SLN_OR_DIR) package --vulnerable --include-transitive

check: format-check build-release test-all vuln

team-check:
	$(PWSH) -NoProfile -File scripts/speckit/team-feature.ps1 check

ef-add:
	$(DOTNET) ef migrations add $(NAME) --project $(PROJECT)

ef-update:
	$(DOTNET) ef database update --project $(PROJECT)

# Dev convenience only - prints the keypair to stdout and writes nothing to disk.
# Not part of any gate: CI never needs to sign a token.
KID ?= longevia-dev-01

jwt-keys:
	$(DOTNET) run --file scripts/jwt-keys.cs -- --kid "$(KID)"

clean:
	$(DOTNET) clean $(SLN_OR_DIR)

# Core infra only (SC-001: ready in <= 30s). Explicit service list so Phase 4/6
# additions (identity/worker, prometheus/grafana) never leak into infra-up.
infra-up:
	$(COMPOSE) up -d $(INFRA_SERVICES)

# Removes infra containers but KEEPS volumes (postgres/rabbitmq data survives).
# Full wipe (incl. volumes) is docker-down.
infra-down:
	-$(COMPOSE) rm -f -s $(INFRA_SERVICES)

# Full stack: builds app images (identity/worker once their Dockerfiles land in
# Phase 4) and starts everything, including monitoring (Phase 6).
docker-up:
	$(COMPOSE) up -d --build

# Full cleanup: containers AND volumes are removed. Postgres init scripts and
# Mailpit/Redis contents start from scratch on next up.
docker-down:
	$(COMPOSE) down -v

# Stops containers without removing them or their volumes.
docker-stop:
	$(COMPOSE) stop

# Follows logs; scope with SERVICE=name (e.g. make docker-logs SERVICE=mailpit).
docker-logs:
	$(COMPOSE) logs -f $(SERVICE)

book-ocr-build:
	$(BOOK_OCR) build

book-ocr-info:
	$(BOOK_OCR) run --rm book-ocr info

book-ocr-render:
	$(BOOK_OCR) run --rm book-ocr render $(BOOK_OCR_PAGES)

book-ocr-vision:
	$(BOOK_OCR) run --rm book-ocr vision $(BOOK_OCR_PAGES) $(BOOK_OCR_VISION_ARGS)

book-ocr-ocr:
	$(BOOK_OCR) run --rm book-ocr ocr $(BOOK_OCR_PAGES)

book-ocr-assemble:
	$(BOOK_OCR) run --rm book-ocr assemble $(BOOK_OCR_PAGES)

book-ocr-qa:
	$(BOOK_OCR) run --rm book-ocr qa $(BOOK_OCR_PAGES)

book-ocr-coverage:
	$(BOOK_OCR) run --rm book-ocr coverage $(BOOK_OCR_PAGES) $(if $(WORKERS),--workers $(WORKERS),) $(if $(BATCH),--batch $(BATCH),)

book-ocr-night:
	-$(DOCKER) rm -f $(BOOK_OCR_CONTAINER)
	$(BOOK_OCR) run -d --name $(BOOK_OCR_CONTAINER) book-ocr night $(BOOK_OCR_PAGES) $(BOOK_OCR_VISION_ARGS)
	@echo "Started $(BOOK_OCR_CONTAINER). Follow with: make book-ocr-logs"
	@echo "Progress: artifacts/book-ocr/status.json"

book-ocr-resume:
	-$(DOCKER) rm -f $(BOOK_OCR_CONTAINER)
	$(BOOK_OCR) run -d --name $(BOOK_OCR_CONTAINER) book-ocr night $(BOOK_OCR_PAGES) $(BOOK_OCR_VISION_ARGS)
	@echo "Resumed $(BOOK_OCR_CONTAINER). Finished pages are skipped."

book-ocr-logs:
	$(DOCKER) logs -f --tail 80 $(BOOK_OCR_CONTAINER)

book-ocr-status:
	powershell -NoProfile -Command "Get-Content 'artifacts/book-ocr/status.json' -Raw -Encoding UTF8"

book-ocr-clean:
	-$(DOCKER) rm -f $(BOOK_OCR_CONTAINER)
	$(BOOK_OCR) down --rmi local -v
	powershell -NoProfile -Command "if (Test-Path 'artifacts/book-ocr') { Remove-Item -Recurse -Force 'artifacts/book-ocr' }"

kb-build:
	$(BOOK_KB) build
	$(BOOK_KB) run --rm book-kb build

kb-load:
	$(BOOK_KB) run --rm book-kb load

kb-verify:
	$(BOOK_KB) run --rm book-kb verify

kb-eval:
	$(BOOK_KB) run --rm book-kb eval

kb-seed:
	$(BOOK_KB) run --rm book-kb seed

kb-ensure:
	$(DOCKER) compose --env-file .env -f deployments/docker-compose.yml run --rm kb-init

kb-smoke:
	$(PWSH) -NoProfile -File scripts/book-kb/smoke-seed.ps1
