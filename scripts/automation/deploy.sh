#!/bin/bash

deploymentName="IDA$(date +%Y%m%d%H%M%S)"
location="northeurope"
bicepTemplateFile="scripts/automation/infrastructure.bicep"
bicepParameterFile="scripts/automation/infrastructure-dev.bicepparam"

# for postgreSQL flexible server:
administratorLoginPassword=$(date +%s%N | sha256sum | head -c48)

# Read values from json parameter file
serverNamejson=$(jq -r '.parameters.serverName.value' scripts/automation/infrastructure-dev.parameters.json)
administratorLoginjson=$(jq -r '.parameters.administratorLogin.value' scripts/automation/infrastructure-dev.parameters.json)

# connection string for postgreSQL server:
postgresConnectionString1="Server=${serverNamejson}.postgres.database.azure.com;Database=postgres;Port=5432;User Id=${administratorLoginjson};Password=$administratorLoginPassword;Ssl Mode=VerifyCA"


az deployment sub create \
    --location $location \
    --name $deploymentName \
    --template-file $bicepTemplateFile \
    --parameters scripts/automation/infrastructure-dev.parameters.json \
    --parameters postgresConnectionString="$postgresConnectionString1" \
    --parameters administratorLoginPassword="$administratorLoginPassword" 



if [ $? -eq 0 ]; then
    echo "Deployment succeeded."
else
    echo "Deployment failed."
    exit 1
fi
