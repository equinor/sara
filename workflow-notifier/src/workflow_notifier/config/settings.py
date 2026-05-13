from typing import Annotated, Optional

from dotenv import load_dotenv
from pydantic import BeforeValidator, Field
from pydantic_settings import BaseSettings


def _parse_comma_separated(value: object) -> list[str]:
    """Accept a comma-separated string **or** an existing list and return a list of stripped, non-empty strings."""
    if isinstance(value, str):
        return [item.strip() for item in value.split(",") if item.strip()]
    return value  # type: ignore[return-value]


load_dotenv()


class Settings(BaseSettings):
    SARA_SERVER_URL: str
    TENANT_ID: str
    NOTIFIER_CLIENT_ID: str
    SARA_APP_REG_SCOPE: str

    # Optional client secret for local development.
    NOTIFIER_CLIENT_SECRET: Optional[str] = Field(default=None)

    # In environment variables or ConfigMaps, supply a comma-separated string
    # (e.g. ALLOWED_AUTH_METHODS=WorkloadIdentity,ClientSecret).
    ALLOWED_AUTH_METHODS: Annotated[
        list[str], BeforeValidator(_parse_comma_separated)
    ] = Field(default_factory=lambda: ["WorkloadIdentity"])

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
