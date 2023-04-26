# Script to create a Semantic Kernel service deployment using an
# existing OpenAI or Azure OpenAI instance.

param(
    [Parameter(Mandatory)]
    [string]
    $DeploymentName = "SKaaS",

    [Parameter(Mandatory)]
    [string]
    $ApiKey,

    [string]
    $Endpoint = "",

    [Parameter(Mandatory)]
    [string]
    $Subscription,

    [string]
    $ResourceGroup = "",

    [string]
    $Region = "South Central US",

    [string]
    $PackageUri = 'https://skaasdeploy.blob.core.windows.net/api/skaas.zip',

    [switch]
    $DebugDeployment
)

$ErrorActionPreference = "Stop"

$templateFile = "$($PSScriptRoot)/sk-reuse-ai.bicep"

if (!$ResourceGroup)
{
    $ResourceGroup = $DeploymentName + "-rg"
}

Write-Host "Log into your Azure account"
az login | out-null

az account set -s $Subscription

Write-Host "Creating resource group $($ResourceGroup) if it doesn't exist..."
az group create --location $Region --name $ResourceGroup --tags Creator=$env:UserName
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host "Validating template file..."
az deployment group validate --name $DeploymentName --resource-group $ResourceGroup --template-file $templateFile --parameters name=$DeploymentName location=$Region packageUri=$PackageUri endpoint=$Endpoint apiKey=$ApiKey
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Deploying..."
if ($DebugDeployment) {
    az deployment group create --name $DeploymentName --resource-group $ResourceGroup --template-file $templateFile --debug --parameters name=$DeploymentName location=$Region packageUri=$PackageUri endpoint=$Endpoint apiKey=$ApiKey
}
else {
    az deployment group create --name $DeploymentName --resource-group $ResourceGroup --template-file $templateFile --parameters name=$DeploymentName location=$Region packageUri=$PackageUri endpoint=$Endpoint apiKey=$ApiKey
}