import logging

import typer

from workflow_notifier.notifier import app as notifier_app

logger = logging.getLogger(__name__)


def make_app() -> typer.Typer:
    logger.info("Creating typer app")
    return notifier_app
