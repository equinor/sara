# Workflow Notifier

The purpose of the workflow notifier is to serve as communication between SARA and Argo
Workflows.

The notifier has its own identity and authenticates towards SARA to reach SARA's
generic workflow notification endpoints. Each workflow execution is identified by an
opaque `workflowId` (UUID) that SARA hands to Argo when triggering the workflow; the
notifier passes this same id back when reporting lifecycle events.

## Endpoints called

```
PUT /api/workflow/{workflowId}/started
PUT /api/workflow/{workflowId}/result    body: {"resultJson": "<stringified json>"}
PUT /api/workflow/{workflowId}/exited    body: {"exitStatus": "Succeeded|Failed|Error",
                                                "errorMessage": "..."}
```

The same three commands work for every workflow type (anonymizer, fencilla, cloe,
thermal-reading, ...). The `result` payload is forwarded verbatim and is interpreted
on the SARA side by the workflow's result handler.

## CLI

```
notifier started <workflow-id>
notifier result  <workflow-id> <result-json>
notifier exited  <workflow-id> <Succeeded|Failed|Error> [--error-message TEXT]
```

`<workflow-id>` is validated as a UUID before any HTTP call. `<result-json>` is
validated as parseable JSON and then transmitted verbatim.

## Authentication

The notifier authenticates to the SARA API using `azure-identity`. The
credential types it will try are configured through the `ALLOWED_AUTH_METHODS`
environment variable: a comma-separated, ordered list whose allowed values are
`WorkloadIdentity` and `ClientSecret` (case-insensitive). When more than one
method is listed, the order determines the priority inside the resulting
`ChainedTokenCredential`.

### Cloud (dev / staging / prod)

All cloud environments run the notifier with Azure Workload Identity and
`ALLOWED_AUTH_METHODS=WorkloadIdentity` (also the default when the variable is
unset). The `workflow-notifier-sa` service account is federated to a
per-environment notifier app registration; the `azure-workload-identity`
mutating webhook injects `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_FEDERATED_TOKEN_FILE` and `AZURE_AUTHORITY_HOST` into the notifier pod,
and `WorkloadIdentityCredential` exchanges the projected service-account token
for an Entra ID access token. No client secret is provisioned to the cluster.

### Local development

For local docker-compose and docker-desktop Kubernetes runs, set
`NOTIFIER_CLIENT_SECRET` in `.env` and include `ClientSecret` in
`ALLOWED_AUTH_METHODS`. The default in `.env.example` is
`WorkloadIdentity,ClientSecret`, which tries Workload Identity first and falls
back to the client secret when no federated token file is present — convenient
when running the same image both in-cluster and on a workstation.

When using the docker-desktop Kubernetes overlay in
`analytics-infrastructure`, the per-workflow `workflow-notifier-config`
ConfigMap pins `ALLOWED_AUTH_METHODS=ClientSecret`, and
`overlays/local/apply_to_local.py` materializes `NOTIFIER_CLIENT_SECRET` from
`saradev-kv` into a Kubernetes `Secret` named `workflow-notifier-secrets` that
the local overlay mounts into each notifier step.

## Running the mock

When developing it is useful to run SARA locally. Running real Argo Workflows locally
is awkward, so a Flask mock simulates the trigger endpoints SARA POSTs to. The mock
spins up a thread per trigger and uses this notifier package to call back into SARA
with `started` -> `result` -> `exited`.

Install:

```
uv sync --extra dev
```

Populate a `.env` file with the keys shown in `.env.example`, then run:

```
uv run python mocks/argo_workflow_mock.py
```
