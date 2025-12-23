import typer

from workflow_notifier.anonymizer import app as anonymizer_app
from workflow_notifier.cloe import app as cloe_app
from workflow_notifier.fencilla import app as fencilla_app
from workflow_notifier.thermal_reading import app as thermal_reading_app


def make_app():
    app = typer.Typer()

    app.add_typer(anonymizer_app)
    app.add_typer(cloe_app)
    app.add_typer(fencilla_app)
    app.add_typer(thermal_reading_app)

    return app
