# Inspection Data Analyzer

IDA (Inspection Data Analyzer) is a repository for running pipelines to analyze data coming from various inspections.

When running locally the endpoint can be reached at
https://localhost:8100

TODO: At the moment the application is using FlotillaKV and Flotilla App Reg in Azure, needs to be changed to a new one for IDA

See [LocalFunctionProj](./functions/LocalFunctionProj/) for an example of how to set up your pipeline. You can also run this function locally by running
'func start' from the [LocalFunctionProj](./functions/LocalFunctionProj/) folder, and then going to 'http://localhost:7071/api/HttpExample' to trigger it.

If you get 'Can't determine Project to build. Expected 1 .csproj or .fsproj but found 2' run 'dotnet clean' before running 'func start'

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
