// =============================================================================
// Job DSL seed for the cicd-* pipeline jobs.
//
// Run by a "seed" job (Process Job DSLs build step) that has first checked out
// this repo, or by JCasC's jobs: bootstrap. See jenkins/jobs/README.md.
//
// All three jobs load their Jenkinsfile from THIS repo (the pipeline-definition
// repo), not from the application being built. All three are repo-agnostic:
//
//   * cicd-build              -> lightweight Jenkinsfile fetch. The Jenkinsfile
//                                sets skipDefaultCheckout(true) and clones the
//                                caller-supplied GIT_URL itself.
//   * cicd-publish-nexus-*    -> lightweight Jenkinsfile fetch. The Jenkinsfiles
//                                set skipDefaultCheckout(true), then clone the app
//                                repo recorded in the upstream build-info.json
//                                (gitUrl), pinned to gitCommitHash, and pack/build
//                                that exact source — so they publish any repo, not
//                                just this monorepo (and exactly the built commit).
//                                Private repos: pass GIT_CREDENTIALS_ID.
//
// Declaring the parameters here pre-registers them so the orchestrator's first
// POST /buildWithParameters succeeds before any run has executed the declarative
// `parameters {}` block. The Jenkinsfile remains the source of truth thereafter.
// =============================================================================

// Where the Jenkinsfiles live. Override by binding these in the seed job
// (e.g. as String parameters / env) — otherwise the defaults below are used.
def pipelineRepo   = binding.hasVariable('PIPELINE_REPO_URL')           ? PIPELINE_REPO_URL           : 'https://github.com/mhdm-egen/net-jenkins-gcr.git'
def pipelineBranch = binding.hasVariable('PIPELINE_REPO_BRANCH')        ? PIPELINE_REPO_BRANCH        : 'main'
def pipelineCreds  = binding.hasVariable('PIPELINE_REPO_CREDENTIAL_ID') ? PIPELINE_REPO_CREDENTIAL_ID : ''

// Common build-container plumbing shared by every job.
def BUILD_IMAGE = 'netsdk10:latest'
def BUILD_ARGS_BUILD   = '-v /tmp/nuget:/tmp/nuget -e DOTNET_CLI_TELEMETRY_OPTOUT=1 --net=cicd-net -u root -v /var/run/docker.sock:/var/run/docker.sock --group-add 0'
// Scan job: needs the Trivy DB cache, no docker.sock (it doesn't build images).
def BUILD_ARGS_SCAN    = '-v /tmp/nuget:/tmp/nuget -e DOTNET_CLI_TELEMETRY_OPTOUT=1 --net=cicd-net -u root -v /tmp/trivy-cache:/root/.cache/trivy --group-add 0'
// Publish jobs: docker.sock for image builds + the Trivy DB cache for the image scan.
def BUILD_ARGS_PUBLISH = '-v /tmp/nuget:/tmp/nuget -e DOTNET_CLI_TELEMETRY_OPTOUT=1 --net=cicd-net -u root -v /var/run/docker.sock:/var/run/docker.sock -v /tmp/trivy-cache:/root/.cache/trivy --group-add 0'

// Helper: build a pipelineJob that loads `scriptPath` from the pipeline repo.
// `lightweight` is always true now — every job fetches just its Jenkinsfile and clones
// the app repo itself (cicd-build from GIT_URL; scan/publish from build-info.json's gitUrl).
def makePipeline = { String name, String desc, String scriptPath, boolean lightweight, Closure params ->
    pipelineJob(name) {
        description(desc)
        logRotator {
            numToKeep(30)
            artifactNumToKeep(10)
        }
        parameters(params)
        definition {
            cpsScm {
                scm {
                    git {
                        remote {
                            url(pipelineRepo)
                            if (pipelineCreds) {
                                credentials(pipelineCreds)
                            }
                        }
                        branch(pipelineBranch)
                    }
                }
                scriptPath(scriptPath)
                lightweight(lightweight)
            }
        }
    }
}

// ---------------------------------------------------------------------------
// cicd-build — parameterized, repo-agnostic. The caller passes GIT_URL.
// ---------------------------------------------------------------------------
makePipeline('cicd-build',
    'Build the repository given by GIT_URL (compile + container discovery). Repo-agnostic: the Jenkinsfile clones GIT_URL@GIT_BRANCH itself. Security scanning is the downstream cicd-scan job.',
    'jenkins/build/Jenkinsfile',
    true) {
    stringParam('GIT_URL', '', 'Git repository URL to build (required). The Jenkinsfile clones this itself.')
    stringParam('GIT_BRANCH', 'main', 'Branch, tag, or ref to build')
    stringParam('GIT_CREDENTIALS_ID', '', 'Jenkins credentials id for cloning a private repo (blank = public/anonymous)')
    stringParam('BUILD_CONTAINER_IMAGE', BUILD_IMAGE, 'Image for the build container')
    stringParam('BUILD_CONTAINER_ARGS', BUILD_ARGS_BUILD, 'Arguments for the build container')
    stringParam('BUILD_FILE', 'src/app/cicd.sln', 'File to build (sln or csproj), relative to the cloned repo root')
    stringParam('BASE_VER', '1.0.0', 'Base version (Major.Minor.Patch) used to derive the build versions')
}

