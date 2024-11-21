from flask import Flask, request, jsonify
import requests
import threading
import time
import random

app = Flask(__name__)

@app.route('/trigger-anonymizer', methods=['POST'])
def trigger_anonymizer():
    try:
        # Parse the input JSON
        data = request.get_json()
        inspection_id = data.get('inspectionId')
        raw_data_uri = data.get('rawDataUri')
        anonymized_uri = data.get('anonymizedUri')

        # Validate input
        if not inspection_id or not raw_data_uri or not anonymized_uri:
            return jsonify({"error": "Missing required fields"}), 400

        print(f"Received trigger request: {data}")

        # Start the workflow notifications in a separate thread
        threading.Thread(target=start_workflow, args=(inspection_id,)).start()

        return jsonify({"message": "Trigger request received"}), 200
    except Exception as e:
        print(f"Error in /trigger-anonymizer: {e}")
        return jsonify({"error": "An error occurred"}), 500

def start_workflow(inspection_id):
    try:
        workflow_name = f"workflow-{random.randint(1000, 9999)}"
        print(f"Starting workflow for inspectionId: {inspection_id} with workflowName: {workflow_name}")

        # Notify workflow started after 10 seconds
        time.sleep(10)
        notify_workflow_started(inspection_id, workflow_name)

        # Notify workflow exited after another 10 seconds
        time.sleep(10)
        notify_workflow_exited(inspection_id)
    except Exception as e:
        print(f"Error in start_workflow: {e}")

def notify_workflow_started(inspection_id, workflow_name):
    try:
        url = "https://localhost:8100/Workflows/notify-workflow-started"
        payload = {
            "inspectionId": inspection_id,
            "workflowName": workflow_name
        }
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Workflow started notification sent successfully.")
        else:
            print(f"Failed to notify workflow started: {response.text}")
    except Exception as e:
        print(f"Error in notify_workflow_started: {e}")

def notify_workflow_exited(inspection_id):
    try:
        url = "https://localhost:8100/Workflows/notify-workflow-exited"
        #workflow_status = "Succeded" if random.random() > 0.3 else "Failed"
        workflow_status = "Succeded"
        payload = {
            "inspectionId": inspection_id,
            "workflowStatus": workflow_status
        }
        print(f"Sending PUT to {url} with data: {payload}")
        response = requests.put(url, json=payload, verify=False)

        if response.status_code == 200:
            print("Workflow exited notification sent successfully.")
        else:
            print(f"Failed to notify workflow exited: {response.text}")
    except Exception as e:
        print(f"Error in notify_workflow_exited: {e}")

if __name__ == '__main__':
    app.run(host='127.0.0.1', port=8080)
