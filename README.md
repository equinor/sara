# Inspection Data Analyzer

IDA (Inspection Data Analyzer) is a repository for running pipelines to analyze data coming from various inspections.

When running locally the endpoint can be reached at
https://localhost:8100

## Deployment of azure resources (PostgreSQL flexible server, KeyVault and Storage accounts)

Requirements to be met:

- Contributor role in [S159-Robotics-in-Echo](https://portal.azure.com/#@StatoilSRM.onmicrosoft.com/resource/subscriptions/c389567b-2dd0-41fa-a5da-d86b81f80bda/overview) subscription.
- [az CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) installed.
- It will be required, for the deployment and injection in the key vault of the postgreSQL connection string, to build a json file from the provided bicepparam file. This will be an automated process, but you need to ensure to have [jq](https://github.com/jqlang/jq), a command-line json processor. If you are using MacOs, you can installed with [brew](https://formulae.brew.sh/formula/jq).

### Deployment of resources

1. Give the deployment script privileges to run. From root of this repository, run:
   - `chmod +x scripts/automation/deploy.sh`
2. Prepare the resource group name:
   - open the /scripts/automation/infrastructure.bicepparam file.
   - change `param environment = 'YourEnvName' ` to desire name.
     Keep in mind that, in the same file, you can change the name of storage accounts, key vault and database if needed. Remember that the names of these resources must be unique.
3. Deploy the Azure resources with the bicep files. Run the following commands:
   - `az login `
   - select the S159 subscription when prompted. If not, run: `az account set -s S159-Robotics-in-Echo`
   - run `az bicep build-params --file scripts/automation/infrastructure.bicepparam --outfile scripts/automation/infrastructure.parameters.json` to generate a json file from the bicepparam file provided.
   - run `bash scripts/automation/deploy.sh` to deploy the resources.
   - Note: administrator login password and the connection string for the postgreSQL flexible server would be available in the deployed key vault.

### Individual deployment of blob containers

You can populate the previously deployed storage accounts with blob containers as needed, following these steps:

1. Open /scripts/automation/modules/blob-container.bicep file.
2. Change:
   - `param storageAccountName string = 'YourStorageAccountNameHere'`
   - `param containerName string = 'YourContainerNameHere'`
     Note: the container name should be in lowercase.
3. Run the following command:

- `az deployment group create --resource-group <resource-group-name> --template-file <bicep-file-name>`, changing '<resource-group-name>' for the already deployed resource group name, and <bicep-file-name>` for /scripts/automation/modules/blob-container.

### Generate client secret (App Registration) and inject to deployed key vault.

1. Under /scripts/automation/appRegistration, there are available config files for each one of the environments (dev, staging and prod). Select which one you want to modify, to deploy a new client secret.
2. Ensure that `CFG_IDA_CLIENT_ID` is the client ID of the App in which you want to add a new client secret. These values are already pre-filed for IDA app registrations.
3. You can change `CFG_IDA_SECRET_NAME` by the secret name desired.
4. Change `CFG_RESOURCE_GROUP` and `CFG_VAULT_NAME` for the resource group and respective key vault, in which the secret will be injected.
5. Change the source in 'app-injection-secrets.sh' with the path to the config file you were editing and grant privileges to run it: `bash scripts/automation/appRegistration/app-injection-secrets.sh`

### Generate connection strings for storage accounts and inject to deployed key vault.

1. Following same logic as for the client secrets (app Registration) in the previous section, modify the names of the storage accounts and the names you want to use for the deployed connection string in the same config files. For example, `CFG_STORAGE_ACCOUNT_NAME_RAW` is the name of the raw storage account and `CFG_CONNECTION_STRING_RAW_NAME` would be the displayed name in the key vault for the connection string of the raw storage account. Do the same for anon and vis storage accounts.
2. Change the source in 'blobstorage-injection-connectionstrings.sh' to the desired config file and grant privileges to run it: `bash scripts/automation/appRegistration/blobstorage-injection-connectionstrings.sh`

## Database model and EF Core

Our database model is defined in the folder
[`/api/Models`](/api/Models) and we use
[Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) as an
object-relational mapper (O/RM). When making changes to the model, we also need
to create a new
[migration](https://docs.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
and apply it to our databases.

### Installing EF Core

```bash
dotnet tool install --global dotnet-ef
```

### Adding a new migration

**NB: Make sure you have have fetched the newest code from main and that no-one else
is making migrations at the same time as you!**

1. Set the environment variable `ASPNETCORE_ENVIRONMENT` to `Development`:

   ```bash
    export ASPNETCORE_ENVIRONMENT=Development
   ```

2. Run the following command from `/api`:
   ```bash
     dotnet ef migrations add AddTableNamePropertyName
   ```
   `add` will make changes to existing files and add 2 new files in
   `/api/Migrations`, which all need to be checked in to git.

### Notes

- The `your-migration-name-here` is basically a database commit message.
- `Database__ConnectionString` will be fetched from the keyvault when running the `add` command.
- `add` will _not_ update or alter the connected database in any way, but will add a
  description of the changes that will be applied later
- If you for some reason are unhappy with your migration, you can delete it with:
  ```bash
  dotnet ef migrations remove
  ```
  Once removed you can make new changes to the model
  and then create a new migration with `add`.

### Applying the migrations to the dev database

Updates to the database structure (applying migrations) are done in Github Actions.

When a pull request contains changes in the `/api/Migrations` folder,
[a workflow](https://github.com/equinor/inspection-data-analyzer/blob/main/.github/workflows/notifyMigrationChanges.yml)
is triggered to notify that the pull request has database changes.

After the pull request is approved, a user can then trigger the database changes by commenting
`/UpdateDatabase` on the pull request.

This will trigger
[another workflow](https://github.com/equinor/flotilla/blob/main/.github/workflows/updateDatabase.yml)
which updates the database by apploying the new migrations.

By doing migrations this way, we ensure that the commands themselves are scripted, and that the database
changes become part of the review process of a pull request.

### Applying migrations to staging and production databases

This is done automatically as part of the promotion workflows
([promoteToProduction](https://github.com/equinor/flotilla/blob/main/.github/workflows/promoteToProduction.yml)
and [promoteToStaging](https://github.com/equinor/flotilla/blob/main/.github/workflows/promoteToStaging.yml)).

## Formatting

### CSharpier

In everyday development we use [CSharpier](https://csharpier.com/) to auto-format code on save. Installation procedure is described [here](https://csharpier.com/docs/About). No configuration should be required.

### Dotnet format

The formatting of the api is defined in the [.editorconfig file](../.editorconfig).

We use [dotnet format](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-format)
to format and verify code style in api based on the
[C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

Dotnet format is included in the .NET6 SDK.

To check the formatting, run the following command in the api folder:

```
cd api
dotnet format --severity info --verbosity diagnostic --verify-no-changes --exclude ./api/migrations
```

dotnet format is used to detect naming conventions and other code-related issues. They can be fixed by

```
dotnet format --severity info
```
