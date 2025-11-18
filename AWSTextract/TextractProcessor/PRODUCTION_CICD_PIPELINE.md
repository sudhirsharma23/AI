# Production CI/CD Pipeline for TextractProcessor

**Continuous Integration and Deployment Strategy**

> **Recommended Tool:** GitHub Actions (AWS Native Integration)  
> **Alternative Tools:** AWS CodePipeline, Azure DevOps  
> **Last Updated:** January 2025

---

## Table of Contents

1. [CI/CD Tool Comparison](#cicd-tool-comparison)
2. [Recommended Approach: GitHub Actions](#recommended-approach-github-actions)
3. [GitHub Actions Implementation](#github-actions-implementation)
4. [Alternative: AWS CodePipeline](#alternative-aws-codepipeline)
5. [Alternative: Azure DevOps](#alternative-azure-devops)
6. [Build & Test Process](#build--test-process)
7. [Deployment Workflow](#deployment-workflow)
8. [Environment Strategy](#environment-strategy)
9. [Security & Secrets Management](#security--secrets-management)
10. [Monitoring & Rollback](#monitoring--rollback)

---

## CI/CD Tool Comparison

### Feature Comparison Matrix

| Feature | GitHub Actions | AWS CodePipeline | Azure DevOps |
|---------|---------------|------------------|--------------|
| **Native AWS Integration** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Ease of Setup** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Cost (< 2000 min/month)** | FREE | $1/pipeline/month | FREE |
| **YAML Configuration** | ✅ | ❌ (UI-based) | ✅ |
| **Version Control Integration** | ✅ Native | ✅ Via CodeCommit | ✅ Native |
| **Multi-Cloud Support** | ✅ | ❌ | ✅ |
| **Community Marketplace** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ |
| **Self-Hosted Runners** | ✅ | ❌ | ✅ |
| **.NET 8/9 Support** | ✅ Built-in | ✅ | ✅ Built-in |
| **Lambda Deployment** | ✅ Simple | ✅ Native | ✅ Via AWS CLI |
| **Infrastructure as Code** | ✅ | ✅ | ✅ |
| **Pull Request Previews** | ✅ | ❌ | ✅ |

### Cost Comparison (Monthly)

**Scenario: 100 builds/month, 10 minutes each**

| Tool | Free Tier | Cost Beyond Free Tier |
|------|-----------|----------------------|
| **GitHub Actions** | 2000 minutes/month (Public repos: Unlimited) | $0.008/minute |
| **AWS CodePipeline** | 1 free pipeline | $1/pipeline/month |
| **Azure DevOps** | 1800 minutes/month | $40/user/month |

**For this project:**
- **GitHub Actions:** FREE (well within free tier)
- **AWS CodePipeline:** $2/month (dev + prod pipelines)
- **Azure DevOps:** FREE (well within free tier)

---

## Recommended Approach: GitHub Actions

### Why GitHub Actions?

✅ **Best Choice for AWS Lambda + .NET**

**Reasons:**
1. **Native GitHub Integration:** Code already on GitHub
2. **AWS Ecosystem:** Excellent AWS SDK and CLI support
3. **Free for Public/Private Repos:** 2000 minutes/month free
4. **YAML Configuration:** Infrastructure as Code approach
5. **Marketplace Actions:** Pre-built AWS deployment actions
6. **Matrix Builds:** Test multiple .NET versions simultaneously
7. **Secrets Management:** GitHub Secrets + AWS Secrets Manager
8. **Community Support:** Large community, extensive documentation

---

## GitHub Actions Implementation

### Repository Structure

```
TextractProcessor/
├── .github/
│   └── workflows/
│       ├── ci-build-test.yml       # PR validation
│       ├── cd-deploy-dev.yml # Deploy to dev
│├── cd-deploy-prod.yml      # Deploy to production
│       └── infrastructure-deploy.yml  # CloudFormation/Terraform
├── src/
│   └── TextractProcessor/
│       ├── TextractProcessor.csproj
│       └── Function.cs
├── tests/
│   └── TextractProcessor.Tests/
│     └── FunctionTests.cs
├── infrastructure/
│   ├── cloudformation/
│   │   ├── template.yml
│   │   └── parameters-prod.json
│   └── terraform/
│       ├── main.tf
│       └── variables.tf
├── buildspec.yml           # Optional: For CodeBuild
└── README.md
```

### Workflow 1: CI - Build & Test (PR Validation)

**File:** `.github/workflows/ci-build-test.yml`

```yaml
name: CI - Build and Test

on:
  pull_request:
    branches: [ main, develop ]
    paths:
      - 'src/**'
   - 'tests/**'
   - '.github/workflows/ci-build-test.yml'
  push:
    branches: [ develop ]

env:
  DOTNET_VERSION: '8.0.x'
  SOLUTION_PATH: 'TextractProcessor/src/TextractProcessor.sln'

jobs:
  build-and-test:
    name: Build and Test
    runs-on: ubuntu-latest
    
    strategy:
      matrix:
      dotnet: [ '8.0.x', '9.0.x' ]
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
  - name: Setup .NET ${{ matrix.dotnet }}
        uses: actions/setup-dotnet@v4
        with:
     dotnet-version: ${{ matrix.dotnet }}
      
      - name: Restore dependencies
    run: dotnet restore ${{ env.SOLUTION_PATH }}
  
      - name: Build solution
        run: dotnet build ${{ env.SOLUTION_PATH }} --configuration Release --no-restore
      
      - name: Run unit tests
 run: dotnet test ${{ env.SOLUTION_PATH }} --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage"
      
- name: Upload code coverage
uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.cobertura.xml'
          fail_ci_if_error: false
      
      - name: Package Lambda
   run: |
          cd TextractProcessor/src/TextractProcessor
          dotnet lambda package --configuration Release --output-package ../../../build/function.zip
      
      - name: Upload build artifact
 uses: actions/upload-artifact@v4
 with:
          name: lambda-package-${{ matrix.dotnet }}
 path: build/function.zip
          retention-days: 7

  code-quality:
    name: Code Quality Checks
    runs-on: ubuntu-latest
    
  steps:
    - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Setup .NET
      uses: actions/setup-dotnet@v4
        with:
      dotnet-version: ${{ env.DOTNET_VERSION }}
      
      - name: Restore dependencies
        run: dotnet restore ${{ env.SOLUTION_PATH }}
      
      - name: Run code analysis
     run: dotnet build ${{ env.SOLUTION_PATH }} --configuration Release /p:TreatWarningsAsErrors=true
    
      - name: Run security scan
        uses: security-code-scan/security-code-scan-action@v3
        with:
          project: ${{ env.SOLUTION_PATH }}
```

### Workflow 2: CD - Deploy to Dev

**File:** `.github/workflows/cd-deploy-dev.yml`

```yaml
name: CD - Deploy to Dev

on:
  push:
    branches: [ develop ]
  paths:
      - 'src/**'
  workflow_dispatch:  # Manual trigger

env:
  AWS_REGION: us-east-1
  LAMBDA_FUNCTION_NAME: textract-processor-dev
  DOTNET_VERSION: '8.0.x'

jobs:
  deploy-dev:
    name: Deploy to Development
    runs-on: ubuntu-latest
    environment:
      name: development
      url: https://console.aws.amazon.com/lambda/home?region=us-east-1#/functions/${{ env.LAMBDA_FUNCTION_NAME }}
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Setup .NET
      uses: actions/setup-dotnet@v4
        with:
   dotnet-version: ${{ env.DOTNET_VERSION }}
      
      - name: Install Amazon.Lambda.Tools
        run: dotnet tool install -g Amazon.Lambda.Tools
      
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
     aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID_DEV }}
   aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY_DEV }}
          aws-region: ${{ env.AWS_REGION }}
     role-to-assume: ${{ secrets.AWS_ROLE_ARN_DEV }}
role-duration-seconds: 3600
      
 - name: Build and package Lambda
     run: |
    cd TextractProcessor/src/TextractProcessor
dotnet lambda package --configuration Release --output-package function.zip
      
      - name: Deploy to Lambda
 run: |
    cd TextractProcessor/src/TextractProcessor
    dotnet lambda deploy-function ${{ env.LAMBDA_FUNCTION_NAME }} \
      --function-role ${{ secrets.AWS_LAMBDA_ROLE_ARN }} \
            --function-handler TextractProcessor::TextractProcessor.Function::FunctionHandler \
      --function-memory-size 1024 \
        --function-timeout 900 \
     --package function.zip \
         --region ${{ env.AWS_REGION }}
   
      - name: Update Lambda environment variables
        run: |
 aws lambda update-function-configuration \
   --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
--environment Variables="{
              BUCKET_NAME=${{ secrets.S3_BUCKET_DEV }},
    TEXTRACT_ROLE_ARN=${{ secrets.TEXTRACT_ROLE_ARN }},
  SNS_TOPIC_ARN=${{ secrets.SNS_TOPIC_ARN_DEV }},
        OUTPUT_DIRECTORY=/tmp/CachedFiles_OutputFiles,
      ENVIRONMENT=development
   }" \
            --region ${{ env.AWS_REGION }}
 
      - name: Publish new version
        id: publish
        run: |
          VERSION=$(aws lambda publish-version \
            --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
            --query 'Version' \
      --output text)
          echo "version=$VERSION" >> $GITHUB_OUTPUT
 
      - name: Run smoke tests
        run: |
   RESPONSE=$(aws lambda invoke \
       --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
      --payload '{"test": true}' \
         --log-type Tail \
  response.json)
          
          cat response.json
    
     if grep -q "error" response.json; then
          echo "Smoke test failed"
            exit 1
          fi
      
      - name: Create deployment tag
 run: |
    git tag "dev-${{ steps.publish.outputs.version }}-$(date +%Y%m%d-%H%M%S)"
          git push origin --tags
      
   - name: Notify deployment status
        if: always()
        uses: 8398a7/action-slack@v3
    with:
        status: ${{ job.status }}
  text: |
            Dev deployment ${{ job.status }}
            Function: ${{ env.LAMBDA_FUNCTION_NAME }}
            Version: ${{ steps.publish.outputs.version }}
          webhook_url: ${{ secrets.SLACK_WEBHOOK }}
```

### Workflow 3: CD - Deploy to Production

**File:** `.github/workflows/cd-deploy-prod.yml`

```yaml
name: CD - Deploy to Production

on:
  push:
    branches: [ main ]
    paths:
      - 'src/**'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to deploy'
  required: true

env:
  AWS_REGION: us-east-1
  LAMBDA_FUNCTION_NAME: textract-processor-prod
  DOTNET_VERSION: '8.0.x'

jobs:
  deploy-prod:
    name: Deploy to Production
    runs-on: ubuntu-latest
    environment:
      name: production
      url: https://console.aws.amazon.com/lambda/home?region=us-east-1#/functions/${{ env.LAMBDA_FUNCTION_NAME }}
    
    steps:
      - name: Checkout code
 uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
 dotnet-version: ${{ env.DOTNET_VERSION }}
      
      - name: Install Amazon.Lambda.Tools
  run: dotnet tool install -g Amazon.Lambda.Tools
      
      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID_PROD }}
    aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY_PROD }}
          aws-region: ${{ env.AWS_REGION }}
  role-to-assume: ${{ secrets.AWS_ROLE_ARN_PROD }}
    role-duration-seconds: 3600
      
      - name: Build and package Lambda
        run: |
          cd TextractProcessor/src/TextractProcessor
          dotnet lambda package --configuration Release --output-package function.zip
      
      - name: Get current Lambda version
        id: current
run: |
        CURRENT=$(aws lambda get-function \
  --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
  --query 'Configuration.Version' \
            --output text)
          echo "version=$CURRENT" >> $GITHUB_OUTPUT
  
      - name: Deploy to Lambda
        run: |
    cd TextractProcessor/src/TextractProcessor
      dotnet lambda deploy-function ${{ env.LAMBDA_FUNCTION_NAME }} \
       --function-role ${{ secrets.AWS_LAMBDA_ROLE_ARN }} \
   --function-handler TextractProcessor::TextractProcessor.Function::FunctionHandler \
       --function-memory-size 1024 \
            --function-timeout 900 \
      --package function.zip \
  --region ${{ env.AWS_REGION }}
    
      - name: Update Lambda environment variables
        run: |
        aws lambda update-function-configuration \
      --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
     --environment Variables="{
    BUCKET_NAME=${{ secrets.S3_BUCKET_PROD }},
              TEXTRACT_ROLE_ARN=${{ secrets.TEXTRACT_ROLE_ARN }},
         SNS_TOPIC_ARN=${{ secrets.SNS_TOPIC_ARN_PROD }},
 OUTPUT_DIRECTORY=/tmp/CachedFiles_OutputFiles,
         ENVIRONMENT=production
            }" \
 --region ${{ env.AWS_REGION }}
      
      - name: Publish new version
        id: publish
  run: |
 VERSION=$(aws lambda publish-version \
        --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
    --description "Deployed from GitHub Actions - ${{ github.sha }}" \
    --query 'Version' \
  --output text)
     echo "version=$VERSION" >> $GITHUB_OUTPUT
      
      - name: Create alias for new version
        run: |
     aws lambda update-alias \
        --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
    --name production \
        --function-version ${{ steps.publish.outputs.version }} \
            --region ${{ env.AWS_REGION }} || \
     aws lambda create-alias \
     --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
     --name production \
    --function-version ${{ steps.publish.outputs.version }} \
            --region ${{ env.AWS_REGION }}
      
      - name: Run integration tests
        run: |
  # Upload test file to S3
   aws s3 cp tests/fixtures/sample.tif \
            s3://${{ secrets.S3_BUCKET_PROD }}/incoming/test-$(date +%s).tif
       
        # Wait for processing (check DynamoDB or poll Lambda logs)
     sleep 60
          
          # Verify output exists
  aws s3 ls s3://${{ secrets.S3_BUCKET_PROD }}/processed/ || exit 1
      
   - name: Monitor Lambda errors
  run: |
    ERRORS=$(aws cloudwatch get-metric-statistics \
     --namespace AWS/Lambda \
   --metric-name Errors \
            --dimensions Name=FunctionName,Value=${{ env.LAMBDA_FUNCTION_NAME }} \
     --start-time $(date -u -d '5 minutes ago' +%Y-%m-%dT%H:%M:%S) \
            --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
       --period 300 \
   --statistics Sum \
        --query 'Datapoints[0].Sum' \
    --output text)
 
     if [ "$ERRORS" != "None" ] && [ "$ERRORS" -gt "0" ]; then
         echo "Lambda errors detected: $ERRORS"
            exit 1
          fi
   
      - name: Create GitHub release
        uses: actions/create-release@v1
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          tag_name: v${{ steps.publish.outputs.version }}
          release_name: Production Release v${{ steps.publish.outputs.version }}
          body: |
    **Deployed to Production**
      
      - Lambda Version: ${{ steps.publish.outputs.version }}
         - Previous Version: ${{ steps.current.outputs.version }}
            - Commit: ${{ github.sha }}
      - Deployed by: ${{ github.actor }}
         
        **Changes:**
      ${{ github.event.head_commit.message }}
          draft: false
     prerelease: false
    
      - name: Notify deployment status
        if: always()
        uses: 8398a7/action-slack@v3
        with:
    status: ${{ job.status }}
       text: |
    🚀 Production deployment ${{ job.status }}
  Function: ${{ env.LAMBDA_FUNCTION_NAME }}
    Version: ${{ steps.publish.outputs.version }}
            Previous: ${{ steps.current.outputs.version }}
  webhook_url: ${{ secrets.SLACK_WEBHOOK }}

  rollback-on-failure:
    name: Rollback on Failure
    needs: deploy-prod
    runs-on: ubuntu-latest
    if: failure()
    
    steps:
- name: Configure AWS credentials
uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID_PROD }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY_PROD }}
          aws-region: ${{ env.AWS_REGION }}
      
- name: Get previous version
        id: previous
        run: |
          VERSIONS=$(aws lambda list-versions-by-function \
--function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
         --query 'Versions[?Version!=`$LATEST`].Version' \
       --output text)
       
          # Get second-to-last version
          PREVIOUS=$(echo $VERSIONS | awk '{print $(NF-1)}')
  echo "version=$PREVIOUS" >> $GITHUB_OUTPUT
      
      - name: Rollback to previous version
        run: |
    aws lambda update-alias \
            --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
--name production \
            --function-version ${{ steps.previous.outputs.version }} \
    --region ${{ env.AWS_REGION }}
   
      - name: Notify rollback
        uses: 8398a7/action-slack@v3
        with:
        status: custom
       custom_payload: |
            {
              text: '⚠️ Production deployment failed - Rolled back to version ${{ steps.previous.outputs.version }}'
     }
 webhook_url: ${{ secrets.SLACK_WEBHOOK }}
```

### Workflow 4: Infrastructure Deployment

**File:** `.github/workflows/infrastructure-deploy.yml`

```yaml
name: Infrastructure Deployment

on:
  push:
    branches: [ main ]
    paths:
      - 'infrastructure/**'
  workflow_dispatch:
    inputs:
   environment:
  description: 'Environment to deploy'
        required: true
        type: choice
        options:
       - dev
          - prod

env:
  AWS_REGION: us-east-1

jobs:
  deploy-infrastructure:
    name: Deploy Infrastructure
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Configure AWS credentials
 uses: aws-actions/configure-aws-credentials@v4
  with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
  aws-region: ${{ env.AWS_REGION }}
   
 - name: Deploy CloudFormation stack
  run: |
     aws cloudformation deploy \
       --template-file infrastructure/cloudformation/template.yml \
      --stack-name textract-processor-${{ github.event.inputs.environment || 'dev' }} \
 --parameter-overrides file://infrastructure/cloudformation/parameters-${{ github.event.inputs.environment || 'dev' }}.json \
            --capabilities CAPABILITY_IAM \
    --region ${{ env.AWS_REGION }}
      
      - name: Get stack outputs
        run: |
          aws cloudformation describe-stacks \
  --stack-name textract-processor-${{ github.event.inputs.environment || 'dev' }} \
            --query 'Stacks[0].Outputs' \
            --output table
```

---

## Alternative: AWS CodePipeline

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│    AWS CodePipeline Architecture    │
├──────────────────────────────────────────────────────────────┤
│   │
│  GitHub Repository (Source)  │
│  ↓     │
│  CodeBuild (Build & Test)  │
│        ↓    │
│  CodeBuild (Package Lambda)   │
│        ↓    │
│  Manual Approval (Production Only)   │
│        ↓      │
│  CloudFormation (Deploy Infrastructure)    │
│        ↓    │
│  CodeDeploy (Deploy Lambda) │
│        ↓     │
│  CloudWatch Alarms (Monitor)    │
└──────────────────────────────────────────────────────────────┘
```

### buildspec.yml (CodeBuild)

```yaml
version: 0.2

phases:
  install:
    runtime-versions:
      dotnet: 8
    commands:
    - dotnet tool install -g Amazon.Lambda.Tools
  
  pre_build:
  commands:
      - echo Restoring dependencies...
      - dotnet restore TextractProcessor/src/TextractProcessor/TextractProcessor.csproj
  
  build:
    commands:
      - echo Build started on `date`
 - cd TextractProcessor/src/TextractProcessor
    - dotnet build --configuration Release
      - dotnet test ../../tests/TextractProcessor.Tests/TextractProcessor.Tests.csproj
  
  post_build:
    commands:
      - echo Packaging Lambda function...
      - dotnet lambda package --configuration Release --output-package $CODEBUILD_SRC_DIR/function.zip
      - echo Build completed on `date`

artifacts:
  files:
    - function.zip
    - infrastructure/cloudformation/template.yml
  discard-paths: no
```

### AWS CLI Commands (One-time Setup)

```bash
# Create CodeBuild project
aws codebuild create-project \
  --name textract-processor-build \
  --source type=GITHUB,location=https://github.com/your-org/textract-processor \
  --artifacts type=S3,location=textract-build-artifacts \
  --environment type=LINUX_CONTAINER,image=aws/codebuild/standard:7.0,computeType=BUILD_GENERAL1_SMALL \
  --service-role arn:aws:iam::123456789012:role/CodeBuildServiceRole

# Create CodePipeline
aws codepipeline create-pipeline \
  --cli-input-json file://pipeline-config.json
```

**pipeline-config.json:**
```json
{
  "pipeline": {
    "name": "textract-processor-pipeline",
    "roleArn": "arn:aws:iam::123456789012:role/CodePipelineServiceRole",
    "stages": [
      {
        "name": "Source",
        "actions": [
          {
            "name": "SourceAction",
            "actionTypeId": {
              "category": "Source",
              "owner": "ThirdParty",
              "provider": "GitHub",
    "version": "1"
  },
        "configuration": {
    "Owner": "your-github-org",
        "Repo": "textract-processor",
              "Branch": "main",
              "OAuthToken": "{{resolve:secretsmanager:github-token}}"
    },
            "outputArtifacts": [
           {
       "name": "SourceOutput"
        }
        ]
          }
    ]
      },
      {
        "name": "Build",
  "actions": [
      {
         "name": "BuildAction",
    "actionTypeId": {
   "category": "Build",
              "owner": "AWS",
              "provider": "CodeBuild",
    "version": "1"
        },
         "configuration": {
      "ProjectName": "textract-processor-build"
 },
            "inputArtifacts": [
      {
         "name": "SourceOutput"
           }
  ],
    "outputArtifacts": [
     {
    "name": "BuildOutput"
        }
      ]
      }
        ]
      },
      {
        "name": "Deploy",
        "actions": [
          {
      "name": "DeployAction",
       "actionTypeId": {
     "category": "Deploy",
   "owner": "AWS",
              "provider": "CloudFormation",
 "version": "1"
       },
            "configuration": {
       "ActionMode": "CREATE_UPDATE",
   "StackName": "textract-processor-prod",
  "TemplatePath": "BuildOutput::infrastructure/cloudformation/template.yml",
         "Capabilities": "CAPABILITY_IAM",
         "RoleArn": "arn:aws:iam::123456789012:role/CloudFormationServiceRole"
 },
     "inputArtifacts": [
       {
         "name": "BuildOutput"
 }
            ]
          }
        ]
    }
    ]
  }
}
```

---

## Alternative: Azure DevOps

### azure-pipelines.yml

```yaml
trigger:
  branches:
    include:
      - main
      - develop
  paths:
    include:
      - src/**

pool:
  vmImage: 'ubuntu-latest'

variables:
  dotnetVersion: '8.0.x'
  buildConfiguration: 'Release'
  awsRegion: 'us-east-1'

stages:
  - stage: Build
    displayName: 'Build and Test'
    jobs:
   - job: BuildJob
        displayName: 'Build Lambda Function'
        steps:
   - task: UseDotNet@2
      displayName: 'Install .NET SDK'
  inputs:
              version: $(dotnetVersion)
 
          - task: DotNetCoreCLI@2
   displayName: 'Restore dependencies'
  inputs:
  command: 'restore'
        projects: 'TextractProcessor/src/TextractProcessor/TextractProcessor.csproj'
          
     - task: DotNetCoreCLI@2
        displayName: 'Build project'
        inputs:
        command: 'build'
              projects: 'TextractProcessor/src/TextractProcessor/TextractProcessor.csproj'
  arguments: '--configuration $(buildConfiguration) --no-restore'
          
      - task: DotNetCoreCLI@2
       displayName: 'Run tests'
inputs:
              command: 'test'
     projects: 'TextractProcessor/tests/**/*.csproj'
  arguments: '--configuration $(buildConfiguration) --no-build'
          
          - script: |
   dotnet tool install -g Amazon.Lambda.Tools
     cd TextractProcessor/src/TextractProcessor
          dotnet lambda package --configuration $(buildConfiguration) --output-package $(Build.ArtifactStagingDirectory)/function.zip
         displayName: 'Package Lambda function'
          
        - task: PublishBuildArtifacts@1
            displayName: 'Publish artifacts'
            inputs:
              PathtoPublish: '$(Build.ArtifactStagingDirectory)'
           ArtifactName: 'lambda-package'

  - stage: DeployDev
    displayName: 'Deploy to Development'
    dependsOn: Build
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/develop'))
    jobs:
   - deployment: DeployDevJob
        displayName: 'Deploy to Dev Environment'
        environment: 'development'
        strategy:
          runOnce:
       deploy:
           steps:
 - task: AWSCLI@1
         displayName: 'Configure AWS CLI'
      inputs:
 awsCredentials: 'AWS-Dev-ServiceConnection'
      regionName: $(awsRegion)
  
     - task: AWSCLI@1
     displayName: 'Deploy Lambda function'
          inputs:
           awsCredentials: 'AWS-Dev-ServiceConnection'
           regionName: $(awsRegion)
         awsCommand: 'lambda'
              awsSubCommand: 'update-function-code'
        awsArguments: '--function-name textract-processor-dev --zip-file fileb://$(Pipeline.Workspace)/lambda-package/function.zip'

  - stage: DeployProd
    displayName: 'Deploy to Production'
    dependsOn: Build
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - deployment: DeployProdJob
        displayName: 'Deploy to Production Environment'
        environment: 'production'
        strategy:
          runOnce:
            deploy:
              steps:
     - task: ManualValidation@0
   displayName: 'Manual approval required'
      inputs:
            notifyUsers: 'devops-team@example.com'
 instructions: 'Please review and approve production deployment'
       
          - task: AWSCLI@1
          displayName: 'Deploy Lambda function'
         inputs:
     awsCredentials: 'AWS-Prod-ServiceConnection'
        regionName: $(awsRegion)
awsCommand: 'lambda'
      awsSubCommand: 'update-function-code'
           awsArguments: '--function-name textract-processor-prod --zip-file fileb://$(Pipeline.Workspace)/lambda-package/function.zip'
```

---

## Build & Test Process

### Local Build Script

**File:** `scripts/build.sh`

```bash
#!/bin/bash
set -e

echo "========================================="
echo "TextractProcessor Build Script"
echo "========================================="

# Variables
PROJECT_DIR="TextractProcessor/src/TextractProcessor"
TEST_DIR="TextractProcessor/tests/TextractProcessor.Tests"
OUTPUT_DIR="build"
CONFIGURATION="Release"

# Clean previous build
echo "Cleaning previous build..."
rm -rf $OUTPUT_DIR
mkdir -p $OUTPUT_DIR

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore $PROJECT_DIR/TextractProcessor.csproj

# Build project
echo "Building project..."
dotnet build $PROJECT_DIR/TextractProcessor.csproj \
  --configuration $CONFIGURATION \
  --no-restore

# Run tests
echo "Running tests..."
dotnet test $TEST_DIR/TextractProcessor.Tests.csproj \
  --configuration $CONFIGURATION \
  --no-build \
  --verbosity normal

# Package Lambda
echo "Packaging Lambda function..."
cd $PROJECT_DIR
dotnet lambda package \
  --configuration $CONFIGURATION \
  --output-package ../../../$OUTPUT_DIR/function.zip

cd ../../..

echo "========================================="
echo "Build completed successfully!"
echo "Package location: $OUTPUT_DIR/function.zip"
echo "========================================="
```

### Unit Test Structure

**File:** `tests/TextractProcessor.Tests/FunctionTests.cs`

```csharp
using Xunit;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.S3Events;
using TextractProcessor;

namespace TextractProcessor.Tests
{
    public class FunctionTests
 {
        [Fact]
        public async Task TestFunctionHandler()
        {
          // Arrange
            var function = new Function();
            var context = new TestLambdaContext();
     var s3Event = new S3Event
         {
   Records = new List<S3Event.S3EventNotificationRecord>
   {
                    new S3Event.S3EventNotificationRecord
       {
        S3 = new S3Event.S3Entity
   {
          Bucket = new S3Event.S3BucketEntity { Name = "test-bucket" },
    Object = new S3Event.S3ObjectEntity { Key = "test.tif" }
           }
          }
}
   };

   // Act
        var response = await function.FunctionHandler(s3Event, context);

            // Assert
            Assert.NotNull(response);
    Assert.Equal(200, response.StatusCode);
    }

        [Fact]
        public void TestConfigurationLoading()
        {
            // Arrange & Act
            var config = Configuration.LoadFromEnvironment();

    // Assert
      Assert.NotNull(config);
      Assert.NotEmpty(config.BucketName);
        }
    }
}
```

---

## Deployment Workflow

### Multi-Environment Deployment Strategy

```
┌───────────────────────────────────────────────────────────────┐
│     DEPLOYMENT WORKFLOW      │
├───────────────────────────────────────────────────────────────┤
│          │
│  Developer Push to Branch   │
│   ↓       │
│  ┌──────────────────────────────────────────────┐ │
│  │  Branch: develop      │     │
│  │  Trigger: Automatic    │     │
│  │  Target: Dev Environment      │    │
│  │  Approval: Not Required        │         │
│  └──────────────────┬───────────────────────────┘   │
│     ↓       │
│  Build → Test → Deploy to Dev    │
│     ↓    │
│  Smoke Tests (Automated)    │
│     ↓ │
│  ✅ Dev Validated        │
│              │
│  ┌──────────────────────────────────────────────┐        │
│  │  Merge develop → main         │        │
│  └──────────────────┬───────────────────────────┘      │
│      ↓      │
│  ┌──────────────────────────────────────────────┐    │
│  │  Branch: main        │      │
│  │  Trigger: Automatic        │        │
│  │  Target: Production Environment    │    │
│  │  Approval: REQUIRED (Manual)     │        │
│  └──────────────────┬───────────────────────────┘      │
│           ↓       │
│  Build → Test → Await Approval         │
│   ↓     │
│  👤 Manual Approval by DevOps Lead│
│          ↓   │
│  Deploy to Production       │
│     ↓    │
│  Integration Tests (Automated)   │
│         ↓    │
│  Monitor for 10 minutes    │
│         ↓     │
│  ✅ Production Validated    │
└───────────────────────────────────────────────────────────────┘
```

### Blue/Green Deployment with Lambda Aliases

```bash
# Current production: Alias "production" → Version 5
# New deployment: Version 6

# Step 1: Deploy new version
aws lambda publish-version \
  --function-name textract-processor-prod \
  --description "Version 6 - Feature XYZ"

# Step 2: Test new version directly
aws lambda invoke \
  --function-name textract-processor-prod:6 \
  --payload '{"test": true}' \
  response.json

# Step 3: Shift 10% traffic to new version (canary)
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 6 \
  --routing-config AdditionalVersionWeights={"5"=0.9}

# Step 4: Monitor for 10 minutes
# Check CloudWatch metrics, error rates, latency

# Step 5: Full cutover if successful
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 6

# Step 6: Rollback if needed
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 5
```

---

## Environment Strategy

### Environment Configuration

| Environment | Branch | Auto-Deploy | Approval Required | Monitoring |
|-------------|--------|-------------|-------------------|------------|
| **Development** | `develop` | ✅ Yes | ❌ No | Basic |
| **Staging** | `release/*` | ✅ Yes | ⚠️ Optional | Standard |
| **Production** | `main` | ✅ Yes | ✅ Required | Comprehensive |

### GitHub Environments Setup

**Development Environment:**
```yaml
# .github/environments/development.yml
name: development
url: https://dev.example.com
protection_rules:
  required_reviewers: 0
  wait_timer: 0
secrets:
  AWS_ACCESS_KEY_ID_DEV: ${{ secrets.AWS_ACCESS_KEY_ID_DEV }}
  AWS_SECRET_ACCESS_KEY_DEV: ${{ secrets.AWS_SECRET_ACCESS_KEY_DEV }}
  S3_BUCKET_DEV: textract-dev-bucket
```

**Production Environment:**
```yaml
# .github/environments/production.yml
name: production
url: https://prod.example.com
protection_rules:
  required_reviewers: 2
  wait_timer: 10  # 10 minutes cooldown
  allowed_branches:
    - main
secrets:
  AWS_ACCESS_KEY_ID_PROD: ${{ secrets.AWS_ACCESS_KEY_ID_PROD }}
  AWS_SECRET_ACCESS_KEY_PROD: ${{ secrets.AWS_SECRET_ACCESS_KEY_PROD }}
  S3_BUCKET_PROD: textract-prod-bucket
```

---

## Security & Secrets Management

### GitHub Secrets Configuration

**Required Secrets:**
```
AWS_ACCESS_KEY_ID_DEV
AWS_SECRET_ACCESS_KEY_DEV
AWS_ROLE_ARN_DEV

AWS_ACCESS_KEY_ID_PROD
AWS_SECRET_ACCESS_KEY_PROD
AWS_ROLE_ARN_PROD

AWS_LAMBDA_ROLE_ARN
TEXTRACT_ROLE_ARN

S3_BUCKET_DEV
S3_BUCKET_PROD

SNS_TOPIC_ARN_DEV
SNS_TOPIC_ARN_PROD

SLACK_WEBHOOK (optional)
```

### OIDC Authentication (Recommended)

**Instead of IAM user access keys, use OIDC:**

```yaml
- name: Configure AWS credentials
  uses: aws-actions/configure-aws-credentials@v4
  with:
    role-to-assume: arn:aws:iam::123456789012:role/GitHubActionsRole
    aws-region: us-east-1
```

**IAM Trust Policy for GitHub Actions:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
  "Federated": "arn:aws:iam::123456789012:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
     "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
   "token.actions.githubusercontent.com:sub": "repo:your-org/textract-processor:*"
   }
      }
    }
  ]
}
```

---

## Monitoring & Rollback

### Post-Deployment Monitoring

```yaml
- name: Monitor Lambda health
  run: |
    # Check error rate
    ERRORS=$(aws cloudwatch get-metric-statistics \
      --namespace AWS/Lambda \
      --metric-name Errors \
      --dimensions Name=FunctionName,Value=${{ env.LAMBDA_FUNCTION_NAME }} \
      --start-time $(date -u -d '5 minutes ago' +%Y-%m-%dT%H:%M:%S) \
      --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
      --period 300 \
    --statistics Sum \
      --query 'Datapoints[0].Sum' \
      --output text)
    
    if [ "$ERRORS" != "None" ] && [ "$ERRORS" -gt "5" ]; then
      echo "High error rate detected: $ERRORS errors"
      exit 1
    fi
    
    # Check duration
    DURATION=$(aws cloudwatch get-metric-statistics \
      --namespace AWS/Lambda \
      --metric-name Duration \
      --dimensions Name=FunctionName,Value=${{ env.LAMBDA_FUNCTION_NAME }} \
      --start-time $(date -u -d '5 minutes ago' +%Y-%m-%dT%H:%M:%S) \
      --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
      --period 300 \
      --statistics Average \
      --query 'Datapoints[0].Average' \
   --output text)
    
    if [ "$DURATION" != "None" ] && (( $(echo "$DURATION > 800000" | bc -l) )); then
      echo "High duration detected: $DURATION ms"
      exit 1
    fi
```

### Automated Rollback

```yaml
- name: Rollback on high error rate
  if: failure()
  run: |
    # Get all versions
    VERSIONS=$(aws lambda list-versions-by-function \
      --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
      --query 'Versions[?Version!=`$LATEST`].Version' \
   --output text)
  
    # Get previous stable version (second-to-last)
    PREVIOUS=$(echo $VERSIONS | awk '{print $(NF-1)}')
    
    # Rollback alias
    aws lambda update-alias \
      --function-name ${{ env.LAMBDA_FUNCTION_NAME }} \
      --name production \
      --function-version $PREVIOUS
    
    echo "Rolled back to version $PREVIOUS"
```

---

## Summary: Recommended Approach

### ✅ Use GitHub Actions Because:

1. **Native Git Integration:** Already using GitHub
2. **Free Tier:** Generous free tier for private repos
3. **YAML Configuration:** Infrastructure as Code
4. **AWS Marketplace Actions:** Pre-built AWS integrations
5. **Easy Setup:** 15-minute initial setup
6. **Community Support:** Large community, extensive docs
7. **Multi-Environment:** Built-in environment management
8. **Security:** OIDC authentication support

### Quick Start Checklist

- [ ] Create GitHub repository (if not exists)
- [ ] Add GitHub Secrets (AWS credentials)
- [ ] Copy workflow files to `.github/workflows/`
- [ ] Configure AWS IAM roles and policies
- [ ] Test CI workflow (create PR)
- [ ] Test CD workflow (merge to develop)
- [ ] Set up production environment protection
- [ ] Deploy to production (merge to main)
- [ ] Configure CloudWatch alarms
- [ ] Set up Slack notifications (optional)

---

**Next Steps:**
1. **[PRODUCTION_DEPLOYMENT_STRATEGY.md](PRODUCTION_DEPLOYMENT_STRATEGY.md)** - Detailed release strategies
2. **[PRODUCTION_INFRASTRUCTURE_CODE.md](PRODUCTION_INFRASTRUCTURE_CODE.md)** - CloudFormation/Terraform templates
3. **[PRODUCTION_MONITORING_SETUP.md](PRODUCTION_MONITORING_SETUP.md)** - Monitoring dashboards

---

**CI/CD Version:** 1.0  
**Last Updated:** January 2025  
**Recommended Tool:** GitHub Actions
