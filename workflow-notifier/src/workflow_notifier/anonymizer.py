import logging

import typer

from workflow_notifier.notifier import notify_exited, notify_result, notify_started

logger = logging.getLogger(__name__)

WORKFLOW_TYPE = "anonymizer"

app = typer.Typer()


@app.command()
def notify_anonymizer_started(inspection_id: str, workflow_name: str):
    logger.info(
        f"Notify anonymizer started for inspection {inspection_id} and workflow {workflow_name}"
    )
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_anonymizer_result(inspection_id: str, is_person_in_image: bool = False):
    logger.info(f"Notify anonymizer result for inspection {inspection_id}")
    result = {"IsPersonInImage": is_person_in_image}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_anonymizer_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    logger.info(
        f"Notify anonymizer exited for inspection {inspection_id} with status {workflow_status}"
    )
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
