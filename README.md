# Storage and Analysis of Robot Acquired plant data

SARA (Storage and Analysis of Robot Acquired plant data) is an ASP.NET Core
Web API that indexes inspection data published by ISAR and orchestrates
Argo-based analysis workflows on it, exposing the results to Flotilla. Each
incoming inspection becomes an `InspectionRecord`; one or more records are
grouped into an `Analysis` (the use-case), which executes as an `AnalysisRun`
made up of one or more sequential `Workflow` steps.

When running locally the endpoint is reachable at https://localhost:8100
(`/` redirects to Swagger).

## Architecture at a glance

- `InspectionRecord` -- one row per ISAR inspection result, persisted on
  receipt of an `isar/+/inspection_result` MQTT message.
- `Analysis` -> `AnalysisRun` -> `Workflow` -- three-tier model where an
  Analysis describes the use-case, an AnalysisRun is one execution attempt,
  and each Workflow is a single Argo step.
- `AnalysisGroup` -- lets a single Analysis span multiple InspectionRecords. 
  The group is buffered until all expected records arrive or 
  `AnalysisGroupTimeoutMinutes` elapses.
- Workflow chains run sequentially. By default each step's output blob
  becomes the next step's input; per-workflow rewiring lives in the matching
  `IWorkflowResultHandler`.
- The Argo trigger payload has a stable core (`workflowId`,
  `inputBlobStorageLocations`, `outputBlobStorageLocation`) plus an `extras`
  object populated by `ITriggerPayloadEnricher` implementations matched on
  workflow type.
- Result handling is split: `WorkflowResultHandlers/` runs per step,
  `AnalysisResultHandlers/` runs once the whole Analysis is done.

## Run locally

```bash
make run            # or: dotnet run --project api
```

### Local authentication

Cloud deployments authenticate via Azure Workload Identity (federated
credentials on the sara app registration). Locally you have two options,
selected by `ASPNETCORE_ENVIRONMENT`:

- **`Local` (default for `make run`)** — uses `appsettings.Local.json`
  with `AllowedAuthMethods: ["AzureCliBootstrap", "ClientSecret"]`.
  Requires `az login` first; the developer's Azure CLI session is used
  to bootstrap Key Vault access, and the app registration's client
  secret is loaded from Key Vault for subsequent Azure calls.

- **`Development` (mimics deployed dev)** — uses
  `appsettings.Development.json` with
  `AllowedAuthMethods: ["WorkloadIdentity", "ClientSecret"]`. Workload
  Identity is unavailable outside AKS, so the chain falls through to
  `ClientSecretCredential`. Provide the secret via an `api/.env` file
  (gitignored):

  ```
  AzureAd__ClientSecret=<value of AzureAd--ClientSecret in saradev-kv>
  ```

  Then run:

  ```bash
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project api
  ```

Use `Local` for normal day-to-day development. Use `Development` when
you need behaviour identical to the deployed dev pod (real Postgres,
real OpenTelemetry export, etc.).


## Test & format

```bash
make test           # xUnit; integration tests use Postgres Testcontainers (Docker required)
make format         # CSharpier
```


## Creating a new workflow

1. Register the workflow under `Analysis:Workflows` in `appsettings.json`
   with its `TriggerUrl`, `OutputStorageAccount`, `OutputBlobContainer` and
   (optionally) `OutputFileExtension`.
2. Reference it from one or more chains under `Analysis:Analyses`, e.g.
   `"my-analysis": { "Workflows": ["anonymizer", "my-workflow"] }`.
3. Add an `IWorkflowResultHandler` in
   `api/Services/ResultHandlers/WorkflowResultHandlers/` that matches the
   new workflow type.
4. If the analyzer needs per-workflow parameters in the Argo trigger
   payload, add an `ITriggerPayloadEnricher` that populates `extras`.
5. Add the matching Argo `WorkflowTemplate` + `Sensor` in
   [analytics-infrastructure](https://github.com/equinor/analytics-infrastructure)
   and an analyzer image repo that implements the generic CLI contract
   (`--input-blob-storage-locations`, `--output-blob-storage-location`,
   `--extras`).

## workflow-notifier

`workflow-notifier/` is a standalone Python CLI, shipped as its own Docker
image and invoked from inside Argo `WorkflowTemplate` steps. It exposes
three subcommands -- `started`, `result` and `exited` -- each of which
performs an authenticated `PUT` against `/api/workflow/{id}/...` so SARA can
track workflow progress and persist results.

## Analyzer services

- [sara-anonymizer](https://github.com/equinor/sara-anonymizer/) -- anonymizes images
- [sara-thermal-reading](https://github.com/equinor/sara-thermal-reading/) -- extracts temperatures from thermal images
- [sara-fence-detection](https://github.com/equinor/sara-fence-detection/) -- detects fence breaches
- [sara-constant-level-oiler](https://github.com/equinor/sara-constant-level-oiler/) -- reads constant-level oiler spherical glasses
- [sara-timeseries](https://github.com/equinor/sara-timeseries/) -- timeseries ingestion
- [sara-sap](https://github.com/equinor/sara-sap/) -- SAP integration

## Deployments

We currently have 3 environments (Development, Staging, and Production) deployed to Aurora.

| Environment | Deployment                                                                           |
| ----------- | ------------------------------------------------------------------------------------ |
| Development | [Backend](https://shared.dev.aurora.equinor.com/sara-dev-backend/swagger/index.html) |
| Staging     | [Backend](https://shared.aurora.equinor.com/sara-staging-backend/swagger/index.html) |
| Production  | [Backend](https://shared.aurora.equinor.com/sara-prod-backend/swagger/index.html)    |
