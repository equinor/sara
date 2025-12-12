import threading
import time

from flask import Flask, jsonify, request
from pydantic import BaseModel

from workflow_notifier import anonymizer, cloe, fencilla, thermal_reading

app = Flask(__name__)


class BlobStorageLocation(BaseModel):
    storageAccount: str
    blobContainer: str
    blobName: str


class TriggerAnonymizerRequest(BaseModel):
    inspectionId: str
    rawDataBlobStorageLocation: BlobStorageLocation
    anonymizedBlobStorageLocation: BlobStorageLocation


@app.route("/trigger-anonymizer", methods=["POST"])
def trigger_anonymizer():
    try:
        data = request.get_json()
        print(f"Received trigger request for anonymizer: {data}")

        inspection_id = data.get("inspectionId")
        raw_data_blob_storage_location = data.get("rawDataBlobStorageLocation")
        anonymized_blob_storage_location = data.get("anonymizedBlobStorageLocation")

        trigger_anonymizer_request = TriggerAnonymizerRequest(
            inspectionId=inspection_id,
            rawDataBlobStorageLocation=raw_data_blob_storage_location,
            anonymizedBlobStorageLocation=anonymized_blob_storage_location,
        )

        threading.Thread(
            target=start_anonymizer_workflow, args=(trigger_anonymizer_request,)
        ).start()
        return jsonify({"message": "Trigger request for anonymizer handled"}), 200
    except Exception as e:
        print(f"Error in /trigger-anonymizer: {e}")
        return jsonify({"error": "An error occurred"}), 500


def start_anonymizer_workflow(trigger_anonymizer_request: TriggerAnonymizerRequest):
    workflow_name = f"workflow-{trigger_anonymizer_request.inspectionId}"
    print(
        f"Starting anonymizer workflow for inspectionId: {trigger_anonymizer_request.inspectionId}"
        f" with workflowName: {workflow_name}"
    )

    time.sleep(2)
    anonymizer.notify_anonymizer_started(
        trigger_anonymizer_request.inspectionId, workflow_name
    )

    # Mock Anonymizer
    time.sleep(2)
    is_person_in_images = True

    anonymizer.notify_anonymizer_result(
        trigger_anonymizer_request.inspectionId, is_person_in_images
    )

    time.sleep(2)
    anonymizer.notify_anonymizer_exited(
        trigger_anonymizer_request.inspectionId, "Succeeded", ""
    )


class TriggerConstantLevelOilerEstimatorRequest(BaseModel):
    inspectionId: str
    sourceBlobStorageLocation: BlobStorageLocation
    visualizedBlobStorageLocation: BlobStorageLocation


@app.route("/trigger-constant-level-oiler-estimator", methods=["POST"])
def trigger_constant_level_oiler_estimator():
    try:
        data = request.get_json()
        print(f"Received trigger request for constant level oiler estimator: {data}")

        inspection_id = data.get("inspectionId")
        source_blob_storage_location = data.get("sourceBlobStorageLocation")
        visualized_blob_storage_location = data.get("visualizedBlobStorageLocation")

        trigger_constant_level_oiler_estimator_request = (
            TriggerConstantLevelOilerEstimatorRequest(
                inspectionId=inspection_id,
                sourceBlobStorageLocation=source_blob_storage_location,
                visualizedBlobStorageLocation=visualized_blob_storage_location,
            )
        )

        threading.Thread(
            target=start_constant_level_oiler_estimator_workflow,
            args=(trigger_constant_level_oiler_estimator_request,),
        ).start()
        return (
            jsonify({"message": "Trigger request for constant level oiler handled"}),
            200,
        )
    except Exception as e:
        print(f"Error in /trigger-constant-level-oiler-estimator: {e}")
        return jsonify({"error": "An error occurred"}), 500


def start_constant_level_oiler_estimator_workflow(
    trigger_constant_level_oiler_estimator_request: TriggerConstantLevelOilerEstimatorRequest,
):
    workflow_name = (
        f"workflow-{trigger_constant_level_oiler_estimator_request.inspectionId}"
    )
    print(
        f"Starting constant level oiler workflow for inspectionId: {trigger_constant_level_oiler_estimator_request.inspectionId}"
        f" with workflowName: {workflow_name}"
    )

    time.sleep(2)
    cloe.notify_constant_level_oiler_estimator_started(
        trigger_constant_level_oiler_estimator_request.inspectionId, workflow_name
    )

    # Mock Constant Level Oiler
    time.sleep(2)
    oil_level = "0.777"

    cloe.notify_constant_level_oiler_estimator_result(
        trigger_constant_level_oiler_estimator_request.inspectionId, oil_level
    )

    time.sleep(2)
    cloe.notify_constant_level_oiler_estimator_exited(
        trigger_constant_level_oiler_estimator_request.inspectionId, "Succeeded", ""
    )


