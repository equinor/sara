#!/bin/bash

# Set default paths for the configuration files
dev_config="./scripts/automation/appRegistration/app-injection-secrets-param-dev.cfg"
prod_config="./scripts/automation/appRegistration/app-injection-secrets-param-prod.cfg"
staging_config="./scripts/automation/appRegistration/app-injection-secrets-param-staging.cfg"

# Check that the user has passed the name of the desired config file
if [[ "$#" -ne 1 ]] || [[ ! "$1" =~ ^(dev|prod|staging)$ ]]; then
    echo "Please specify the name of the desired config file ('dev', 'prod', or 'staging'). Run it as './scripts/automation/appRegistration/app-injection-secrets.sh dev', for example"
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
