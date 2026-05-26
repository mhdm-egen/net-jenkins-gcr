#!/usr/bin/env bash
# Provision Nexus cleanup policies and attach them to the docker/nuget hosted
# repositories. Idempotent - safe to re-run.
#
# Policies created:
#   docker-ci-cleanup        : prune docker components not updated in N days.
#                              Active floating tags (:latest, :v1) survive
#                              because every build re-pushes them, refreshing
#                              their last-blob-updated timestamp.
#   nuget-prerelease-cleanup : prune *prerelease* NuGet packages older than N
#                              days. Release versions are preserved.
#
# Requires: bash, curl, jq.
# Required env: NEXUS_PASS
# Optional env (defaults shown):
#   NEXUS_URL=http://nexus:8081
#   NEXUS_USER=admin
#   DOCKER_REPO=docker-hosted
#   NUGET_REPO=nuget-hosted
#   DOCKER_POLICY_NAME=docker-ci-cleanup
#   NUGET_POLICY_NAME=nuget-prerelease-cleanup
#   RETENTION_DAYS=30
#
# Usage:
#   NEXUS_PASS='...' bash nexus/wire-cleanup-policies.sh
#   NEXUS_PASS='...' NEXUS_URL=http://localhost:8081 DOCKER_REPO=docker-internal \
#       bash nexus/wire-cleanup-policies.sh
#
# Note: cleanup policies do not delete components on their own. They are
# evaluated by the built-in "Admin - Cleanup repositories" scheduled task,
# which runs daily by default. Confirm it is enabled at
# System > Tasks in the Nexus UI.

set -euo pipefail

NEXUS_URL="${NEXUS_URL:-http://nexus:8081}"
NEXUS_USER="${NEXUS_USER:-admin}"
NEXUS_PASS="${NEXUS_PASS:?NEXUS_PASS env var is required}"
DOCKER_REPO="${DOCKER_REPO:-docker-hosted}"
NUGET_REPO="${NUGET_REPO:-nuget-hosted}"
DOCKER_POLICY_NAME="${DOCKER_POLICY_NAME:-docker-ci-cleanup}"
NUGET_POLICY_NAME="${NUGET_POLICY_NAME:-nuget-prerelease-cleanup}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

API="$NEXUS_URL/service/rest/v1"
TMP_BODY="$(mktemp)"
trap 'rm -f "$TMP_BODY"' EXIT

log() { printf '\n=== %s\n' "$*"; }

# nx METHOD PATH [JSON_BODY]
# Performs an authenticated Nexus REST call.
# Writes response body to $TMP_BODY, echoes the HTTP status code on stdout.
nx() {
    local method="$1" path="$2" body="${3:-}"
    if [ -n "$body" ]; then
        curl -sS -o "$TMP_BODY" -w '%{http_code}' \
            -u "$NEXUS_USER:$NEXUS_PASS" \
            -H 'Content-Type: application/json' \
            -H 'Accept: application/json' \
            -X "$method" \
            --data "$body" \
            "$API$path"
    else
        curl -sS -o "$TMP_BODY" -w '%{http_code}' \
            -u "$NEXUS_USER:$NEXUS_PASS" \
            -H 'Accept: application/json' \
            -X "$method" \
            "$API$path"
    fi
}

die() { echo "ERROR: $*" >&2; exit 1; }

upsert_policy() {
    local payload="$1" name code
    name=$(echo "$payload" | jq -r '.name')

    log "Upserting cleanup policy: $name"
    code=$(nx GET "/cleanup-policies/$name")
    if [ "$code" = "200" ]; then
        echo "  exists - updating"
        code=$(nx PUT "/cleanup-policies/$name" "$payload")
        case "$code" in
            200|204) echo "  ok ($code)" ;;
            *) die "PUT /cleanup-policies/$name returned $code: $(cat "$TMP_BODY")" ;;
        esac
    elif [ "$code" = "404" ]; then
        echo "  creating"
        code=$(nx POST "/cleanup-policies" "$payload")
        case "$code" in
            200|201|204) echo "  ok ($code)" ;;
            *) die "POST /cleanup-policies returned $code: $(cat "$TMP_BODY")" ;;
        esac
    else
        die "GET /cleanup-policies/$name returned $code: $(cat "$TMP_BODY")"
    fi
}

attach_to_repo() {
    local repo_kind="$1" repo_name="$2" policy_name="$3"
    log "Attaching '$policy_name' to $repo_kind hosted repo '$repo_name'"

    local code cfg new_cfg
    code=$(nx GET "/repositories/$repo_kind/hosted/$repo_name")
    [ "$code" = "200" ] || die "GET repo returned $code: $(cat "$TMP_BODY")"
    cfg=$(cat "$TMP_BODY")

    new_cfg=$(echo "$cfg" | jq --arg p "$policy_name" '
        .cleanup = (.cleanup // {})
        | .cleanup.policyNames = (((.cleanup.policyNames // []) + [$p]) | unique)
    ')

    local before after
    before=$(echo "$cfg"     | jq -c '.cleanup // {}')
    after=$(echo "$new_cfg"  | jq -c '.cleanup')
    if [ "$before" = "$after" ]; then
        echo "  already attached - no change"
        return
    fi

    code=$(nx PUT "/repositories/$repo_kind/hosted/$repo_name" "$new_cfg")
    case "$code" in
        200|204) echo "  attached ($code)" ;;
        *) die "PUT repo returned $code: $(cat "$TMP_BODY")" ;;
    esac
}

# Sanity-check Nexus is reachable and credentials work before touching anything.
log "Probing $NEXUS_URL"
code=$(nx GET "/status")
[ "$code" = "200" ] || die "Nexus probe failed ($code). Check NEXUS_URL/NEXUS_USER/NEXUS_PASS."

# --- Policy definitions ---
docker_policy=$(jq -n \
    --arg name "$DOCKER_POLICY_NAME" \
    --arg notes "Prune docker components not updated in $RETENTION_DAYS days. Active floating tags survive because every build re-pushes them." \
    --argjson days "$RETENTION_DAYS" \
    '{name: $name, notes: $notes, criteriaLastBlobUpdated: $days, format: "docker"}')

nuget_policy=$(jq -n \
    --arg name "$NUGET_POLICY_NAME" \
    --arg notes "Prune prerelease NuGet packages not updated in $RETENTION_DAYS days. Release versions are preserved." \
    --argjson days "$RETENTION_DAYS" \
    '{name: $name, notes: $notes, criteriaLastBlobUpdated: $days, criteriaReleaseType: "PRERELEASES", format: "nuget"}')

upsert_policy "$docker_policy"
upsert_policy "$nuget_policy"

attach_to_repo docker "$DOCKER_REPO" "$DOCKER_POLICY_NAME"
attach_to_repo nuget  "$NUGET_REPO"  "$NUGET_POLICY_NAME"

log "Done. Cleanup will run on the 'Admin - Cleanup repositories' schedule (daily by default)."
