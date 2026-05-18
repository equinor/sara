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
