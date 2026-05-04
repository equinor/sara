from dotenv import load_dotenv
from pydantic import Field
from pydantic_settings import BaseSettings

load_dotenv()


class Settings(BaseSettings):
    SARA_SERVER_URL: str
    TENANT_ID: str
    NOTIFIER_CLIENT_ID: str
    NOTIFIER_CLIENT_SECRET: str
    SARA_APP_REG_SCOPE: str

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
