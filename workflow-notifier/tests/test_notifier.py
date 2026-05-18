import json
from unittest.mock import patch
from uuid import uuid4

import pytest
import requests
import requests_mock
import typer
from typer.testing import CliRunner

from workflow_notifier.config.settings import settings
from workflow_notifier.notifier import app

runner = CliRunner()
WORKFLOW_ID = uuid4()


@pytest.fixture(autouse=True)
def fake_token():
    """Bypass MSAL during tests."""
    with patch(
        "workflow_notifier.notifier.get_access_token", return_value="fake-token"
    ):
        yield


@pytest.fixture
def mock_http():
    with requests_mock.Mocker() as m:
        yield m


def test_started_sends_put_with_no_body(mock_http: requests_mock.Mocker):
    url = f"{settings.workflow_base_url}/{WORKFLOW_ID}/started"
    mock_http.put(url, status_code=204)

    result = runner.invoke(app, ["started", str(WORKFLOW_ID)])

    assert result.exit_code == 0
    assert mock_http.last_request.text in (None, "null")
    assert mock_http.last_request.headers["Authorization"] == "Bearer fake-token"


def test_result_sends_payload_verbatim(mock_http: requests_mock.Mocker):
    url = f"{settings.workflow_base_url}/{WORKFLOW_ID}/result"
    mock_http.put(url, status_code=204)

    payload = '{"isPersonInImage": true, "nested": {"a": 1}}'
    result = runner.invoke(app, ["result", str(WORKFLOW_ID), payload])

    assert result.exit_code == 0
    body = mock_http.last_request.json()
    assert body == {"resultJson": payload}
    assert body["resultJson"] == payload


def test_exited_succeeded_omits_error_message(mock_http: requests_mock.Mocker):
    url = f"{settings.workflow_base_url}/{WORKFLOW_ID}/exited"
    mock_http.put(url, status_code=204)

    result = runner.invoke(app, ["exited", str(WORKFLOW_ID), "Succeeded"])

    assert result.exit_code == 0
    assert mock_http.last_request.json() == {"exitStatus": "Succeeded"}


def test_exited_failed_with_error_message(mock_http: requests_mock.Mocker):
    url = f"{settings.workflow_base_url}/{WORKFLOW_ID}/exited"
    mock_http.put(url, status_code=204)

    result = runner.invoke(
        app,
        ["exited", str(WORKFLOW_ID), "Failed", "boom"],
    )

    assert result.exit_code == 0
    assert mock_http.last_request.json() == {
        "exitStatus": "Failed",
        "errorMessage": "boom",
    }


def test_exited_failed_accepts_failures_array(mock_http: requests_mock.Mocker):
    url = f"{settings.workflow_base_url}/{WORKFLOW_ID}/exited"
    mock_http.put(url, status_code=204)
    failures = '[{"displayName":"step","message":"oops"}]'

    result = runner.invoke(app, ["exited", str(WORKFLOW_ID), "Failed", failures])

    assert result.exit_code == 0
    assert mock_http.last_request.json() == {
        "exitStatus": "Failed",
        "errorMessage": failures,
    }


def test_invalid_workflow_id_rejected_before_http():
    result = runner.invoke(app, ["started", "not-a-uuid"])
    assert result.exit_code != 0
    assert "Invalid value" in result.output or "UUID" in result.output


def test_invalid_result_json_rejected_before_http():
    result = runner.invoke(app, ["result", str(WORKFLOW_ID), "not json {"])
    assert result.exit_code != 0
    assert "JSON" in result.output or "json" in result.output


def test_invalid_exit_status_rejected():
    result = runner.invoke(app, ["exited", str(WORKFLOW_ID), "Bogus"])
    assert result.exit_code != 0


def test_http_error_propagates_as_exit_code(mock_http: requests_mock.Mocker):
    url = f"{settings.workflow_base_url}/{WORKFLOW_ID}/started"
    mock_http.put(url, status_code=500)

    result = runner.invoke(app, ["started", str(WORKFLOW_ID)])
    assert result.exit_code == 1


def test_workflow_url_uses_api_prefix():
    assert settings.workflow_base_url.endswith("/api/workflow")
