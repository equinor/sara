import json
import logging
import os
from enum import Enum
from functools import lru_cache
from typing import Optional
from uuid import UUID

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


class WorkflowExitStatus(str, Enum):
    Succeeded = "Succeeded"
    Failed = "Failed"


@lru_cache(maxsize=1)
def _get_credential() -> TokenCredential:
    """
    Build a TokenCredential.

    The set of credential types to try is configured via
    ``settings.allowed_auth_methods``, an ordered list whose entries may be
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

    allowed_methods = settings.allowed_auth_methods or ["WorkloadIdentity"]

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

    logger.info("Using ChainedTokenCredential: " + " -> ".join(activated))
    return ChainedTokenCredential(*credentials)


def get_access_token() -> str:
    """Acquire an access token for the SARA API using azure-identity."""
    credential = _get_credential()
    try:
        token = credential.get_token(*settings.scopes)
    except Exception as e:
        logger.error(f"Error acquiring token: {e}")
        raise typer.Exit(1)
    return token.token


def _send_authenticated_put(url: str, payload: Optional[dict]) -> None:
    """Send an authenticated PUT request and raise on non-2xx responses."""
    access_token = get_access_token()
    logger.info(f"Sending PUT request to {url} with payload: {payload}")
    headers = {"Authorization": f"Bearer {access_token}"}
    response = requests.put(url, json=payload, headers=headers)
    response.raise_for_status()


def _workflow_url(workflow_id: UUID, suffix: str) -> str:
    return f"{settings.workflow_base_url}/{workflow_id}/{suffix}"


def _validate_result_json(value: str) -> str:
    """Typer callback: ensure result_json is parseable JSON; return verbatim."""
    try:
        json.loads(value)
    except (json.JSONDecodeError, TypeError) as exc:
        raise typer.BadParameter(f"result_json must be valid JSON: {exc}")
    return value


@app.command()
def started(
    workflow_id: UUID = typer.Argument(...),
) -> None:
    """Notify SARA that the workflow has started executing."""
    url = _workflow_url(workflow_id, "started")
    logger.info(f"Workflow {workflow_id} reporting started")
    try:
        _send_authenticated_put(url, payload=None)
    except requests.exceptions.RequestException as exc:
        logger.error(f"Error notifying workflow {workflow_id} start: {exc}")
        raise typer.Exit(1)


@app.command()
def result(
    workflow_id: UUID = typer.Argument(...),
    result_json: str = typer.Argument(..., callback=_validate_result_json),
) -> None:
    """Forward the workflow's result payload to SARA verbatim as a JSON string."""
    url = _workflow_url(workflow_id, "result")
    logger.info(f"Workflow {workflow_id} reporting result ({len(result_json)} bytes)")
    try:
        _send_authenticated_put(url, payload={"resultJson": result_json})
    except requests.exceptions.RequestException as exc:
        logger.error(f"Error notifying workflow {workflow_id} result: {exc}")
        raise typer.Exit(1)


@app.command()
def exited(
    workflow_id: UUID = typer.Argument(...),
    exit_status: WorkflowExitStatus = typer.Argument(...),
    error_message: Optional[str] = typer.Argument(
        None,
        help="Error detail; only meaningful when exit_status is Failed.",
    ),
) -> None:
    """Notify SARA that the workflow has exited with the given status."""
    url = _workflow_url(workflow_id, "exited")
    payload: dict = {"exitStatus": exit_status.value}
    if error_message is not None:
        payload["errorMessage"] = error_message

    logger.info(
        f"Workflow {workflow_id} reporting exit: status={exit_status.value}"
        + (f", errorMessage={error_message!r}" if error_message else "")
    )

    try:
        workflow_counter.add(
            1,
            {
                "status": exit_status.value,
                "workflow_id": str(workflow_id),
            },
        )

        meter_provider = metrics.get_meter_provider()
        if hasattr(meter_provider, "force_flush"):
            meter_provider.force_flush()

        _send_authenticated_put(url, payload=payload)
    except requests.exceptions.RequestException as exc:
        logger.error(f"Error notifying workflow {workflow_id} exit: {exc}")
        raise typer.Exit(1)