class TriggerFencillaRequest(BaseModel):
    inspectionId: str
    sourceBlobStorageLocation: BlobStorageLocation
    visualizedBlobStorageLocation: BlobStorageLocation


@app.route("/trigger-fencilla", methods=["POST"])
def trigger_fencilla():
    try:
        data = request.get_json()
        print(f"Received trigger request for fencilla: {data}")

        inspection_id = data.get("inspectionId")
        source_blob_storage_location = data.get("sourceBlobStorageLocation")
        visualized_blob_storage_location = data.get("visualizedBlobStorageLocation")

        trigger_fencilla_request = TriggerFencillaRequest(
            inspectionId=inspection_id,
            sourceBlobStorageLocation=source_blob_storage_location,
            visualizedBlobStorageLocation=visualized_blob_storage_location,
        )

        threading.Thread(
            target=start_fencilla_workflow,
            args=(trigger_fencilla_request,),
        ).start()
        return (
            jsonify({"message": "Trigger request for fencilla handled"}),
            200,
        )
    except Exception as e:
        print(f"Error in /trigger-fencilla: {e}")
        return jsonify({"error": "An error occurred"}), 500


def start_fencilla_workflow(trigger_fencilla_request: TriggerFencillaRequest):
    workflow_name = f"workflow-{trigger_fencilla_request.inspectionId}"
    print(
        f"Starting fencilla workflow for inspectionId: {trigger_fencilla_request.inspectionId}"
        f" with workflowName: {workflow_name}"
    )

    time.sleep(2)
    fencilla.notify_fencilla_started(
        trigger_fencilla_request.inspectionId, workflow_name
    )

    # Mock Fencilla
    time.sleep(2)
    is_break = True
    confidence = 0.95

    fencilla.notify_fencilla_result(
        trigger_fencilla_request.inspectionId, is_break, confidence
    )

    time.sleep(2)
    fencilla.notify_fencilla_exited(
        trigger_fencilla_request.inspectionId, "Succeeded", ""
    )


class TriggerThermalReadingRequest(BaseModel):
    inspectionId: str
    tagId: str
    inspectionDescription: str
    installationCode: str
    sourceBlobStorageLocation: BlobStorageLocation
    visualizedBlobStorageLocation: BlobStorageLocation


@app.route("/trigger-thermal-reading", methods=["POST"])
def trigger_thermal_reading():
    try:
        data = request.get_json()
        print(f"Received trigger request for thermal reading: {data}")

        inspectionId = data.get("inspectionId")
        tagId = data.get("tagId")
        inspectionDescription = data.get("inspectionDescription")
        installationCode = data.get("installationCode")
        source_blob_storage_location = data.get("sourceBlobStorageLocation")
        visualized_blob_storage_location = data.get("visualizedBlobStorageLocation")

        trigger_thermal_reading_request = TriggerThermalReadingRequest(
            inspectionId=inspectionId,
            tagId=tagId,
            inspectionDescription=inspectionDescription,
            installationCode=installationCode,
            sourceBlobStorageLocation=source_blob_storage_location,
            visualizedBlobStorageLocation=visualized_blob_storage_location,
        )

        threading.Thread(
            target=start_thermal_reading_workflow,
            args=(trigger_thermal_reading_request,),
        ).start()
        return (
            jsonify({"message": "Trigger request for thermal reading handled"}),
            200,
        )
    except Exception as e:
        print(f"Error in /trigger-thermal-reading: {e}")
        return jsonify({"error": "An error occurred"}), 500


def start_thermal_reading_workflow(
    trigger_thermal_reading_request: TriggerThermalReadingRequest,
):
    workflow_name = f"workflow-{trigger_thermal_reading_request.inspectionId}"
    print(
        f"Starting thermal reading workflow for inspectionId: {trigger_thermal_reading_request.inspectionId}"
        f" with workflowName: {workflow_name}"
    )

    time.sleep(2)
    thermal_reading.notify_thermal_reading_started(
        trigger_thermal_reading_request.inspectionId, workflow_name
    )

    # Mock Thermal Reading
    time.sleep(2)
    temperature = 69

    thermal_reading.notify_thermal_reading_result(
        trigger_thermal_reading_request.inspectionId, temperature
    )

    time.sleep(2)
    thermal_reading.notify_thermal_reading_exited(
        trigger_thermal_reading_request.inspectionId, "Succeeded", ""
    )


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=30232)
