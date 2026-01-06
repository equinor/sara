## Deployment of azure resources (PostgreSQL flexible server, KeyVault and Storage accounts)

Requirements to be met:

- Owner role in [S159-Robotics-in-Echo](https://portal.azure.com/#@StatoilSRM.onmicrosoft.com/resource/subscriptions/c389567b-2dd0-41fa-a5da-d86b81f80bda/overview) subscription (needed for ManagedIdentity deployment to work).
- Owner status in the [app registration](https://portal.azure.com/?feature.msaljs=true#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Overview/appId/dd7e115a-037e-4846-99c4-07561158a9cd/isMSAApp~/false) for dev, prod and staging environment (needed for the generation of client secret).
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
   - `az login`
   - select the S159 subscription when prompted. If not, run: `az account set -s S159-Robotics-in-Echo`
   - Owner role activated on the subscription might be needed
   - open `bash scripts/automation/deploy.sh` and change '<env>' in `bicepParameterFile` to the desire environment. Default is "dev". For example, `bicepParameterFile` is by default 'scripts/automation/infrastructure-dev.bicepparam'. Change dev in the path to prod or staging, as desire.
   - run `bash scripts/automation/deploy.sh` to deploy the resources.
4. Optional. Copy data from old storage accounts to newly created simply by using
   - `azcopy copy 'https://mysourceaccount.blob.core.windows.net/' 'https://mydestinationaccount.blob.core.windows.net' --recursive` (this might require that you add the role assignment Storage Blob Data Owner to yourself in the portal)

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
2. Ensure that `CFG_SARA_CLIENT_ID` is the client ID of the App in which you want to add a new client secret. These values are already pre-filed for SARA app registrations.
3. You can change `CFG_SARA_SECRET_NAME` by the secret name desired.
4. Change `CFG_RESOURCE_GROUP` and `CFG_VAULT_NAME` for the resource group and respective key vault, in which the secret will be injected.
5. Grant privileges to 'app-injection-secrets.sh' and run it: `bash scripts/automation/appRegistration/app-injection-secrets.sh`. Follow the instructions prompted in the command line and choose the environment you are deploying (dev, prod or staging).

### Generate connection strings for storage accounts and inject to deployed key vault.

1. Following same logic as for the client secrets (app Registration) in the previous section, modify the names of the storage accounts and the names you want to use for the deployed connection string in the same config files. For example, `CFG_STORAGE_ACCOUNT_NAME_RAW` is the name of the raw storage account and `CFG_CONNECTION_STRING_RAW_NAME` would be the displayed name in the key vault for the connection string of the raw storage account. Do the same for anon and vis storage accounts.
2. Grant privileges to 'blobstorage-injection-connectionstrings.sh' and run it: `bash scripts/automation/appRegistration/blobstorage-injection-connectionstrings.sh`. Follow the instructions prompted in the command line and choose the environment you are deploying (dev, prod or staging)
