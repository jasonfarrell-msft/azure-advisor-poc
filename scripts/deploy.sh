#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SUBSCRIPTION_ID=""
ENVIRONMENT_NAME="azureadvisor-dev"
LOCATION="eastus2"
STATIC_WEB_APP_LOCATION="centralus"
BOOTSTRAP_CIDR=""
FOUNDRY_RESOURCE_GROUP=""
FOUNDRY_ACCOUNT=""
FOUNDRY_ENDPOINT=""
FOUNDRY_DEPLOYMENT="gpt-5.4-mini"
FOUNDRY_PROJECT=""
ADMIN_EMAIL=""
ENABLE_LIVE_READER="false"
SKIP_TESTS="false"
ACR_NAME=""
RESOURCE_GROUP=""
ACR_BOOTSTRAP_OPEN="false"

cleanup() {
  if [[ "$ACR_BOOTSTRAP_OPEN" == "true" && -n "$ACR_NAME" && -n "$RESOURCE_GROUP" ]]; then
    echo "Deployment did not complete; disabling ACR public access..." >&2
    az acr update \
      --name "$ACR_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --public-network-enabled false \
      --only-show-errors \
      -o none || echo "Warning: failed to disable ACR public access automatically." >&2
  fi
}

trap cleanup EXIT

usage() {
  cat <<'EOF'
Deploy Azure Cost Advisor into the selected Azure subscription.

Usage:
  ./scripts/deploy.sh [options]

Options:
  --subscription ID              Azure subscription ID or name. Defaults to the active subscription.
  --environment NAME             Lowercase deployment name (default: azureadvisor-dev).
  --location REGION              Container Apps region (default: eastus2).
  --swa-location REGION          Static Web Apps region (default: centralus).
  --bootstrap-cidr CIDR          IPv4/CIDR allowed during the temporary ACR build window.
  --admin-email EMAIL            Create an operator invitation for this email.
  --enable-live-reader           Grant the app identity Reader at subscription scope.
  --foundry-resource-group NAME  Resource group of an existing Azure AI Foundry account.
  --foundry-account NAME         Existing Azure AI Foundry account name.
  --foundry-endpoint URL         Existing Azure AI Foundry/OpenAI endpoint.
  --foundry-deployment NAME      Model deployment name (default: gpt-5.4-mini).
  --foundry-project NAME         Optional Foundry project display name.
  --skip-tests                   Skip backend and frontend tests before deployment.
  -h, --help                     Show this help.

Foundry is optional. If omitted, the application deploys with deterministic explanations.
EOF
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1" >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --subscription)
      SUBSCRIPTION_ID="$2"
      shift 2
      ;;
    --environment)
      ENVIRONMENT_NAME="$2"
      shift 2
      ;;
    --location)
      LOCATION="$2"
      shift 2
      ;;
    --swa-location)
      STATIC_WEB_APP_LOCATION="$2"
      shift 2
      ;;
    --bootstrap-cidr)
      BOOTSTRAP_CIDR="$2"
      shift 2
      ;;
    --admin-email)
      ADMIN_EMAIL="$2"
      shift 2
      ;;
    --enable-live-reader)
      ENABLE_LIVE_READER="true"
      shift
      ;;
    --foundry-resource-group)
      FOUNDRY_RESOURCE_GROUP="$2"
      shift 2
      ;;
    --foundry-account)
      FOUNDRY_ACCOUNT="$2"
      shift 2
      ;;
    --foundry-endpoint)
      FOUNDRY_ENDPOINT="$2"
      shift 2
      ;;
    --foundry-deployment)
      FOUNDRY_DEPLOYMENT="$2"
      shift 2
      ;;
    --foundry-project)
      FOUNDRY_PROJECT="$2"
      shift 2
      ;;
    --skip-tests)
      SKIP_TESTS="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

require_command az
require_command curl
require_command jq
require_command node
require_command npm
require_command dotnet

if [[ ! "$ENVIRONMENT_NAME" =~ ^[a-z0-9][a-z0-9-]{2,24}$ ]]; then
  echo "--environment must be 3-25 lowercase letters, numbers, or hyphens." >&2
  exit 1
fi

foundry_values=0
[[ -n "$FOUNDRY_RESOURCE_GROUP" ]] && foundry_values=$((foundry_values + 1))
[[ -n "$FOUNDRY_ACCOUNT" ]] && foundry_values=$((foundry_values + 1))
[[ -n "$FOUNDRY_ENDPOINT" ]] && foundry_values=$((foundry_values + 1))
if [[ "$foundry_values" -ne 0 && "$foundry_values" -ne 3 ]]; then
  echo "Foundry requires --foundry-resource-group, --foundry-account, and --foundry-endpoint together." >&2
  exit 1
