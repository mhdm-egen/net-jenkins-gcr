#!/usr/bin/env bash
#
# Setup script for Jenkins → Artifact Registry → Cloud Run pipeline.
#
# Creates two service accounts following least-privilege:
#   1. jenkins-deployer  - builds, pushes images, deploys Cloud Run
#   2. myapp-runtime     - identity the deployed container runs as
#
# Idempotent: safe to re-run. Existing resources are left in place.

set -euo pipefail

# ============================================================================
# CONFIGURATION - edit these
# ============================================================================
PROJECT_ID="egen-gcr"
REGION="us-west1"
GAR_REPO="egen-cicd-net"

DEPLOYER_SA_NAME="jenkins-deployer"
RUNTIME_SA_NAME="webapphost-runtime"

# Where to write the JSON key for Jenkins. Treat this file like a password.
KEY_OUTPUT_FILE="./jenkins-deployer-key.json"

# Additional runtime roles your app needs (uncomment / edit as appropriate)
RUNTIME_EXTRA_ROLES=(
    # "roles/cloudsql.client"
    # "roles/secretmanager.secretAccessor"
    # "roles/pubsub.publisher"
    # "roles/storage.objectViewer"
)

# Retry tuning
SA_VISIBILITY_ATTEMPTS=30   # x 2s = up to 60s waiting for SA to be visible
RETRY_MAX_ATTEMPTS=10       # for IAM binding calls
RETRY_DELAY_SECONDS=3

# ============================================================================
# DERIVED VALUES - don't edit
# ============================================================================
DEPLOYER_SA_EMAIL="${DEPLOYER_SA_NAME}@${PROJECT_ID}.iam.gserviceaccount.com"
RUNTIME_SA_EMAIL="${RUNTIME_SA_NAME}@${PROJECT_ID}.iam.gserviceaccount.com"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${GREEN}[OK]${NC}   $1"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
err()  { echo -e "${RED}[ERR]${NC}  $1" >&2; }

# ============================================================================
# RESILIENCE HELPERS
# ============================================================================

# Retry an arbitrary command with exponential-ish backoff.
retry() {
    local attempt=1
    while true; do
        if "$@"; then
            return 0
        fi
        if [ $attempt -ge $RETRY_MAX_ATTEMPTS ]; then
            err "Command failed after $RETRY_MAX_ATTEMPTS attempts: $*"
            return 1
        fi
        warn "Attempt $attempt failed, retrying in ${RETRY_DELAY_SECONDS}s..."
        attempt=$((attempt + 1))
        sleep $RETRY_DELAY_SECONDS
    done
}

# Wait until a service account is visible via describe.
# GCP returns success on `create` before the SA is fully propagated, so
# subsequent IAM calls can fail with "does not exist."
wait_for_sa() {
    local email=$1
    local attempt=0

    while [ $attempt -lt $SA_VISIBILITY_ATTEMPTS ]; do
        if gcloud iam service-accounts describe "${email}" \
             --project="${PROJECT_ID}" >/dev/null 2>&1; then
            log "Service account is visible: ${email}"
            return 0
        fi
        attempt=$((attempt + 1))
        sleep 2
    done

    err "Service account ${email} not visible after $((SA_VISIBILITY_ATTEMPTS * 2))s"
    return 1
}

# ============================================================================
# RESOURCE HELPERS
# ============================================================================

ensure_api() {
    local api=$1
    if gcloud services list --enabled --project="${PROJECT_ID}" \
         --format="value(config.name)" | grep -q "^${api}$"; then
        log "API already enabled: ${api}"
    else
        gcloud services enable "${api}" --project="${PROJECT_ID}"
        log "Enabled API: ${api}"
    fi
}

ensure_sa() {
    local name=$1
    local display=$2
    local email="${name}@${PROJECT_ID}.iam.gserviceaccount.com"

    if gcloud iam service-accounts describe "${email}" \
         --project="${PROJECT_ID}" >/dev/null 2>&1; then
        log "Service account exists: ${email}"
    else
        gcloud iam service-accounts create "${name}" \
            --display-name="${display}" \
            --project="${PROJECT_ID}"
        log "Created service account: ${email}"
        # Wait for it to be fully visible before any IAM binding calls
        wait_for_sa "${email}"
    fi
}

ensure_project_role() {
    local member=$1
    local role=$2
    # Wrapped in retry to handle IAM eventual consistency
    retry gcloud projects add-iam-policy-binding "${PROJECT_ID}" \
        --member="serviceAccount:${member}" \
        --role="${role}" \
        --condition=None \
        --quiet >/dev/null
    log "Granted ${role} to ${member}"
}

ensure_sa_role() {
    local target_sa_email=$1
    local member=$2
    local role=$3
    retry gcloud iam service-accounts add-iam-policy-binding "${target_sa_email}" \
        --member="serviceAccount:${member}" \
        --role="${role}" \
        --quiet >/dev/null
    log "Granted ${role} on ${target_sa_email} to ${member}"
}

