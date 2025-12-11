import typer

from workflow_notifier.notifier import notify_exited, notify_result, notify_started

WORKFLOW_TYPE = "constant-level-oiler-estimator"

app = typer.Typer()


@app.command()
def notify_constant_level_oiler_estimator_started(
    inspection_id: str, workflow_name: str
):
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_constant_level_oiler_estimator_result(inspection_id: str, oil_level: str):
    result = {"OilLevel": oil_level}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_constant_level_oiler_estimator_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
