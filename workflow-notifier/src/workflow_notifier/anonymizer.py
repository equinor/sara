from workflow_notifier.notifier import app, notify_exited, notify_result, notify_started

WORKFLOW_TYPE = "anonymizer"


@app.command()
def notify_anonymizer_started(inspection_id: str, workflow_name: str):
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_anonymizer_result(inspection_id: str, is_person_in_image: bool = False):
    result = {"IsPersonInImage": is_person_in_image}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_anonymizer_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
