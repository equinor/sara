import os
import typer
import requests
from msal import ConfidentialClientApplication

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

SCOPES = [SARA_SCOPE]
AUTHORITY = f"https://login.microsoftonline.com/{TENANT_ID}"

# Should preferably only be set to true in local environment.
# Validating the HTTPS certificate can prevent man-in-the-middle attacks.
SKIP_VALIDATE_HTTPS_CERT_SARA = (
    os.getenv("SKIP_VALIDATE_HTTPS_CERT_IDA", "").lower() == "true"
)
VALIDATE_HTTPS_CERT_SARA = not SKIP_VALIDATE_HTTPS_CERT_SARA

if SKIP_VALIDATE_HTTPS_CERT_SARA:
    typer.echo(
        "Warning: Skipping HTTPS certificate validation for SARA server.", err=False
    )


def get_access_token() -> str:
    """
    Acquire an access token using MSAL.
    """
    if not NOTIFIER_CLIENT_ID or not NOTIFIER_CLIENT_SECRET:
        typer.echo(
            "Error: NOTIFIER_CLIENT_ID or NOTIFIER_CLIENT_SECRET is not set.", err=True
        )
        raise typer.Exit(1)

    app = ConfidentialClientApplication(
        client_id=NOTIFIER_CLIENT_ID,
        client_credential=NOTIFIER_CLIENT_SECRET,
        authority=AUTHORITY,
    )
    result = app.acquire_token_for_client(scopes=SCOPES)

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


def send_authenticated_put_request(url: str, payload: dict) -> requests.Response:
    """
    Send an authenticated PUT request with the access token.
    """
    access_token = get_access_token()
    headers = {"Authorization": f"Bearer {access_token}"}
    response = requests.put(
        url, json=payload, headers=headers, verify=VALIDATE_HTTPS_CERT_SARA
    )
    response.raise_for_status()
    return response


@app.command()
def notify_start(inspection_id: str, workflow_name: str):
    """
    Notify the server about workflow start.
    """
    url = f"{SARA_SERVER_URL}/Workflows/notify-workflow-started"
    payload = {"InspectionId": inspection_id, "WorkflowName": workflow_name}
    try:
        response = send_authenticated_put_request(url, payload)
        typer.echo(f"Workflow started successfully: {response.json()}")
    except requests.exceptions.RequestException as e:
        typer.echo(f"Error notifying workflow start: {e}", err=True)
        raise typer.Exit(1)


@app.command()
def notify_exit(inspection_id: str, workflow_status: str):
    """
    Notify the server about workflow exit.
    """
    url = f"{SARA_SERVER_URL}/Workflows/notify-workflow-exited"
    payload = {"InspectionId": inspection_id, "WorkflowStatus": workflow_status}
    try:
        response = send_authenticated_put_request(url, payload)
        typer.echo(f"Workflow exited successfully: {response.json()}")
    except requests.exceptions.RequestException as e:
        typer.echo(f"Error notifying workflow exit: {e}", err=True)
        raise typer.Exit(1)


if __name__ == "__main__":
    app()
