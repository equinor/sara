#!/bin/bash

deploymentName="IDA$(date +%Y%m%d%H%M%S)"
location="northeurope"
bicepTemplateFile="scripts/automation/infrastructure.bicep"
bicepParameterFile="scripts/automation/infrastructure.bicepparam"

administratorLoginPassword=$(date +%s%N | sha256sum | head -c48)


az deployment sub create \
    --location $location \
    --name $deploymentName \
    --template-file $bicepTemplateFile \
    --parameters $bicepParameterFile \
    --parameters administratorLoginPassword="$administratorLoginPassword"

if [ $? -eq 0 ]; then
    echo "Deployment succeeded."
else
    echo "Deployment failed."
    exit 1
fi
