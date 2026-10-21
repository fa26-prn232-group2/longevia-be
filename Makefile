# Longevia Backend - Make targets wrap the .NET CLI.
# The gate ladder these targets form is the single source of truth in AGENTS.md §2.
#
#   make lint    fast gate   -> format-check + build (warnings as errors)
#   make check   close-out   -> format-check + build Release + full test + vulnerable scan
#
# GNU Make is required. If `make` is not installed, run the underlying
# `dotnet` commands directly.

DOTNET ?= dotnet
SLN := $(firstword $(wildcard *.slnx *.sln))
SLN_OR_DIR := $(if $(SLN),$(SLN),.)
PROJECTS ?=
TEST_SCOPE := $(if $(PROJECTS),$(PROJECTS),$(SLN_OR_DIR))

.PHONY: help restore format format-check build build-release lint test test-all vuln check ef-add ef-update jwt-keys clean \
	book-ocr-build book-ocr-info book-ocr-render book-ocr-vision book-ocr-ocr book-ocr-assemble book-ocr-qa \
	book-ocr-coverage book-ocr-night book-ocr-resume book-ocr-logs book-ocr-status book-ocr-clean

# Book OCR toolchain (scripts/book-ocr). Docker-only, writes to artifacts/book-ocr.
# Never part of the .NET gate.
DOCKER ?= docker
BOOK_OCR := $(DOCKER) compose -f scripts/book-ocr/compose.yml
PAGES ?=
BOOK_OCR_PAGES := $(if $(PAGES),--pages $(PAGES),)
WORKERS ?=
BATCH ?=
BOOK_OCR_VISION_ARGS := $(if $(WORKERS),--workers $(WORKERS),) $(if $(BATCH),--batch $(BATCH),)
BOOK_OCR_CONTAINER := book-ocr-night

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
	@echo   ef-add           NAME=migration PROJECT=path
	@echo   ef-update        PROJECT=path
	@echo   jwt-keys         print a local ES256 JWT keypair, scope with KID=...
	@echo   clean            remove build output
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
