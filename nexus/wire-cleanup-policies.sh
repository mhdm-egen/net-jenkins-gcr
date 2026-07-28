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
#   DOCKER_REPO=docker-private
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
DOCKER_REPO="${DOCKER_REPO:-docker-private}"
NUGET_REPO="${NUGET_REPO:-nuget-hosted}"
DOCKER_POLICY_NAME="${DOCKER_POLICY_NAME:-docker-ci-cleanup}"
NUGET_POLICY_NAME="${NUGET_POLICY_NAME:-nuget-prerelease-cleanup}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

API="$NEXUS_URL/service/rest/v1"
# Cleanup policies are NOT under /v1 on every Nexus. On 3.70.1 the only endpoint that exists is
# service/rest/internal/cleanup-policies (verified: /v1 and /beta both 404, /internal returns 200),
# while newer builds expose a public one. Probed below, after the credentials are known to work.
CLEANUP_API=""
TMP_BODY="$(mktemp)"
trap 'rm -f "$TMP_BODY"' EXIT

log() { printf '\n=== %s\n' "$*"; }

# nx METHOD PATH [JSON_BODY]
# Performs an authenticated Nexus REST call.
# Writes response body to $TMP_BODY, echoes the HTTP status code on stdout.
nx() {
    local method="$1" path="$2" body="${3:-}" base="${NX_BASE:-$API}"
    if [ -n "$body" ]; then
        curl -sS -o "$TMP_BODY" -w '%{http_code}' \
            -u "$NEXUS_USER:$NEXUS_PASS" \
            -H 'Content-Type: application/json' \
            -H 'Accept: application/json' \
            -X "$method" \
            --data "$body" \
            "$base$path"
    else
        curl -sS -o "$TMP_BODY" -w '%{http_code}' \
            -u "$NEXUS_USER:$NEXUS_PASS" \
            -H 'Accept: application/json' \
            -X "$method" \
            "$base$path"
    fi
}

# Same as nx, but against whichever base actually serves cleanup policies.
nxc() { NX_BASE="$CLEANUP_API" nx "$@"; }

die() { echo "ERROR: $*" >&2; exit 1; }

# Pick the cleanup-policy base this Nexus actually implements.
detect_cleanup_api() {
    local candidate
    for candidate in "$NEXUS_URL/service/rest/v1" "$NEXUS_URL/service/rest/internal"; do
        if [ "$(NX_BASE="$candidate" nx GET "/cleanup-policies")" = "200" ]; then
            CLEANUP_API="$candidate"
            log "Cleanup-policy API: $CLEANUP_API"
            return 0
        fi
    done
    die "no cleanup-policy endpoint found under /v1 or /internal on $NEXUS_URL (checked GET /cleanup-policies)"
}

upsert_policy() {
    local payload="$1" name code
    name=$(echo "$payload" | jq -r '.name')

    log "Upserting cleanup policy: $name"
    code=$(nxc GET "/cleanup-policies/$name")
    if [ "$code" = "200" ]; then
        echo "  exists - updating"
        code=$(nxc PUT "/cleanup-policies/$name" "$payload")
        case "$code" in
            200|204) echo "  ok ($code)" ;;
            *) die "PUT /cleanup-policies/$name returned $code: $(cat "$TMP_BODY")" ;;
        esac
    elif [ "$code" = "404" ]; then
        echo "  creating"
        code=$(nxc POST "/cleanup-policies" "$payload")
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

detect_cleanup_api

# --- Policy definitions ---
docker_policy=$(jq -n \
    --arg name "$DOCKER_POLICY_NAME" \
    --arg notes "Prune docker components not updated in $RETENTION_DAYS days. Active floating tags survive because every build re-pushes them." \
    --argjson days "$RETENTION_DAYS" \
    '{name: $name, notes: $notes, criteriaLastBlobUpdated: $days, format: "docker"}')

# Prereleases are selected by ASSET REGEX, not criteriaReleaseType. Nexus only offers the
# prerelease/release criterion (`isPrerelease`) for maven2 — asking for it on a nuget policy is
# rejected with a bare HTTP 400. Verified against
# GET /service/rest/internal/cleanup-policies/criteria/formats, which reports nuget as supporting
# exactly: regex, lastDownloaded, lastBlobUpdated.
#
# The regex matches a SemVer prerelease suffix on the version (a hyphen after the numeric core),
# e.g. Model.Weather.1.0.0-ci.3.gac66016.nupkg. A plain release such as Model.Weather.1.0.0.nupkg
# has no hyphen after the version and is therefore preserved.
NUGET_PRERELEASE_REGEX="${NUGET_PRERELEASE_REGEX:-.*[0-9]+\\.[0-9]+\\.[0-9]+-.*\\.nupkg}"

nuget_policy=$(jq -n \
    --arg name "$NUGET_POLICY_NAME" \
    --arg notes "Prune prerelease NuGet packages not updated in $RETENTION_DAYS days. Release versions are preserved (selected by asset regex; nuget has no prerelease criterion)." \
    --arg regex "$NUGET_PRERELEASE_REGEX" \
    --argjson days "$RETENTION_DAYS" \
    '{name: $name, notes: $notes, criteriaLastBlobUpdated: $days, criteriaAssetRegex: $regex, format: "nuget"}')

upsert_policy "$docker_policy"
upsert_policy "$nuget_policy"

attach_to_repo docker "$DOCKER_REPO" "$DOCKER_POLICY_NAME"
attach_to_repo nuget  "$NUGET_REPO"  "$NUGET_POLICY_NAME"

log "Done. Cleanup will run on the 'Admin - Cleanup repositories' schedule (daily by default)."
