import logging
import os
from functools import lru_cache

import requests
import typer
from azure.core.credentials import TokenCredential
from azure.identity import (
    ChainedTokenCredential,
    ClientSecretCredential,
    WorkloadIdentityCredential,
)
from opentelemetry import metrics

from workflow_notifier.config.settings import settings

logger = logging.getLogger(__name__)

# Default path where the azure-workload-identity mutating webhook projects the
# service-account token in the pod.
_DEFAULT_FEDERATED_TOKEN_FILE = "/var/run/secrets/azure/tokens/azure-identity-token"

meter = metrics.get_meter("workflow-notifier")
workflow_counter = meter.create_counter(
    "workflow_execution_count",
    description="Workflow execution status",
)

app = typer.Typer()


@lru_cache(maxsize=1)
def _get_credential() -> TokenCredential:
    """
    Build a TokenCredential.

    The set of credential types to try is configured via
    ``settings.ALLOWED_AUTH_METHODS``, an ordered list whose entries may be
    ``"WorkloadIdentity"`` and/or ``"ClientSecret"`` (case-insensitive). When
    more than one method is configured, the order determines the order inside
    the resulting ``ChainedTokenCredential``.

    In cloud (AKS with Azure Workload Identity), the standard ``AZURE_CLIENT_ID``,
    ``AZURE_TENANT_ID``, ``AZURE_FEDERATED_TOKEN_FILE`` and ``AZURE_AUTHORITY_HOST``
    environment variables are injected by the azure-workload-identity mutating
    webhook, and ``WorkloadIdentityCredential`` exchanges the projected service
    account token for an Entra ID access token.

    For local development, include ``"ClientSecret"`` in
    ``ALLOWED_AUTH_METHODS`` and provide ``NOTIFIER_CLIENT_SECRET``.
    """
    token_file_path = os.environ.get(
        "AZURE_FEDERATED_TOKEN_FILE", _DEFAULT_FEDERATED_TOKEN_FILE
    )
    client_secret = settings.NOTIFIER_CLIENT_SECRET

    credentials: list[TokenCredential] = []
    activated: list[str] = []

    allowed_methods = settings.ALLOWED_AUTH_METHODS or ["WorkloadIdentity"]

    for method in allowed_methods:
        normalized = method.strip().lower()
        if normalized == "workloadidentity":
            if os.path.exists(token_file_path):
                credentials.append(
                    WorkloadIdentityCredential(
                        tenant_id=settings.TENANT_ID,
                        client_id=settings.NOTIFIER_CLIENT_ID,
                        token_file_path=token_file_path,
                    )
                )
                activated.append("WorkloadIdentityCredential")
            else:
                logger.warning(
                    "ALLOWED_AUTH_METHODS includes 'WorkloadIdentity' but no federated "
                    f"token file found at '{token_file_path}'; skipping "
                    "WorkloadIdentityCredential."
                )
        elif normalized == "clientsecret":
            if client_secret and not client_secret.lower().startswith("fill in"):
                credentials.append(
                    ClientSecretCredential(
                        tenant_id=settings.TENANT_ID,
                        client_id=settings.NOTIFIER_CLIENT_ID,
                        client_secret=client_secret,
                    )
                )
                activated.append("ClientSecretCredential")
            else:
                logger.warning(
                    "ALLOWED_AUTH_METHODS includes 'ClientSecret' but "
                    "NOTIFIER_CLIENT_SECRET is missing/placeholder; skipping "
                    "ClientSecretCredential."
                )
        else:
            logger.warning(
                f"Unknown auth method '{method}' in ALLOWED_AUTH_METHODS; "
                "expected 'WorkloadIdentity' or 'ClientSecret'."
            )

    if not credentials:
        raise RuntimeError(
            "No usable Azure credential could be constructed from "
            "ALLOWED_AUTH_METHODS. Configure at least one of 'WorkloadIdentity' "
            "(with a federated token file present) or 'ClientSecret' (with "
            "NOTIFIER_CLIENT_SECRET set)."
        )

    if len(credentials) == 1:
        logger.info(f"Using {activated[0]} only")
        return credentials[0]

    logger.info(
        "Using ChainedTokenCredential: " + " -> ".join(activated)
    )
    return ChainedTokenCredential(*credentials)



def get_access_token() -> str:
    """
    Acquire an access token for the SARA API using azure-identity.
    """
    credential = _get_credential()
    try:
        token = credential.get_token(*settings.scopes)
    except Exception as e:
        logger.error(f"Error acquiring token: {e}")
        raise typer.Exit(1)
    return token.token


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
        workflow_counter.add(
            1,
            {
                "workflow_type": workflow_type,
                "status": workflow_status,
            },
        )
        
        meter_provider = metrics.get_meter_provider()
        if hasattr(meter_provider, "force_flush"):
            meter_provider.force_flush()

        send_authenticated_put_request(url, payload)
    except requests.exceptions.RequestException as e:
        logger.error(f"Error notifying {workflow_type} workflow exit: {e}")
        raise typer.Exit(1)
