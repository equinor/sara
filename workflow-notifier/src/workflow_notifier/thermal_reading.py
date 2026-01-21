import logging

from workflow_notifier.notifier import app, notify_exited, notify_result, notify_started

logger = logging.getLogger(__name__)

WORKFLOW_TYPE = "thermal-reading"


@app.command()
def notify_thermal_reading_started(inspection_id: str, workflow_name: str):
    logger.info(
        f"Notify thermal reading started for inspection {inspection_id} and workflow {workflow_name}"
    )
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_thermal_reading_result(inspection_id: str, temperature: float):
    logger.info(f"Notify thermal reading result for inspection {inspection_id}")
    result = {"temperature": temperature}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_thermal_reading_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    logger.info(
        f"Notify thermal reading exited for inspection {inspection_id} with status {workflow_status}"
    )
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
