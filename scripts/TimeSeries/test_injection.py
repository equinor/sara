# Examples from https://github.com/equinor/omnia-timeseries-python/blob/main/examples/usage_examples.ipynb

import os

from azure.identity import ClientSecretCredential
from omnia_timeseries.api import TimeseriesAPI, TimeseriesEnvironment

credentials = ClientSecretCredential(
    client_id=os.environ["AZURE_CLIENT_ID"],
    client_secret=os.environ["AZURE_CLIENT_SECRET"],
    tenant_id=os.environ["AZURE_TENANT_ID"],
)

api = TimeseriesAPI(
    azure_credential=credentials,
    environment=TimeseriesEnvironment.Test(),
)

# Calling commands

# print(
#     api.post_timeseries(
#         {
#             "name": "30JBY2101",
#             "facility": "NLS",
#             "description": "Spherical glass",
#             "unit": "%",
#         }
#     )
# )

# This ID will be in the response from the api.post_timeseries call
timeseries_id1 = ""

# print(
#     api.write_data(
#         id=timeseries_id1,
#         data={
#             "datapoints": [
#                 {"time": "2025-04-01T20:44:00.000Z", "value": 70, "status": 0}
#             ]
#         },
#     )
# )

# Check that the data point was written
# print(api.get_latest_datapoint(id=timeseries_id1))

# Other useful commands

# print(api.get_timeseries(limit=10))
# print(api.search_timeseries(facility="NLS", limit=10))
# print(api.get_timeseries_by_id(id=timeseries_id1))
# print(api.get_history(id=timeseries_id1))
# print(api.get_facilities())
# print(api.get_sources())

# print(api.patch_timeseries(
#     id=timeseries_id1,
#     request={
#         "name": "30JBY2101",
#         "description": "Spherical glass",
#         "unit": "%",
#         "step": True,
#     },
# ))

# Remember to delete timeseries from test database after use
# print(api.delete_timeseries_by_id(id=timeseries_id1))
