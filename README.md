# Storage and Analysis of Robot Acquired plant data

_Note: This repository was previously named Inspection Data Analyzer (IDA).
Work is in progress on renaming it to SARA (Storage and Analysis of Robot Acquired plant data)_

SARA (Storage and Analysis of Robot Acquired plant data) is a web service that facilitates
the processing of incoming plant data from autonomous robots. Various workflows will be
triggered to analyse the plant data, based on metadata provided. The workflows themselves
are not in the scope of this solution. This solution only needs to have an overview of
which workflows are available and how to call them with their API.

SARA is responsible for indexing the incoming data, including information on where the
raw data is stored, which analysis are to be run, the status on these, where to
temporarily store artifacts and where to store the finalized results and visualizations.
SARA can then be queried later from other solutions and use the indexing to look data and
generate a response.

Examples of plant data are pictures, videos, thermal pictures, thermal videos and audio.

The resulting service is available in three environments (development, staging and production).

When running locally the endpoint can be reached at https://localhost:8100.

## More documentation

See the `/docs` folder for more documentation, for example

- [Deploying resources](docs/deploying_resources.md)
- [Database and migrations](docs/database_and_migrations.md)
- [Formatting](docs/formatting.md)
