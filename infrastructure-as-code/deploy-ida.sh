#!/bin/bash

# Variables for storage
location= "northeurope"
resourceGroup="InspectionDataAnalyzer"

storageInspectionData="inspectiondata"
rawDataContainer="rawinspectiondata"
sensoredDataContainer="sensoredinspectiondata"

storageFunctions="azurefunctionsstate"
functionsAppName="idafunctions"

# Create resource group
echo "Creating $resourceGroup in $location..."
az group create --name $resourceGroup --location "$location"

# Create a general-purpose standard storage account
echo "Creating $storageInspectionData..."
az storage account create --name $storageInspectionData --resource-group $resourceGroup --location "$location" --sku Standard_RAGRS --encryption-services blob

# Create a storage container for the raw data, retention of 7 days
az storage container create --name $rawDataContainer --fail-on-exist

# Create a storage container for sensored data, no retention limit
# TODO: Add the delete policy
# https://learn.microsoft.com/en-us/azure/storage/blobs/lifecycle-management-overview?tabs=azure-portal
az storage container create --name $sensoredDataContainer --fail-on-exist

# Create a general-purpose storage account for the azure functions
az storage account create --name $storageFunctions --location $location --resource-group $resourceGroup --sku Standard_LRS --allow-blob-public-access false
# https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-cli-csharp?tabs=linux%2Cazure-cli#run-the-function-locally

# Create the function app in Azure
az functionapp create --resource-group $resourceGroup --consumption-plan-location $location --runtime dotnet-isolated --functions-version 4 --name $functionsAppName --storage-account $storageFunctions
