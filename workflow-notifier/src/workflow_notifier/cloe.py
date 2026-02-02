import logging

import typer

from workflow_notifier.notifier import notify_exited, notify_result, notify_started

logger = logging.getLogger(__name__)

WORKFLOW_TYPE = "constant-level-oiler-estimator"

app = typer.Typer()


@app.command()
def notify_constant_level_oiler_estimator_started(
    inspection_id: str, workflow_name: str
):
    logger.info(
        f"Notify CLOE started for inspection {inspection_id} and workflow {workflow_name}"
    )
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_constant_level_oiler_estimator_result(
    inspection_id: str, oil_level: str, confidence: float
):
    logger.info(f"Notify CLOE result for inspection {inspection_id}")
    result = {"OilLevel": oil_level, "Confidence": confidence}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_constant_level_oiler_estimator_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    logger.info(
        f"Notify CLOE exited for inspection {inspection_id} with status {workflow_status}"
    )
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
