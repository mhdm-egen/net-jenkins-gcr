gcloud config get-value project

gcloud projects list

gcloud config set project egen-cicd-net

gcloud artifacts repositories list    

gcloud artifacts repositories create REPO_NAME \
  --repository-format=docker \
  --location=REGION \
  --description="Docker repo"

  docker tag web.apphost:latest us-west1-docker.pkg.dev/egen-cicd-net/egen-cicd-net/web-apphost:v1
  docker push us-west1-docker.pkg.dev/egen-cicd-net/egen-cicd-net/web-apphost:v1 

  https://web-apphost-603087230604.europe-west1.run.app

PROJECT_ID="egen-cicd-net"
REGION="us-west1"
REPO="egen-cicd-net"
IMAGE="web-apphost"
TAG="v1"
SERVICE="web-apphost"
IMAGE_URL="us-west1-docker.pkg.dev/${PROJECT_ID}/${REPO}/${IMAGE}:${TAG}"

gcloud run deploy "${SERVICE}" \
  --image="${IMAGE_URL}" \
  --region="${REGION}" \
  --project="${PROJECT_ID}" \
  --platform=managed \
  --allow-unauthenticated \
  --port=8080 \
  --memory=512Mi \
  --cpu=1 \
  --min-instances=0 \
  --max-instances=1

$PROJECT_ID="egen-cicd-net"
$REGION="us-west1"
$REPO="egen-cicd-net"
$IMAGE="web-apphost"
$TAG="v1"
$SERVICE="web-apphost"
$IMAGE_URL="us-west1-docker.pkg.dev/$PROJECT_ID/$REPO/${IMAGE}:$TAG"

gcloud run deploy "$SERVICE" --image="$IMAGE_URL" --region="$REGION" --project="$PROJECT_ID" `
  --platform=managed --allow-unauthenticated --port=8080 --memory=512Mi --cpu=1 `
  --min-instances=0 --max-instances=1
  
gcloud run services list --region=$REGION --project=$PROJECT_ID

gcloud run services delete $SERVICE --region=$REGION --project=$PROJECT_ID --quiet

