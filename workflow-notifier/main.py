from workflow_notifier.app import make_app
from workflow_notifier.config.logger import setup_logger
from workflow_notifier.config.open_telemetry import setup_open_telemetry

if __name__ == "__main__":
    setup_logger()
    setup_open_telemetry()
    app = make_app()

    app()