// ---------------------------------------------------------------------------
// cicd-scan — dependency SBOM + vulnerability scan of the upstream build.
// ---------------------------------------------------------------------------
makePipeline('cicd-scan',
    'Scan the upstream cicd-build: CycloneDX SBOM + NuGet vulnerability report (with the FAIL_ON_SEVERITY gate) + Trivy VEX, uploaded to Nexus. Clones the exact built commit.',
    'jenkins/scan/Jenkinsfile',
    true) {
    stringParam('BUILD_CONTAINER_IMAGE', BUILD_IMAGE, 'Image for the build container')
    stringParam('BUILD_CONTAINER_ARGS', BUILD_ARGS_SCAN, 'Arguments for the build container')
    stringParam('CYCLONEDX_TOOL_VERSION', '5.4.0', 'Version of the dotnet CycloneDX global tool')
    stringParam('TRIVY_VERSION', 'v0.55.0', 'Trivy release tag used to enrich the SBOM (bom-vex.json)')
    choiceParam('FAIL_ON_SEVERITY', ['none', 'high', 'critical'], 'Fail the scan when dependency vulnerabilities at this severity (or worse) are present')
    stringParam('SBOM_NEXUS_REPO_URL', 'http://nexus:8081/repository/sboms/', 'Nexus raw (hosted) repo URL for bom.json + vulnerabilities.json + bom-vex.json')
    stringParam('SBOM_NEXUS_CREDENTIAL_ID', 'nexus-sbom', 'Jenkins credential id (Username/Password) for the Nexus REST API')
    stringParam('GIT_CREDENTIALS_ID', '', 'Jenkins credentials id for cloning a private app repo (blank = public/anonymous)')
    stringParam('SOURCE_BUILD_JOB', 'cicd-build', 'Upstream build job whose build-info.json is pulled in')
    stringParam('SOURCE_BUILD_NUMBER', '', 'Specific upstream build number to scan. Blank = last successful build.')
}

// ---------------------------------------------------------------------------
// cicd-publish-nexus-nuget — packs + pushes the .nupkg to Nexus, uploads SBOM.
// ---------------------------------------------------------------------------
makePipeline('cicd-publish-nexus-nuget',
    'Pack the upstream build and push the NuGet package to Nexus.',
    'jenkins/publish/nexus/nuget/Jenkinsfile',
    true) {
    stringParam('BUILD_CONTAINER_IMAGE', BUILD_IMAGE, 'Image for the build container')
    stringParam('BUILD_CONTAINER_ARGS', BUILD_ARGS_PUBLISH, 'Arguments for the build container')
    stringParam('NUGET_SOURCE', 'http://nexus:8081/repository/nuget-hosted/', 'NuGet feed URL (Nexus hosted repo)')
    stringParam('NUGET_API_KEY_CREDENTIAL_ID', 'rhythm-nuget', 'Jenkins credential id (Secret Text) holding the NuGet API key')
    stringParam('GIT_CREDENTIALS_ID', '', 'Jenkins credentials id for cloning a private app repo (blank = public/anonymous)')
    stringParam('SOURCE_BUILD_JOB', 'cicd-scan', 'Upstream job whose build-info.json is pulled in (cicd-scan)')
    stringParam('SOURCE_BUILD_NUMBER', '', 'Specific upstream build number to publish. Blank = last successful build.')
}

// ---------------------------------------------------------------------------
// cicd-publish-nexus-docker — builds + pushes the container image to Nexus.
// ---------------------------------------------------------------------------
makePipeline('cicd-publish-nexus-docker',
    'Build the application container image and push it to the Nexus docker registry.',
    'jenkins/publish/nexus/docker/Jenkinsfile',
    true) {
    stringParam('BUILD_CONTAINER_IMAGE', BUILD_IMAGE, 'Image for the build container')
    stringParam('BUILD_CONTAINER_ARGS', BUILD_ARGS_PUBLISH, 'Arguments for the build container')
    stringParam('DOCKER_BUILD_FILE', '', 'Optional Dockerfile override (path within the app repo). Blank = built-in copy-only runtime Dockerfile generated by the job')
    stringParam('CONTAINER_NAME', '', 'Optional single-container override; normally blank — containers come from the cicd-build manifest in build-info.json')
    stringParam('NEXUS_DOCKER_HOST', 'nexus:8082', 'Nexus docker registry host:port')
    stringParam('NEXUS_DOCKER_CREDENTIAL_ID', 'rhythm-docker', 'Jenkins credential id (Username/Password) for the Nexus docker registry')
    stringParam('NEXUS_DOCKER_USER', 'admin', 'Nexus docker registry username')
    stringParam('NEXUS_DOCKER_PROTOCOL', 'http://', 'Nexus communications protocol (http:// or https://)')
    stringParam('TRIVY_VERSION', 'v0.55.0', 'Trivy release tag used to scan the built container image')
    choiceParam('FAIL_ON_SEVERITY', ['none', 'high', 'critical'], 'Fail the publish when the built image has OS/library vulnerabilities at this severity (or worse). Default: report only')
    stringParam('SBOM_NEXUS_REPO_URL', 'http://nexus:8081/repository/sboms/', 'Nexus raw (hosted) repo URL for the per-image scan report')
    stringParam('SBOM_NEXUS_CREDENTIAL_ID', 'nexus-sbom', 'Jenkins credential id (Username/Password) for the Nexus REST API')
    stringParam('GIT_CREDENTIALS_ID', '', 'Jenkins credentials id for cloning a private app repo (blank = public/anonymous)')
    stringParam('SOURCE_BUILD_JOB', 'cicd-scan', 'Upstream job whose build-info.json is pulled in (cicd-scan)')
    stringParam('SOURCE_BUILD_NUMBER', '', 'Specific upstream build number to publish. Blank = last successful build.')
}

