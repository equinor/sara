# Documentation for calling the Omnia TimeSeries API

## Documentation

General documentation is available here: https://github.com/equinor/OmniaPlant
Documentation for the Python API is available here: https://github.com/equinor/omnia-timeseries-python/tree/main

## Installation

Create a virtual environment with

```bash
python -m venv venv
```

and activate it.

Install the TimeSeries API with

```bash
pip install git+https://github.com/equinor/omnia-timeseries-python.git@main
```

## Authorizing against the test API

From https://github.com/equinor/OmniaPlant/wiki/Authentication-&-Authorization#introduction:

The customer client application must ask for an access token from Azure AD by providing the required parameters (i.e. client id, client secret, resource id and tenant id of the Omnia Timeseries API for Client Credentials Grant Flow using shared secret).

```bash
AZURE_CLIENT_ID=
AZURE_CLIENT_SECRET=
AZURE_TENANT_ID=
```
