# Storage and Analysis of Robot Acquired plant data

SARA (Storage and Analysis of Robot Acquired plant data) is a web service that facilitates
the processing of incoming plant data from autonomous robots. Various workflows will be
triggered to analyse the plant data, based on metadata provided. The workflows themselves
are not in the scope of this solution. This solution only needs to have an overview of
which workflows are available and how to call them with their API.

SARA is responsible for indexing the incoming data, including information on where the
raw data is stored, which analysis are to be run, the status on these, where to
temporarily store artifacts and where to store the finalized results and visualizations.
SARA can then be queried later from other solutions and use the indexing to look up data and
generate a response.

Examples of plant data are pictures, videos, thermal pictures, thermal videos and audio.

The resulting service is available in three environments (development, staging and production).

When running locally the endpoint can be reached at https://localhost:8100.

## Services

SARA uses several services to upload data to other sources for storage or analysis.
A list of these services can be found below.

- [Sara Timeseries](https://github.com/equinor/sara-timeseries/)
- [Sara Anonymizer](https://github.com/equinor/sara-anonymizer/)

## Workflow

There will be several analysis available for SARA. These are triggered through an
Argo Workflow which can have a conditional flow based on the type of inspection.

```
anonymizer-->constant-level-oiler
         \-->stid-uploader
```

## Analysis Mapping

Which analysis pipeline is run is chosen by the analysis mapping. A tag + an insepction descripts maps to an analysis type.
To add a new analysis type, add a value to the [AnalysisType](api/Database/Models/Analysis.cs) enum. Then add which tag + inspection description should map to the new AnalysisType. This can be done through the [AddOrCreateAnalysisMapping](api/Controllers/AnalysisMappingController.cs) endpoint. Then include code in the [MqttEventHandler](api/MQTT/MqttEventHandler.cs) to run the desiered pipeline for you AnalysisType. At the moment the supported analysis types are:

- Anonymizer
- ConstantLevelOiler

At the moment Anonymizer is configured to always run on IsarInspectionResultMessage

## Run

To build and run SARA, run the following command in the root folder:

```
dotnet run --project api
```

## Running the argo workflow mock

`python mocks/argo_workflow_mock.py`

## Deployments

We currently have 3 environments (Development, Staging, and Production) deployed to Aurora.

| Environment | Deployment                                                                           |
| ----------- | ------------------------------------------------------------------------------------ |
| Development | [Backend](https://shared.dev.aurora.equinor.com/sara-dev-backend/swagger/index.html) |
| Staging     | [Backend](https://shared.aurora.equinor.com/sara-staging-backend/swagger/index.html) |
| Production  | [Backend](https://shared.aurora.equinor.com/sara-prod-backend/swagger/index.html)    |

## More documentation

See the `/docs` folder for more documentation, for example

- [Deploying resources](docs/deploying_resources.md)
- [Database and migrations](docs/database_and_migrations.md)
- [Formatting](docs/formatting.md)
