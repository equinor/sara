import random
import threading
import time
from typing import List

import requests
from flask import Flask, jsonify, request

app = Flask(__name__)

"""
Flow diagram

SARA                        Workflow                    Anonymization       Constant Level Oiler
/trigger-analysis
                            Notify started
                            (Notify anon started)
                                                        Run-Anon
                            Notify anon done
/anonymization-available
                            (Notify CLO started)
                                                                            Run-CLO
                            Notify CLO done
/clo-available
                            Notify exit

"""


@app.route("/trigger-analysis", methods=["POST"])
def trigger_analysis():
    try:
        # Parse the input JSON
        data = request.get_json()
        inspection_id = data.get("inspectionId")
        raw_data_blob_storage_location = data.get("rawDataBlobStorageLocation")
        anonymized_blob_storage_location = data.get("anonymizedBlobStorageLocation")
        visualized_blob_storage_location = data.get("visualizedBlobStorageLocation")
        should_run_constant_level_oiler = data.get("shouldRunConstantLevelOiler")
        should_run_fencilla = data.get("shouldRunFencilla")

        # Validate input
        if (
            not inspection_id
            or not raw_data_blob_storage_location
            or not anonymized_blob_storage_location
            or not visualized_blob_storage_location
            or should_run_constant_level_oiler is None
            or should_run_fencilla is None
        ):
            print("Missing required fields")
            return jsonify({"error": "Missing required fields"}), 400

        print(f"Received trigger request: {data}")

        # Start the workflow notifications in a separate thread
        threading.Thread(
            target=start_workflow,
            args=(inspection_id, should_run_constant_level_oiler, should_run_fencilla),
        ).start()

        return jsonify({"message": "Trigger request received"}), 200
    except Exception as e:
        print(f"Error in /trigger-analysis: {e}")
        return jsonify({"error": "An error occurred"}), 500


def start_workflow(inspection_id, should_run_constant_level_oiler, should_run_fencilla):
    try:
        workflow_name = f"workflow-{random.randint(1000, 9999)}"
        print(
            f"Starting workflow for inspectionId: {inspection_id} with workflowName: {workflow_name}"
        )

        # Notify workflow started after 10 seconds
        time.sleep(5)
        notify_workflow_started(inspection_id, workflow_name)

        # Notify anonymizer done after 10 seconds
        time.sleep(5)
        notify_anonymizer_done(inspection_id)

        time.sleep(5)
        if should_run_constant_level_oiler:
            # Notify CLO done after 10 seconds
            oil_level = 0.777
            notify_constant_level_oiler_done(inspection_id, oil_level)

        time.sleep(5)
        if should_run_fencilla:
            # Notify Fencilla done after 10 seconds
            is_break = True
            confidence = 0.95
            notify_fencilla_done(inspection_id, is_break, confidence)

        # Notify workflow exited after another 10 seconds
        time.sleep(5)
        notify_workflow_exited(inspection_id)
    except Exception as e:
        print(f"Error in start_workflow: {e}")


def notify_workflow_started(inspection_id, workflow_name):
    try:
        url = "http://localhost:8100/Workflows/notify-workflow-started"
        payload = {"inspectionId": inspection_id, "workflowName": workflow_name}
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Workflow started notification sent successfully.")
        else:
            print(
                f"Failed to notify workflow started. Response {response.status_code}: {response.text}"
            )
    except Exception as e:
        print(f"Error in notify_workflow_started: {e}")


def notify_anonymizer_done(inspection_id):
    try:
        url = "http://localhost:8100/Workflows/notify-anonymizer-done"
        payload = {"inspectionId": inspection_id}
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Anonymizer done notification sent successfully.")
        else:
            print(
                f"Failed to notify anonymizer done. Response {response.status_code}: {response.text}"
            )
    except Exception as e:
        print(f"Error in notify_anonymizer_done: {e}")


def notify_constant_level_oiler_done(inspection_id, oil_level):
    try:
        url = "http://localhost:8100/Workflows/notify-constant-level-oiler-done"
        payload = {"inspectionId": inspection_id, "oilLevel": oil_level}
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Constant Level Oiler done notification sent successfully.")
        else:
            print(
                f"Failed to notify Constant Level Oiler done. Response {response.status_code}: {response.text}"
            )
    except Exception as e:
        print(f"Error in notify_constant_level_oiler_done: {e}")


def notify_fencilla_done(inspection_id: str, is_break: bool, confidence: float):
    try:
        url = "http://localhost:8100/Workflows/notify-fencilla-done"
        payload = {
            "inspectionId": inspection_id,
            "isBreak": is_break,
            "confidence": confidence,
        }
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Fencilla done notification sent successfully.")
        else:
            print(
                f"Failed to notify Fencilla done. Response {response.status_code}: {response.text}"
            )
    except Exception as e:
        print(f"Error in notify_fencilla_done: {e}")


def notify_workflow_exited(inspection_id):
    try:
        url = "http://localhost:8100/Workflows/notify-workflow-exited"
        # workflow_status = "Succeded" if random.random() > 0.3 else "Failed"
        workflow_status = "Succeeded"
        payload = {
            "inspectionId": inspection_id,
            "workflowStatus": workflow_status,
            "workflowFailures": "Success",
        }
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Workflow exited notification sent successfully.")
        else:
            print(
                f"Failed to notify workflow exited. Response {response.status_code}: {response.text}"
            )
    except Exception as e:
        print(f"Error in notify_workflow_exited: {e}")


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=30232)