fi

if [[ -n "$SUBSCRIPTION_ID" ]]; then
  az account set --subscription "$SUBSCRIPTION_ID"
fi

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
DEPLOYED_BY="$(az account show --query user.name -o tsv)"
if [[ -z "$ADMIN_EMAIL" && "$DEPLOYED_BY" == *"@"* ]]; then
  ADMIN_EMAIL="$DEPLOYED_BY"
fi

echo "Deploying to subscription $SUBSCRIPTION_ID as $DEPLOYED_BY"

if [[ -z "$BOOTSTRAP_CIDR" ]]; then
  public_ip="$(curl -fsS https://api.ipify.org)"
  if [[ ! "$public_ip" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]]; then
    echo "Unable to detect a valid public IPv4 address. Pass --bootstrap-cidr explicitly." >&2
    exit 1
  fi
  BOOTSTRAP_CIDR="${public_ip}/32"
fi

if [[ ! "$BOOTSTRAP_CIDR" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}(/[0-9]{1,2})?$ ]]; then
  echo "--bootstrap-cidr must be an IPv4 address or CIDR." >&2
  exit 1
fi

bootstrap_ip="${BOOTSTRAP_CIDR%/*}"
bootstrap_prefix="${BOOTSTRAP_CIDR#*/}"
if [[ "$bootstrap_prefix" == "$BOOTSTRAP_CIDR" ]]; then
  bootstrap_prefix="32"
