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
/trigger-stid-upload
                            Notify started
 
                            Notify exit

"""


@app.route("/trigger-stid-upload", methods=["POST"])
def trigger_stid_upload():
    try:
        # Parse the input JSON
        data = request.get_json()
        print(data)
        inspection_id = data.get("inspectionId")
        anonymized_blob_storage_location = data.get("anonymizedBlobStorageLocation")
        tag = data.get("tagId")
        description = data.get("description")

        # Validate input
        if (
            not inspection_id
            or not anonymized_blob_storage_location
            or not tag
            or not description
        ):
            print("Missing required fields")
            return jsonify({"error": "Missing required fields"}), 400

        print(f"Received trigger request: {data}")

        # Start the workflow notifications in a separate thread
        threading.Thread(
            target=start_workflow,
            args=(inspection_id, anonymized_blob_storage_location, tag, description),
        ).start()

        return jsonify({"message": "Trigger request received"}), 200
    except Exception as e:
        print(f"Error in /trigger-analysis: {e}")
        return jsonify({"error": "An error occurred"}), 500


def start_workflow(
    inspection_id: str, anonymized_blob_storage_location, tag: str, description: str
):
    try:
        workflow_name = f"workflow-{random.randint(1000, 9999)}"
        print(
            f"Starting workflow for inspectionId: {inspection_id} with workflowName: {workflow_name}"
        )

        # Notify workflow started after 10 seconds
        time.sleep(5)
        notify_workflow_started(inspection_id, workflow_name)

        # Notify workflow exited after another 10 seconds
        time.sleep(5)
        notify_workflow_exited(inspection_id, 10320, "Succeeded")
    except Exception as e:
        print(f"Error in start_workflow: {e}")


def notify_workflow_started(inspection_id, workflow_name):
    try:
        url = "http://localhost:8100/StidWorkflow/notify-workflow-started"
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


def notify_workflow_exited(inspection_id, stid_media_id, workflow_status):
    try:
        url = "http://localhost:8100/StidWorkflow/notify-workflow-exited"
        # workflow_status = "Succeded" if random.random() > 0.3 else "Failed"
        workflow_status = "Succeeded"
        payload = {
            "inspectionId": inspection_id,
            "stidMediaId": stid_media_id,
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
    app.run(host="127.0.0.1", port=30233)
