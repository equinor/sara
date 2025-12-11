# Workflow Notifier

The purpose of the workflow notifier is to serve as communication between SARA and Argo
Workflow.

The workflow notifier has its own identity and authorizes towards SARA to reach SARA's
endpoints for notifying about the progress of the various steps in the workflow.

The workflow notifier sends notifications to the following endpoints on SARA

/WorkflowNotification/<workflow_type>/started
/WorkflowNotification/<workflow_type>/result
/WorkflowNotification/<workflow_type>/exited

and currently supports the following workflow types:
"anonymizer", "constant-level-oiler-estimator", "fencilla", "thermal-reading"

## Running the mock

When developing, it can be useful to run SARA locally. Since it is a bit harder to run
Argo Workflows locally, we have written a mock for the /trigger-analysis endpoint. This
usually triggers the workflow, which again uses the various commands in this package to
notify SARA about the process.

To run the mock, first install this package (we recommend in a virtual environment) with
`pip install -e ".[dev]"`

,populate a `.env` file with the keys presented in `.env.example`

and run it with

`python mocks/argo_workflow_mock.py`
