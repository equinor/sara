#!/bin/bash

# Load variables from .cfg file
source ./path/to/config.cfg

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