// ---------------------------------------------------------------------------
// cicd-aspire-publish — build a .NET Aspire app with Aspir8 and publish its
// images + Kustomize-output archive to Nexus. Repo-agnostic source job (like
// cicd-build): the platform injects GIT_URL/GIT_BRANCH/BASE_VER (+ APPHOST_PROJECT
// for typed Aspire repositories); the Jenkinsfile clones the app repo itself.
// Aspir8 owns the multi-container build/push, so there is no separate cicd-build
// or copy-only-Dockerfile step upstream.
// ---------------------------------------------------------------------------
makePipeline('cicd-aspire-publish',
    'Build a .NET Aspire app (aspirate) and publish its images (build#+commit tagged) + Kustomize-output manifest archive to Nexus, with per-image Trivy scan + SBOM. Repo-agnostic: clones GIT_URL@GIT_BRANCH itself.',
    'jenkins/publish/aspire/Jenkinsfile',
    true) {
    stringParam('GIT_URL', '', 'Git repository of the Aspire app to build (required; injected from the repository).')
    stringParam('GIT_BRANCH', 'main', 'Branch, tag, or ref to build')
    stringParam('GIT_CREDENTIALS_ID', '', 'Jenkins credentials id for cloning a private repo (blank = public/anonymous)')
    stringParam('APPHOST_PROJECT', '', 'Path (within the repo) to the Aspire AppHost dir or .csproj. Blank = auto-discover the single *.AppHost.csproj.')
    stringParam('BASE_VER', '1.0.0', 'Base version (Major.Minor.Patch); build# + commit hash are appended')
    stringParam('APP_NAME', '', 'Artifact name segment for the manifest archive. Blank = the AppHost project name, lowercased.')
    stringParam('NAMESPACE', 'default', 'Kubernetes namespace baked into the generated manifests')
    stringParam('BUILD_CONTAINER_IMAGE', BUILD_IMAGE, 'Image for the build container')
    stringParam('BUILD_CONTAINER_ARGS', BUILD_ARGS_PUBLISH, 'Arguments for the build container (docker socket for image build/push + Trivy DB cache)')
    stringParam('ASPIRATE_PACKAGE_VERSION', '', 'Pin the Aspirate global tool version (blank = latest)')
    stringParam('NEXUS_DOCKER_HOST', 'nexus:8082', 'Nexus docker registry host:port images are pushed to')
    stringParam('NEXUS_DOCKER_PROTOCOL', 'http://', 'Nexus docker registry protocol')
    stringParam('NEXUS_DOCKER_CREDENTIAL_ID', 'rhythm-docker', 'Jenkins username/password credential id for the Nexus docker registry')
    stringParam('NEXUS_RAW_REPO_URL', 'http://nexus:8081/repository/raw-hosted/', 'Nexus raw (hosted) repo base URL for the Kustomize-output archive')
    stringParam('NEXUS_RAW_CREDENTIAL_ID', 'nexus-sbom', 'Jenkins username/password credential id for the Nexus REST API (raw upload)')
    stringParam('TRIVY_VERSION', 'v0.55.0', 'Trivy release tag used to scan the built images')
    choiceParam('FAIL_ON_SEVERITY', ['none', 'high', 'critical'], 'Fail the publish when a built image has OS/library vulnerabilities at this severity (or worse). Default: report only')
    stringParam('SBOM_NEXUS_REPO_URL', 'http://nexus:8081/repository/sboms/', 'Nexus raw (hosted) repo URL where per-image scan reports are uploaded')
    stringParam('SBOM_NEXUS_CREDENTIAL_ID', 'nexus-sbom', 'Jenkins username/password credential id for the Nexus REST API (SBOM upload)')
}
