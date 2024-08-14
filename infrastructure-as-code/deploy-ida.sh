#!/bin/bash

# Variables for storage
location="northeurope"
resourceGroup="InspectionDataAnalyzer"

storageInspectionData="inspectiondatastorage"
rawDataContainer="rawinspectiondata"
sensoredDataContainer="sensoredinspectiondata"

storageFunctions="idafunctionsstorage"
appServicePlanName="idaappserviceplan"
functionsAppName="idafunctions"


# Create resource group
echo "Creating $resourceGroup in $location..."
az group create --name $resourceGroup --location $location
az config set defaults.group=$resourceGroup defaults.location=$location

# Create a general-purpose storage account for inspection data
echo "Creating storage account $storageInspectionData..."
az storage account create --name $storageInspectionData --sku Standard_RAGRS

# Set delete policy on the raw data
# https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview?tabs=azure-portal
az storage account management-policy create --account-name $storageInspectionData --policy @policy.json

# Create a storage container for the raw data, retention of 7 days
echo "Creating storage container $rawDataContainer..."
az storage container create --name $rawDataContainer --account-name $storageInspectionData --fail-on-exist

# Create a storage container for sensored data, no retention limit
echo "Creating storage container $sensoredDataContainer..."
az storage container create --name $sensoredDataContainer --account-name $storageInspectionData --fail-on-exist

# Create a general-purpose storage account for the azure functions
# https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-cli-csharp?tabs=linux%2Cazure-cli#run-the-function-locally
echo "Creating storage account $storageFunctions..."
az storage account create --name $storageFunctions --sku Standard_LRS

# Create a app service plan
az appservice plan create --name $appServicePlanName

# Create the function app in Azure
echo "Create function app $functionsAppName..."
az functionapp create --name $functionsAppName --storage-account $storageFunctions --plan $appServicePlanName --runtime dotnet-isolated --functions-version 4
