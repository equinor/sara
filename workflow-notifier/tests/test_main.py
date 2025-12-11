import typer

from workflow_notifier.app import make_app


def test_main():
    app = make_app()

    assert isinstance(app, typer.Typer)
    assert callable(app)
