from typing import Optional

from dotenv import load_dotenv
from pydantic import Field
from pydantic_settings import BaseSettings

load_dotenv()


class Settings(BaseSettings):
    SARA_SERVER_URL: str
    TENANT_ID: str
    NOTIFIER_CLIENT_ID: str
    SARA_APP_REG_SCOPE: str

    # Optional client secret for local development. In cloud (AKS with Azure
    # Workload Identity) this is not provided and the federated token file is
    # used instead. Include "ClientSecret" in ALLOWED_AUTH_METHODS to enable
    # the ClientSecretCredential path.
    NOTIFIER_CLIENT_SECRET: Optional[str] = Field(default=None)

    # Ordered list of credential types that may be used to acquire an Azure
    # AD access token. Allowed entries (case-insensitive): "WorkloadIdentity",
    # "ClientSecret". When more than one method is configured, the order
    # determines the order inside the resulting ChainedTokenCredential.
    # When provided via environment variables, supply a comma-separated
    # value (e.g. ALLOWED_AUTH_METHODS=WorkloadIdentity,ClientSecret).
    ALLOWED_AUTH_METHODS: list[str] = Field(default_factory=lambda: ["WorkloadIdentity"])

    OTEL_SERVICE_NAME: str = Field(default="workflow-notifier")
    OTEL_EXPORTER_OTLP_ENDPOINT: str = Field(default="http://localhost:4317")
    OTEL_EXPORTER_OTLP_PROTOCOL: str = Field(default="grpc")

    OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE: str = Field(default="DELTA")

    @property
    def authority(self) -> str:
        return f"https://login.microsoftonline.com/{self.TENANT_ID}"

    @property
    def scopes(self) -> list[str]:
        return [self.SARA_APP_REG_SCOPE]

    @property
    def workflow_notification_url(self) -> str:
        return f"{self.SARA_SERVER_URL}/workflow-notification"


settings = Settings()
