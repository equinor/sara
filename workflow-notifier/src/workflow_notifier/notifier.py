import logging

import requests
import typer
from msal import ConfidentialClientApplication

from workflow_notifier.config.settings import settings

logger = logging.getLogger(__name__)

app = typer.Typer()


def get_access_token() -> str:
    """
    Acquire an access token using MSAL.
    """
    client_app = ConfidentialClientApplication(
        client_id=settings.NOTIFIER_CLIENT_ID,
        client_credential=settings.NOTIFIER_CLIENT_SECRET,
        authority=settings.authority,
    )
    result = client_app.acquire_token_for_client(scopes=settings.scopes)

    if result is None:
        logger.error("Error acquiring token: MSAL returned None.")
        raise typer.Exit(1)

    if isinstance(result, dict) and "access_token" in result:
        return result["access_token"]

    error_message = (
        f"Error acquiring token: {result.get('error', 'Unknown')} - "
        f"{result.get('error_description', 'No description provided')}"
        if isinstance(result, dict)
        else "Error acquiring token: Unexpected result type."
    )
    logger.error(error_message)
    raise typer.Exit(1)


def send_authenticated_put_request(url: str, payload: dict) -> None:
    """
    Send an authenticated PUT request with the access token.
    """
    access_token = get_access_token()
    logger.info(f"Sending PUT request to {url} with payload: {payload}")
    headers = {"Authorization": f"Bearer {access_token}"}
    response = requests.put(url, json=payload, headers=headers)
    response.raise_for_status()


def notify_started(workflow_type: str, inspection_id: str, workflow_name: str) -> None:
    """
    Notify SARA that the workflow has started
    """

    url = f"{settings.workflow_notification_url}/{workflow_type}/started"
    payload = {"InspectionId": inspection_id, "WorkflowName": workflow_name}

    logger.info(
        f"The {workflow_type} workflow has started for inspectionId: {inspection_id}"
    )

    try:
        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        logger.error(f"Error notifying {workflow_type} workflow start: {e}")
        raise typer.Exit(1)


def notify_result(workflow_type: str, inspection_id: str, result: dict) -> None:
    """
    Notify SARA about a result from the workflow
    """
    url = f"{settings.workflow_notification_url}/{workflow_type}/result"

    payload = {"InspectionId": inspection_id}
    payload.update(result)

    logger.info(
        f"The {workflow_type} workflow notifies about result: {result}"
        f" for inspectionId: {inspection_id}"
    )

    try:
        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        logger.error(f"Error notifying {workflow_type} workflow result: {e}")
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
    url = f"{settings.workflow_notification_url}/{workflow_type}/exited"
    payload = {
        "InspectionId": inspection_id,
        "ExitHandlerWorkflowStatus": workflow_status,
        "WorkflowFailures": workflow_failures,
    }

    logger.info(
        f"The {workflow_type} workflow has exited for inspectionId: {inspection_id}"
        f" with status: {workflow_status} and failures: {workflow_failures}"
    )

    try:
        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        logger.error(f"Error notifying {workflow_type} workflow exit: {e}")
        raise typer.Exit(1)
