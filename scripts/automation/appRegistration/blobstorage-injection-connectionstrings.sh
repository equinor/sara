#!/bin/bash

# Set default paths for the configuration files
dev_config="./scripts/automation/appRegistration/app-injection-secrets-param-dev.cfg"
prod_config="./scripts/automation/appRegistration/app-injection-secrets-param-prod.cfg"
staging_config="./scripts/automation/appRegistration/app-injection-secrets-param-staging.cfg"

# Check that the user has passed the name of the desired config file
if [[ "$#" -ne 1 ]] || [[ ! "$1" =~ ^(dev|prod|staging)$ ]]; then
    echo "Please specify the name of the desired config file ('dev', 'prod', or 'staging'). Run it as './scripts/automation/appRegistration/blobstorage-injection-connectionstrig.sh dev', for example"
    exit 1
fi

# Load variables from the chosen .cfg file
case $1 in
    dev)
        source "$dev_config"
        ;;
    prod)
        source "$prod_config"
        ;;
    staging)
        source "$staging_config"
        ;;
esac

# Generate new connection strings for storage accounts:
rawConnectionString=$(az storage account show-connection-string -g $CFG_RESOURCE_GROUP -n $CFG_STORAGE_ACCOUNT_NAME_RAW --out tsv)
anonConnectionString=$(az storage account show-connection-string -g $CFG_RESOURCE_GROUP -n $CFG_STORAGE_ACCOUNT_NAME_ANON --out tsv)
visConnectionString=$(az storage account show-connection-string -g $CFG_RESOURCE_GROUP -n $CFG_STORAGE_ACCOUNT_NAME_VIS --out tsv)

if [ $? -eq 0 ]; then
    echo "Deployment of new connection strings succeeded."
else
    echo "Deployment of new connection strings failed."
    exit 1
fi

# Save client secret in desired key vault
az keyvault secret set --vault-name $CFG_VAULT_NAME \
                       --name $CFG_CONNECTION_STRING_RAW_NAME \
                       --value $rawConnectionString \

az keyvault secret set --vault-name $CFG_VAULT_NAME \
                       --name $CFG_CONNECTION_STRING_ANON_NAME \
                       --value $anonConnectionString \

az keyvault secret set --vault-name $CFG_VAULT_NAME \
                       --name $CFG_CONNECTION_STRING_VIS_NAME \
                       --value $visConnectionString \

if [ $? -eq 0 ]; then
    echo "Saving client secret to key vault succeeded."
else
    echo "Saving client secret to key vault failed."
    exit 1
fi
