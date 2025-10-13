import os

import requests
import typer
from dotenv import load_dotenv
from msal import ConfidentialClientApplication

load_dotenv()

app = typer.Typer()


def get_env_or_fail(var_name: str) -> str:
    value = os.getenv(var_name)
    if not value:
        typer.echo(
            f"Error: Environment variable '{var_name}' is not set or is empty.",
            err=True,
        )
        raise typer.Exit(1)
    return value


SARA_SERVER_URL = get_env_or_fail("SARA_SERVER_URL")
TENANT_ID = get_env_or_fail("TENANT_ID")
NOTIFIER_CLIENT_ID = get_env_or_fail("NOTIFIER_CLIENT_ID")
NOTIFIER_CLIENT_SECRET = get_env_or_fail("NOTIFIER_CLIENT_SECRET")
SARA_SCOPE = get_env_or_fail("SARA_APP_REG_SCOPE")

AUTHORITY = f"https://login.microsoftonline.com/{TENANT_ID}"
SCOPES = [SARA_SCOPE]
WORKFLOW_NOTIFICATION_URL = f"{SARA_SERVER_URL}/workflow-notification"


def get_access_token() -> str:
    """
    Acquire an access token using MSAL.
    """
    if not NOTIFIER_CLIENT_ID or not NOTIFIER_CLIENT_SECRET:
        typer.echo(
            "Error: NOTIFIER_CLIENT_ID or NOTIFIER_CLIENT_SECRET is not set.", err=True
        )
        raise typer.Exit(1)

    client_app = ConfidentialClientApplication(
        client_id=NOTIFIER_CLIENT_ID,
        client_credential=NOTIFIER_CLIENT_SECRET,
        authority=AUTHORITY,
    )
    result = client_app.acquire_token_for_client(scopes=SCOPES)

    if result is None:
        typer.echo("Error acquiring token: MSAL returned None.", err=True)
        raise typer.Exit(1)

    if isinstance(result, dict) and "access_token" in result:
        return result["access_token"]

    error_message = (
        f"Error acquiring token: {result.get('error', 'Unknown')} - "
        f"{result.get('error_description', 'No description provided')}"
        if isinstance(result, dict)
        else "Error acquiring token: Unexpected result type."
    )
    typer.echo(error_message, err=True)
    raise typer.Exit(1)


def send_authenticated_put_request(url: str, payload: dict) -> None:
    """
    Send an authenticated PUT request with the access token.
    """
    access_token = get_access_token()
    headers = {"Authorization": f"Bearer {access_token}"}
    response = requests.put(url, json=payload, headers=headers)
    response.raise_for_status()


def notify_started(workflow_type: str, inspection_id: str, workflow_name: str) -> None:
    """
    Notify SARA that the workflow has started
    """

    url = f"{WORKFLOW_NOTIFICATION_URL}/{workflow_type}/started"
    payload = {"InspectionId": inspection_id, "WorkflowName": workflow_name}

    typer.echo(
        f"The {workflow_type} workflow has started for inspectionId: {inspection_id}"
    )

    try:
        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        typer.echo(f"Error notifying {workflow_type} workflow start: {e}", err=True)
        raise typer.Exit(1)


def notify_result(workflow_type: str, inspection_id: str, result: dict) -> None:
    """
    Notify SARA about a result from the workflow
    """
    url = f"{WORKFLOW_NOTIFICATION_URL}/{workflow_type}/result"
    payload = {"InspectionId": inspection_id, "Result": result}

    typer.echo(
        f"The {workflow_type} workflow notifies about result: {result}"
        f" for inspectionId: {inspection_id}"
    )

    try:
        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        typer.echo(f"Error notifying {workflow_type} workflow result: {e}", err=True)
        raise typer.Exit(1)


def notify_exited(
    workflow_type: str,
    inspection_id: str,
    workflow_status: str,
    workflow_failures: str,
) -> None:
    """
    Notify SARA that the workflow has exited
    """
    url = f"{WORKFLOW_NOTIFICATION_URL}/{workflow_type}/exited"
    payload = {
        "InspectionId": inspection_id,
        "WorkflowStatus": workflow_status,
        "WorkflowFailures": workflow_failures,
    }

    typer.echo(
        f"The {workflow_type} workflow has exited for inspectionId: {inspection_id}"
        f" with status: {workflow_status} and failures: {workflow_failures}"
    )

    try:
        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        typer.echo(f"Error notifying {workflow_type} workflow exit: {e}", err=True)
        raise typer.Exit(1)


# @app.command()
# def notify_start(inspection_id: str, workflow_name: str):
#     """
#     Notify the server about workflow start.
#     """
#     url = f"{SARA_SERVER_URL}/Workflows/notify-workflow-started"
#     payload = {"InspectionId": inspection_id, "WorkflowName": workflow_name}
#     try:
#         response = send_authenticated_put_request(url, payload)
#         typer.echo(f"Workflow started successfully: {response.json()}")
#     except requests.exceptions.RequestException as e:
#         typer.echo(f"Error notifying workflow start: {e}", err=True)
#         raise typer.Exit(1)


# @app.command()
# def notify_anonymizer_done(inspection_id: str):
#     url = f"{SARA_SERVER_URL}/Workflows/notify-anonymizer-done"
#     payload = {"InspectionId": inspection_id}
#     try:
#         response = send_authenticated_put_request(url, payload)
#         typer.echo(f"Notified that anonymizer is done with response: {response.json()}")
#     except requests.exceptions.RequestException as e:
#         typer.echo(f"Error notifying anonymizer done: {e}", err=True)
#         raise typer.Exit(1)


# @app.command()
# def notify_constant_level_oiler_done(inspection_id: str, oil_level: str):
#     url = f"{SARA_SERVER_URL}/Workflows/notify-constant-level-oiler-done"
#     payload = {"InspectionId": inspection_id, "OilLevel": oil_level}
#     try:
#         response = send_authenticated_put_request(url, payload)
#         typer.echo(
#             "Notified that constant level oiler is done"
#             f"with response: {response.json()}"
#         )
#     except requests.exceptions.RequestException as e:
#         typer.echo(f"Error notifying constant level oiler done: {e}", err=True)
#         raise typer.Exit(1)


# @app.command()
# def notify_fencilla_done(inspection_id: str, is_break: bool, confidence: float):
#     url = f"{SARA_SERVER_URL}/Workflows/notify-fencilla-done"
#     payload = {
#         "InspectionId": inspection_id,
#         "IsBreak": is_break,
#         "Confidence": confidence,
#     }
#     try:
#         response = send_authenticated_put_request(url, payload)
#     except requests.exceptions.RequestException as e:
#         typer.echo(f"Error notifying fencilla done: {e}", err=True)
#         raise typer.Exit(1)
#     typer.echo(
#         "Notified that fencilla is done" f"with response: {response.status_code}"
#     )


# @app.command()
# def notify_exit(
#     inspection_id: str,
#     workflow_status: str,
#     workflow_failures: str,
# ):
#     url = f"{SARA_SERVER_URL}/Workflows/notify-workflow-exited"
#     payload = {
#         "InspectionId": inspection_id,
#         "WorkflowStatus": workflow_status,
#         "WorkflowFailures": workflow_failures,
#     }
#     typer.echo(f"Sending payload: {payload}")

#     try:
#         response = send_authenticated_put_request(url, payload)
#     except requests.exceptions.RequestException as e:
#         typer.echo(f"Error notifying workflow exit: {e}", err=True)
#         raise typer.Exit(1)

#     typer.echo(f"Workflow exited successfully: {response.status_code}")
