from workflow_notifier.notifier import app, notify_exited, notify_result, notify_started

WORKFLOW_TYPE = "thermal-reading"


@app.command()
def notify_thermal_reading_started(inspection_id: str, workflow_name: str):
    notify_started(WORKFLOW_TYPE, inspection_id, workflow_name)


@app.command()
def notify_thermal_reading_result(inspection_id: str, temperature: float):
    result = {"temperature": temperature}
    notify_result(WORKFLOW_TYPE, inspection_id, result)


@app.command()
def notify_thermal_reading_exited(
    inspection_id: str, workflow_status: str, workflow_failures: str
):
    notify_exited(WORKFLOW_TYPE, inspection_id, workflow_status, workflow_failures)
