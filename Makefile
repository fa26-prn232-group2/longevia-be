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

.PHONY: help restore format format-check build build-release lint test test-all vuln check ef-add ef-update jwt-keys clean

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
