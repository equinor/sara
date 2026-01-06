#!/bin/bash

deploymentName="SARA$(date +%Y%m%d%H%M%S)"
location="northeurope"
bicepTemplateFile="scripts/automation/infrastructure.bicep"
bicepParameterFile="scripts/automation/infrastructure-dev.bicepparam"

az deployment sub create \
    --debug \
    --location $location \
    --name $deploymentName \
    --template-file $bicepTemplateFile \
    --parameters $bicepParameterFile \



if [ $? -eq 0 ]; then
    echo "Deployment succeeded."
else
    echo "Deployment failed."
    exit 1
fi
