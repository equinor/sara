from workflow_notifier.notifier import app, notify_exited, notify_result, notify_started

WORKFLOW_TYPE = "fencilla"


@app.command()
def notify_fencilla_started(inspection_id: str, workflow_name: str):
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_fencilla_result(inspection_id: str, is_break: bool, confidence: float):
    result = {"isBreak": is_break, "confidence": confidence}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_fencilla_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
