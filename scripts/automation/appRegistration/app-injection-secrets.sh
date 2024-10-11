#!/bin/bash

# Load variables from .cfg file
source ./path/to/config.cfg

# Generate new client secret for app registration
IDA_CLIENT_SECRET=$(az ad app credential reset --id $CFG_IDA_CLIENT_ID --append --display-name $CFG_IDA_SECRET_NAME --query password --out tsv) 

if [ $? -eq 0 ]; then
    echo "Deployment of new client secret succeeded."
else
    echo "Deployment of new client secret failed."
    exit 1
fi

# Save client secret in desired key vault
az keyvault secret set --vault-name $CFG_VAULT_NAME \
                       --name $CFG_IDA_SECRET_NAME \
                       --value $IDA_CLIENT_SECRET \



if [ $? -eq 0 ]; then
    echo "Saving client secret to key vault succeeded."
else
    echo "Saving client secret to key vault failed."
    exit 1
fi