ensure_gar_repo() {
    if gcloud artifacts repositories describe "${GAR_REPO}" \
         --location="${REGION}" --project="${PROJECT_ID}" >/dev/null 2>&1; then
        log "Artifact Registry repo exists: ${GAR_REPO}"
    else
        gcloud artifacts repositories create "${GAR_REPO}" \
            --repository-format=docker \
            --location="${REGION}" \
            --project="${PROJECT_ID}" \
            --description="Docker images for ${PROJECT_ID}"
        log "Created Artifact Registry repo: ${GAR_REPO}"
    fi
}

# ============================================================================
# MAIN
# ============================================================================
echo "============================================"
echo "Project:        ${PROJECT_ID}"
echo "Region:         ${REGION}"
echo "GAR repo:       ${GAR_REPO}"
echo "Deployer SA:    ${DEPLOYER_SA_EMAIL}"
echo "Runtime SA:     ${RUNTIME_SA_EMAIL}"
echo "============================================"
echo

# 0. Sanity checks
if ! command -v gcloud >/dev/null 2>&1; then
    err "gcloud CLI not found. Install it first."
    exit 1
fi

if ! gcloud projects describe "${PROJECT_ID}" >/dev/null 2>&1; then
    err "Cannot access project ${PROJECT_ID}. Are you logged in? Try: gcloud auth login"
    exit 1
fi

# 1. Enable required APIs
echo ">>> Enabling APIs..."
ensure_api "iam.googleapis.com"
ensure_api "artifactregistry.googleapis.com"
ensure_api "run.googleapis.com"
ensure_api "cloudresourcemanager.googleapis.com"

# 2. Artifact Registry repo
echo
echo ">>> Ensuring Artifact Registry repository..."
ensure_gar_repo

# 3. Create service accounts (waits for visibility after creation)
echo
echo ">>> Creating service accounts..."
ensure_sa "${DEPLOYER_SA_NAME}" "Jenkins deployer (build/push/deploy)"
ensure_sa "${RUNTIME_SA_NAME}"  "Cloud Run runtime identity"

# Defensive: wait again in case the SAs already existed but were just-created
# in a prior run that didn't quite finish.
wait_for_sa "${DEPLOYER_SA_EMAIL}"
wait_for_sa "${RUNTIME_SA_EMAIL}"

# 4. Grant project-level roles to the DEPLOYER
echo
echo ">>> Granting deployer roles..."
ensure_project_role "${DEPLOYER_SA_EMAIL}" "roles/artifactregistry.writer"
ensure_project_role "${DEPLOYER_SA_EMAIL}" "roles/run.admin"

# 5. Grant project-level roles to the RUNTIME
echo
echo ">>> Granting runtime roles..."
ensure_project_role "${RUNTIME_SA_EMAIL}" "roles/artifactregistry.reader"

for role in "${RUNTIME_EXTRA_ROLES[@]}"; do
    ensure_project_role "${RUNTIME_SA_EMAIL}" "${role}"
done

# 6. Allow the deployer to "actAs" the runtime SA (required by Cloud Run deploy)
echo
echo ">>> Granting actAs permission..."
ensure_sa_role "${RUNTIME_SA_EMAIL}" "${DEPLOYER_SA_EMAIL}" "roles/iam.serviceAccountUser"

# 7. Generate JSON key for Jenkins
echo
echo ">>> Generating Jenkins key..."
if [ -f "${KEY_OUTPUT_FILE}" ]; then
    warn "Key file already exists at ${KEY_OUTPUT_FILE}"
    warn "Skipping key generation. Delete the file and re-run to create a new one."
else
    gcloud iam service-accounts keys create "${KEY_OUTPUT_FILE}" \
        --iam-account="${DEPLOYER_SA_EMAIL}"
    chmod 600 "${KEY_OUTPUT_FILE}"
    log "Wrote key to ${KEY_OUTPUT_FILE}"
fi

# ============================================================================
# DONE
# ============================================================================
echo
echo "============================================"
echo -e "${GREEN}Setup complete.${NC}"
echo "============================================"
echo
echo "Next steps:"
echo "  1. Upload ${KEY_OUTPUT_FILE} to Jenkins:"
echo "       Manage Jenkins → Credentials → Add Credentials"
echo "       Kind:  Secret file"
echo "       ID:    gar-service-account"
echo "       File:  ${KEY_OUTPUT_FILE}"
echo
echo "  2. Delete the local key file after upload:"
echo "       rm ${KEY_OUTPUT_FILE}"
echo
echo "  3. In your Jenkinsfile, reference:"
echo "       GCP_PROJECT   = '${PROJECT_ID}'"
echo "       GCP_REGION    = '${REGION}'"
echo "       GAR_REPO      = '${GAR_REPO}'"
echo "       RUNTIME_SA    = '${RUNTIME_SA_EMAIL}'"
echo
echo "  4. Verify deployer SA permissions:"
echo "       gcloud projects get-iam-policy ${PROJECT_ID} \\"
echo "         --flatten='bindings[].members' \\"
echo "         --filter='bindings.members:${DEPLOYER_SA_EMAIL}' \\"
echo "         --format='value(bindings.role)'"
echo