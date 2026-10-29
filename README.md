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

- `InspectionRecord` -- one row per ISAR inspection, persisted on receipt of an
  `isar/+/inspection_result` or `isar/+/inspection_value` MQTT message. Values
  must include `blob_storage_data_path` pointing to JSON already uploaded by
  ISAR. SARA verifies the blob and saves the record before forwarding the
  unchanged measurement to sara-timeseries.
- `Analysis` -> `AnalysisRun` -> `Workflow` -- three-tier model where an
  Analysis describes the use-case, an AnalysisRun is one execution attempt,
  and each Workflow is a single Argo step.
- `AnalysisGroup` -- lets a single Analysis span multiple InspectionRecords. 
  The group is buffered until all expected records arrive or 
  `AnalysisGroupTimeoutMinutes` elapses.
- Workflow chains run sequentially. Each step's inputs, planned output
  locations and Argo parameters are wired in YAML under
  `api/Definitions/` (see "Creating a new workflow"); a step can read the
  output of any earlier step.
- The Argo trigger payload has a stable core (`workflowId`,
  `inputBlobStorageLocations`, `outputBlobStorageLocation`) plus an `extras`
  object built from the workflow definition's `arguments`.
- The trigger's `outputBlobStorageLocation` is a requested destination, not a
  confirmed upload. Successful workflows report it in their JSON `result` only
  after uploading, with nonblank `storageAccount`, `blobContainer`, and `blobName`
  strings. SARA persists only valid reported locations; omission or `null` means
  no output. Malformed locations are logged and withheld without discarding metrics.
- Result handling is split: `WorkflowResultHandlers/` runs per step,
  `AnalysisResultHandlers/` runs once the whole Analysis is done.

## Run locally

```bash
make run            # or: dotnet run --project api
```

The Docker frontend build explicitly installs pinned Corepack because Node 25+
no longer bundles it. Corepack uses the pnpm version declared in
`frontend/package.json` under `packageManager`.

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

- **Keycloak (no Azure sign-in)** — both options above need Azure credentials to
  authenticate: `Local` needs `az login` to reach `saradev-kv`, `Development` needs a
  client secret from it. Setting `Authentication:Provider` to `Oidc` points token
  validation at any conformant OpenID Connect issuer instead.

  ```bash
  docker compose --profile keycloak up keycloak
  ```

  Keycloak comes up on `http://localhost:8080` with the same realm the armada
  integration tests use. The realm is read from
  `../armada/robotics_integration_tests/custom_realms`; set `KEYCLOAK_REALM_DIR` if
  armada is not checked out beside this repository.

  Then add to `api/.env`:

  ```
  Authentication__Provider=Oidc
  AzureAd__Authority=http://localhost:8080/realms/robotics
  AzureAd__ClientId=sara-test
  AzureAd__AllowedAuthMethods__0=WorkloadIdentity
  KeyVault__UseKeyVault=false
  Database__UseInMemoryDatabase=true
  ```

  A token for calling the API directly, or from Swagger:

  ```bash
  curl -s -X POST http://localhost:8080/realms/robotics/protocol/openid-connect/token \
    -d grant_type=client_credentials \
    -d client_id=integration-tests \
    -d client_secret=integration-tests-secret \
    -d scope=sara-api | jq -r .access_token
  ```

  This covers sign-in only. `AzureAd:AllowedAuthMethods` is a separate concern — the
  `TokenCredential` used for blob storage and Microsoft Graph — so any code path
  reaching those still needs Azure. The frontend also still signs in against Entra ID.

  The armada integration tests run SARA against the same realm, under
  `ASPNETCORE_ENVIRONMENT=IntegrationTest`. That environment has no appsettings file
  here on purpose: its configuration is passed in as environment variables from
  [`armada/robotics_integration_tests/custom_containers/sara.py`](https://github.com/equinor/armada/blob/main/robotics_integration_tests/custom_containers/sara.py),
  alongside the realm that defines the clients and scopes. Change it there, not here.


## Test & format

```bash
make test           # xUnit; integration tests use Postgres Testcontainers (Docker required)
make format         # CSharpier
```


## Creating a new workflow

1. Add a workflow definition in `api/Definitions/workflows/<name>.yaml`:
   `templateName`, typed `inputs`, `outputs` (optional `fileExtension`;
   `naming: InputName` derives the name from the first input) and `arguments`
   mapping them to the Argo parameters. `skipChainIf` makes it a gate.
2. Set `Analysis:Workflows:<name>:OutputStorageAccount` for each environment
   in `appsettings.json`.
3. Use it in an analysis in `api/Definitions/analyses/<name>.yaml`, binding every
   input from `InspectionImages`, `InspectionMetadata`, `ReferenceMetadata`
   (`required: false` to leave it out when missing) or `fromStep`/`output`
   (a planned output of any earlier step). Add the analysis to
   `DefaultAnalysisBy...` if it should run by default.
4. Add an `IWorkflowResultHandler` in
   `api/Services/ResultHandlers/WorkflowResultHandlers/` that matches the
   new workflow type.
5. Add the matching Argo `WorkflowTemplate` in
   [robotics-infrastructure](https://github.com/equinor/robotics-infrastructure)
   and an analyzer image repo that implements the generic CLI contract
   (`--input-blob-storage-locations`, `--output-blob-storage-location`,
   `--extras`).

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
| Development | [Frontend](https://sara.dev.robotics.equinor.com/)<br>[Backend](https://sara.dev.robotics.equinor.com/swagger/index.html) |
| Staging     | [Frontend](https://sara.staging.robotics.equinor.com/)<br>[Backend](https://shared.aurora.equinor.com/sara-staging-backend/swagger/index.html) |
| Production  | [Frontend](https://sara.robotics.equinor.com/)<br>[Backend](https://sara.robotics.equinor.com/swagger/index.html)    |
