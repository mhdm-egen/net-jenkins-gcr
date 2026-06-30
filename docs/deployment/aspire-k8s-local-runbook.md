# Runbook: deploy a .NET Aspire app to a local Kubernetes cluster via Aspir8, pulling from local Nexus

This is the **Phase 1 local proof** for the "deploy Aspire apps to Kubernetes" feature
(see the plan). It validates the end-to-end mechanics by hand so the Phase 2 service
integration is built on confirmed facts. Verified working: a 2-service Aspire app's
images are pushed to Nexus, Aspir8 generates Kustomize manifests, and the local cluster
**pulls the images from Nexus** and runs them.

## Environment used

- `aspirate` (Aspir8) — `dotnet tool install -g Aspirate` (v9.1.0). NOTE: this is **not** the
  official `aspire` CLI; it's the community Aspir8 tool whose command is `aspirate`.
- Local Kubernetes — Docker Desktop, current context `docker-desktop`, single node
  `desktop-control-plane` (k8s v1.31.1). `kubectl` + `docker` on PATH.
- Nexus — docker **hosted** repo `docker-private` on `localhost:8082` (host view), auth required.

## The flow

### 1. Sample Aspire app
`dotnet new aspire-starter -n SampleApp` → AppHost + ApiService + Web + ServiceDefaults
(two deployable services: `apiservice`, `webfrontend`).

### 2. Configure Aspir8 (in the AppHost dir)
```
aspirate init -cr localhost:8082 -ct 1.0.0 --disable-secrets --non-interactive
```
Writes `aspirate.json` (Registry, Tag, Builder=docker).

### 3. Build + push images to Nexus
```
aspirate generate --non-interactive --disable-secrets --include-dashboard false \
  --image-pull-policy IfNotPresent --output-format kustomize
```
Aspir8 runs `dotnet publish -t:PublishContainer -p:ContainerRegistry=localhost:8082
-p:ContainerRepository=<resource> -p:ContainerImageTag=latest` per project and pushes to Nexus.
(In the platform, **CI does this step**; the service later runs with `--skip-build`.)

### 4. Generate manifests only (the real `--skip-build` path)
```
aspirate generate --non-interactive --disable-secrets --include-dashboard false \
  --skip-build --image-pull-policy IfNotPresent --output-format kustomize
```
Emits `aspirate-output/{resource}/{deployment,service,kustomization}.yaml` + a root
`kustomization.yaml`.

### 5. Make the cluster able to pull from Nexus
- **Networking:** the node can't use `localhost:8082` (that's the node itself). It resolves
  `host.docker.internal` → the host, so manifests must reference `host.docker.internal:8082`.
- **Insecure-registry trust** (Nexus:8082 is plain HTTP). containerd on the node already has
  `config_path = "/etc/containerd/certs.d"`, so just drop a `hosts.toml` (no restart needed):
  ```
  # /etc/containerd/certs.d/host.docker.internal:8082/hosts.toml  (inside the node)
  server = "http://host.docker.internal:8082"
  [host."http://host.docker.internal:8082"]
    capabilities = ["pull", "resolve"]
    skip_verify = true
  ```
- **Image-pull secret** (Nexus requires auth):
  ```
  kubectl create namespace sampleapp
  kubectl -n sampleapp create secret docker-registry nexus-pull \
    --docker-server=host.docker.internal:8082 --docker-username=<u> --docker-password=<p>
  kubectl -n sampleapp patch serviceaccount default \
    -p '{"imagePullSecrets":[{"name":"nexus-pull"}]}'
  ```
- **Rewrite the image host** (and, in the platform, pin to the build#/digest — see below) via the
  root `kustomization.yaml`:
  ```
  namespace: sampleapp
  resources: [apiservice, webfrontend]
  images:
  - name: localhost:8082/apiservice
    newName: host.docker.internal:8082/apiservice
  - name: localhost:8082/webfrontend
    newName: host.docker.internal:8082/webfrontend
  ```

### 6. Deploy
```
kubectl apply -k aspirate-output
kubectl -n sampleapp rollout status deploy/apiservice
kubectl -n sampleapp get events --field-selector reason=Pulled   # confirms pull from Nexus
```
Result: both pods `Running`, events show `Successfully pulled image
"host.docker.internal:8082/apiservice:latest"` — i.e. pulled from local Nexus.

Teardown: `kubectl delete ns sampleapp`.

## Findings that drive Phase 2 (the integration contract)

1. **Image-name contract.** Aspir8 references images as `{registry}/{resourceName}:{tag}` where
   `resourceName` is the Aspire resource (e.g. `apiservice`) and the tag came out as **`latest`**
   (the project's `ContainerImageTag` default — NOT the `-ct` init fallback). CI must push to
   exactly `{registry}/{resource}:{tag}` for `--skip-build` to resolve.
2. **Provenance gap (important).** Aspir8's default `:latest` **bypasses** the platform's CI
   build/commit tagging. Nexus already holds CI images tagged like `ci-33`, `33-22b1075`,
   `22b1075` (build#/commit). Fix: after `generate`, inject a Kustomize `images:` override mapping
   each resource to the exact **build#-tagged / digest-pinned** Nexus ref from the `KnownContainer`
   inventory (`newName` for the host, `digest:`/`newTag:` for the pin). The `images:` mechanism is
   validated here (used it for the host rewrite). Record build#, commit SHA, and digests on the run.
3. **Two registry addresses.** Push via `localhost:8082` (host; insecure-by-default + existing
   docker auth); cluster pulls via `host.docker.internal:8082` (node→host). The `images:` override
   bridges them.
4. **Non-interactive flags required by Aspir8:** `--non-interactive --disable-secrets
   --include-dashboard false --image-pull-policy <policy>` (and `--skip-build` for the deploy path).
   These are what the service's `IAspirateRunner` must pass.
5. **Manifest handoff.** `aspirate generate` first writes the Aspire `manifest.json` from the
   AppHost; the service needs that manifest (CI artifact) + the registry/tag to run `--skip-build`.
6. **Cluster prerequisites** are environment setup, not per-deploy: insecure-registry `hosts.toml`
   on nodes + the `nexus-pull` secret in the target namespace. Document as platform prerequisites
   (alongside `prerequisites.md`).
