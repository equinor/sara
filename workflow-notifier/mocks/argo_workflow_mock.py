"""Local Argo-workflow mock.

Listens for trigger requests from SARA, then asynchronously calls back into
SARA via the generic notifier (`started` / `result` / `exited`). The
per-workflow-type endpoints exist only because SARA's configuration assigns
one TriggerUrl per workflow type; the dispatch logic itself is generic.
"""

import json
import random
import threading
import time
from typing import Any, Optional

import typer
from flask import Flask, jsonify, request
from typer.testing import CliRunner

from workflow_notifier.notifier import app as notifier_app

flask_app = Flask(__name__)
_runner = CliRunner()


def _invoke_notifier(args: list[str]) -> None:
    """Drive the Typer CLI in-process. Mirrors how Argo invokes the binary."""
    result = _runner.invoke(notifier_app, args)
    if result.exit_code != 0:
        print(f"Notifier failed (exit={result.exit_code}): {result.output}")


def _run_workflow(
    workflow_id: str,
    result_payload: dict[str, Any],
    delay_seconds: float = 2.0,
) -> None:
    """Generic mock workflow: started -> result -> exited(Succeeded)."""
    time.sleep(delay_seconds)
    _invoke_notifier(["started", workflow_id])

    time.sleep(delay_seconds)
    _invoke_notifier(["result", workflow_id, json.dumps(result_payload)])

    time.sleep(delay_seconds)
    _invoke_notifier(["exited", workflow_id, "Succeeded"])


def _extract_workflow_id(data: dict[str, Any]) -> Optional[str]:
    workflow_id = data.get("workflowId")
    if not workflow_id:
        print(f"Trigger payload missing workflowId: {data}")
    return workflow_id


@flask_app.route("/trigger-anonymizer", methods=["POST"])
def trigger_anonymizer():
    data = request.get_json()
    print(f"Received anonymizer trigger: {data}")
    workflow_id = _extract_workflow_id(data)
    if workflow_id is None:
        return jsonify({"error": "workflowId missing"}), 400

    threading.Thread(
        target=_run_workflow,
        args=(workflow_id, {"isPersonInImage": True}),
    ).start()
    return jsonify({"message": "Anonymizer triggered"}), 200


@flask_app.route("/trigger-constant-level-oiler-estimator", methods=["POST"])
def trigger_cloe():
    data = request.get_json()
    print(f"Received CLOE trigger: {data}")
    workflow_id = _extract_workflow_id(data)
    if workflow_id is None:
        return jsonify({"error": "workflowId missing"}), 400

    payload = {
        "oilLevel": str(random.uniform(0, 1)),
        "confidence": 0.95,
    }
    threading.Thread(target=_run_workflow, args=(workflow_id, payload)).start()
    return jsonify({"message": "CLOE triggered"}), 200


@flask_app.route("/trigger-fencilla", methods=["POST"])
def trigger_fencilla():
    data = request.get_json()
    print(f"Received Fencilla trigger: {data}")
    workflow_id = _extract_workflow_id(data)
    if workflow_id is None:
        return jsonify({"error": "workflowId missing"}), 400

    payload = {"isBreak": True, "confidence": 0.95}
    threading.Thread(target=_run_workflow, args=(workflow_id, payload)).start()
    return jsonify({"message": "Fencilla triggered"}), 200


@flask_app.route("/trigger-thermal-reading", methods=["POST"])
def trigger_thermal_reading():
    data = request.get_json()
    print(f"Received thermal-reading trigger: {data}")
    workflow_id = _extract_workflow_id(data)
    if workflow_id is None:
        return jsonify({"error": "workflowId missing"}), 400

    payload = {"temperature": 69}
    threading.Thread(target=_run_workflow, args=(workflow_id, payload)).start()
    return jsonify({"message": "Thermal reading triggered"}), 200


if __name__ == "__main__":
    flask_app.run(host="127.0.0.1", port=30232)
