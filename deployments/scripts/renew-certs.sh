#!/bin/sh
# Renew the production certificate (cron entry point).
set -eu

exec sh "$(dirname "$0")/issue-certs.sh" renew
