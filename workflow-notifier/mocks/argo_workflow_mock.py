import random
import threading
import time

from flask import Flask, jsonify, request

from workflow_notifier import commands

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

        time.sleep(5)
        commands.notify_start(inspection_id, workflow_name)

        time.sleep(5)
        commands.notify_anonymizer_done(inspection_id)

        time.sleep(5)
        if should_run_constant_level_oiler:
            oil_level = "0.777"
            commands.notify_constant_level_oiler_done(inspection_id, oil_level)

        time.sleep(5)
        if should_run_fencilla:
            is_break = True
            confidence = 0.95
            commands.notify_fencilla_done(inspection_id, is_break, confidence)

        time.sleep(5)
        commands.notify_exit(inspection_id, "Succeeded", "")
    except Exception as e:
        print(f"Error in start_workflow: {e}")


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=30232)