fi
IFS='.' read -r octet1 octet2 octet3 octet4 <<<"$bootstrap_ip"
octet1=$((10#$octet1))
octet2=$((10#$octet2))
octet3=$((10#$octet3))
octet4=$((10#$octet4))
bootstrap_prefix=$((10#$bootstrap_prefix))
if ((octet1 > 255 || octet2 > 255 || octet3 > 255 || octet4 > 255 || bootstrap_prefix > 32)); then
  echo "--bootstrap-cidr contains an invalid IPv4 address or prefix." >&2
  exit 1
fi

if command -v sha256sum >/dev/null 2>&1; then
  NAME_SUFFIX="$(printf '%s' "${SUBSCRIPTION_ID}-${ENVIRONMENT_NAME}" | sha256sum | cut -c1-6)"
elif command -v shasum >/dev/null 2>&1; then
  NAME_SUFFIX="$(printf '%s' "${SUBSCRIPTION_ID}-${ENVIRONMENT_NAME}" | shasum -a 256 | cut -c1-6)"
else
  echo "A SHA-256 utility (sha256sum or shasum) is required." >&2
  exit 1
fi

RESOURCE_GROUP="rg-${ENVIRONMENT_NAME}-${NAME_SUFFIX}"
ACR_NAME="$(printf 'cr%s%s' "${ENVIRONMENT_NAME//-/}" "$NAME_SUFFIX" | cut -c1-50)"

for provider in Microsoft.App Microsoft.ContainerRegistry Microsoft.ManagedIdentity Microsoft.Network Microsoft.Web; do
  az provider register --namespace "$provider" --wait --only-show-errors
done

if [[ "$foundry_values" -eq 3 ]]; then
  az provider register --namespace Microsoft.CognitiveServices --wait --only-show-errors
fi

if [[ "$SKIP_TESTS" != "true" ]]; then
  dotnet test "$ROOT_DIR/AzureCostAdvisor.slnx" --nologo
  npm ci --prefix "$ROOT_DIR/src/frontend" --include=dev
  npm test --prefix "$ROOT_DIR/src/frontend" -- --run
else
  npm ci --prefix "$ROOT_DIR/src/frontend" --include=dev
fi
npm audit --prefix "$ROOT_DIR/src/frontend" --omit=dev --audit-level=high

swa_cli_version="$(node -p "require('$ROOT_DIR/src/frontend/node_modules/@azure/static-web-apps-cli/package.json').version")"
if [[ "$swa_cli_version" != "2.0.10" ]]; then
  echo "Expected the locked Static Web Apps CLI version 2.0.10, found $swa_cli_version." >&2
  exit 1
fi

az bicep build --file "$ROOT_DIR/infra/main.bicep" --stdout >/dev/null

DEPLOYMENT_BASE="${ENVIRONMENT_NAME}-$(date -u +%Y%m%d%H%M%S)"
CREATED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
BOOTSTRAP_IMAGE="mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"

if [[ "$(az group exists --name "$RESOURCE_GROUP")" == "true" ]]; then
  existing_image="$(az containerapp list \
    --resource-group "$RESOURCE_GROUP" \
    --query '[0].properties.template.containers[0].image' \
    -o tsv)"
  if [[ -n "$existing_image" ]]; then
    BOOTSTRAP_IMAGE="$existing_image"
    echo "Existing deployment detected; preserving image $BOOTSTRAP_IMAGE during bootstrap."
  fi
fi

common_parameters=(
  environmentName="$ENVIRONMENT_NAME"
  nameSuffix="$NAME_SUFFIX"
  location="$LOCATION"
  staticWebAppLocation="$STATIC_WEB_APP_LOCATION"
  sessionId="$DEPLOYMENT_BASE"
  deployedBy="$DEPLOYED_BY"
  createdAt="$CREATED_AT"
  enableSubscriptionReaderAssignment="$ENABLE_LIVE_READER"
  foundryResourceGroupName="$FOUNDRY_RESOURCE_GROUP"
  foundryAccountName="$FOUNDRY_ACCOUNT"
  foundryEndpoint="$FOUNDRY_ENDPOINT"
  foundryDeploymentName="$FOUNDRY_DEPLOYMENT"
  foundryProjectName="$FOUNDRY_PROJECT"
)

echo "Running provider-level deployment preview..."
az deployment sub what-if \
  --name "${DEPLOYMENT_BASE}-preview" \
  --location "$LOCATION" \
  --template-file "$ROOT_DIR/infra/main.bicep" \
  --parameters "${common_parameters[@]}" \
    acrPublicNetworkAccessEnabled=true \
    acrBootstrapIpAddress="$BOOTSTRAP_CIDR" \
    containerImage="$BOOTSTRAP_IMAGE" \
  --validation-level Provider \
  --result-format ResourceIdOnly \
  --no-pretty-print

echo "Creating infrastructure bootstrap resources..."
ACR_BOOTSTRAP_OPEN="true"
az deployment sub create \
  --name "${DEPLOYMENT_BASE}-bootstrap" \
  --location "$LOCATION" \
  --template-file "$ROOT_DIR/infra/main.bicep" \
  --parameters "${common_parameters[@]}" \
    acrPublicNetworkAccessEnabled=true \
    acrBootstrapIpAddress="$BOOTSTRAP_CIDR" \
    containerImage="$BOOTSTRAP_IMAGE" \
  --only-show-errors \
  -o none

deployment_outputs="$(az deployment sub show --name "${DEPLOYMENT_BASE}-bootstrap" --query properties.outputs -o json)"
STATIC_WEB_APP_NAME="$(jq -r '.deployedStaticWebAppName.value' <<<"$deployment_outputs")"
STATIC_WEB_APP_HOSTNAME="$(jq -r '.staticWebAppHostname.value' <<<"$deployment_outputs")"
CONTAINER_APP_NAME="$(jq -r '.deployedContainerAppName.value' <<<"$deployment_outputs")"
CONTAINER_APP_FQDN="$(jq -r '.containerAppFqdn.value' <<<"$deployment_outputs")"
IDENTITY_PRINCIPAL_ID="$(jq -r '.managedIdentityPrincipalId.value' <<<"$deployment_outputs")"
deployed_resource_group="$(jq -r '.deployedResourceGroupName.value' <<<"$deployment_outputs")"
deployed_acr_name="$(jq -r '.deployedAcrName.value' <<<"$deployment_outputs")"
if [[ "$deployed_resource_group" != "$RESOURCE_GROUP" || "$deployed_acr_name" != "$ACR_NAME" ]]; then
  echo "Deployment output names do not match the deterministic cleanup targets." >&2
  exit 1
fi

SOURCE_VERSION="$(git -C "$ROOT_DIR" rev-parse --short=12 HEAD 2>/dev/null || echo local)"
IMAGE_TAG="${SOURCE_VERSION}-$(date -u +%Y%m%d%H%M%S)"
echo "Building backend image in $ACR_NAME..."
az acr build \
  --registry "$ACR_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --image "azure-cost-advisor:$IMAGE_TAG" \
  --file "src/backend/AzureCostAdvisor.Api/Dockerfile.azure" \
  "$ROOT_DIR" \
  --only-show-errors \
  -o none

IMAGE_DIGEST="$(az acr repository show \
  --name "$ACR_NAME" \
  --image "azure-cost-advisor:$IMAGE_TAG" \
  --query digest \
  -o tsv)"
IMAGE_REFERENCE="${ACR_NAME}.azurecr.io/azure-cost-advisor@${IMAGE_DIGEST}"

echo "Deploying the immutable image and closing ACR public access..."
az deployment sub create \
  --name "${DEPLOYMENT_BASE}-application" \
  --location "$LOCATION" \
  --template-file "$ROOT_DIR/infra/main.bicep" \
  --parameters "${common_parameters[@]}" \
    acrPublicNetworkAccessEnabled=false \
    acrBootstrapIpAddress='' \
    containerImage="$IMAGE_REFERENCE" \
  --only-show-errors \
  -o none

acr_public_network_access="$(az acr show \
  --name "$ACR_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query publicNetworkAccess \
  -o tsv)"
if [[ "$acr_public_network_access" != "Disabled" ]]; then
  echo "ACR public network access remains enabled after the final deployment." >&2
  exit 1
fi
ACR_BOOTSTRAP_OPEN="false"

subscription_scope="/subscriptions/${SUBSCRIPTION_ID}"
if [[ "$ENABLE_LIVE_READER" != "true" ]]; then
  while IFS= read -r assignment_id; do
    [[ -z "$assignment_id" ]] && continue
    az role assignment delete --ids "$assignment_id" --only-show-errors
  done < <(az role assignment list \
    --assignee-object-id "$IDENTITY_PRINCIPAL_ID" \
    --role Reader \
    --scope "$subscription_scope" \
    --query "[?scope=='${subscription_scope}'].id" \
    -o tsv)
fi

expected_foundry_scope=""
if [[ "$foundry_values" -eq 3 ]]; then
  expected_foundry_scope="$(az cognitiveservices account show \
    --resource-group "$FOUNDRY_RESOURCE_GROUP" \
    --name "$FOUNDRY_ACCOUNT" \
    --query id \
    -o tsv)"
fi
expected_foundry_scope_normalized="$(printf '%s' "$expected_foundry_scope" | tr '[:upper:]' '[:lower:]')"

while IFS=$'\t' read -r assignment_id assignment_scope; do
  [[ -z "$assignment_id" ]] && continue
  assignment_scope_normalized="$(printf '%s' "$assignment_scope" | tr '[:upper:]' '[:lower:]')"
  if [[ -z "$expected_foundry_scope_normalized" || "$assignment_scope_normalized" != "$expected_foundry_scope_normalized" ]]; then
    az role assignment delete --ids "$assignment_id" --only-show-errors
  fi
done < <(az role assignment list \
  --assignee-object-id "$IDENTITY_PRINCIPAL_ID" \
  --role "Cognitive Services OpenAI User" \
  --all \
  --query '[].[id,scope]' \
  -o tsv)

echo "Building and deploying the frontend..."
npm run build --prefix "$ROOT_DIR/src/frontend" -- --emptyOutDir

deployment_token="$(az staticwebapp secrets list \
  --name "$STATIC_WEB_APP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query properties.apiKey \
  -o tsv)"

SWA_CLI_DEPLOYMENT_TOKEN="$deployment_token" "$ROOT_DIR/src/frontend/node_modules/.bin/swa" deploy \
  "$ROOT_DIR/src/frontend/dist" \
  --env production \
  --swa-config-location "$ROOT_DIR/src/frontend/public"
unset deployment_token

backend_status="$(curl -sS -o /dev/null -w '%{http_code}' "https://${CONTAINER_APP_FQDN}/api/settings/mode")"
if [[ "$backend_status" != "401" ]]; then
  echo "Expected direct backend access to return 401, received $backend_status." >&2
  exit 1
fi

frontend_status="$(curl -sS -o /dev/null -w '%{http_code}' "https://${STATIC_WEB_APP_HOSTNAME}/")"
if [[ "$frontend_status" != "302" && "$frontend_status" != "200" ]]; then
  echo "Expected frontend access to return 200 or an authentication redirect, received $frontend_status." >&2
  exit 1
fi

echo
echo "Deployment complete."
echo "Frontend: https://${STATIC_WEB_APP_HOSTNAME}"
echo "Backend:  https://${CONTAINER_APP_FQDN} (direct unauthenticated access returns 401)"
echo "Image:    ${IMAGE_REFERENCE}"

if [[ -n "$ADMIN_EMAIL" ]]; then
  invitation_url="$(az staticwebapp users invite \
    --name "$STATIC_WEB_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --authentication-provider aad \
    --user-details "$ADMIN_EMAIL" \
    --roles operator \
    --domain "$STATIC_WEB_APP_HOSTNAME" \
    --invitation-expiration-in-hours 168 \
    --query invitationUrl \
    -o tsv)"
  echo "Operator invitation for ${ADMIN_EMAIL} (valid 7 days):"
  echo "$invitation_url"
fi
