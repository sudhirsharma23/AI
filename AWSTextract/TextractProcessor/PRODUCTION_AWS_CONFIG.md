# Production AWS Configuration

**AWS Services Setup for Document Processing Pipeline**

> **Services:** S3, Textract, Bedrock, SNS, IAM, Lambda  
> **Purpose:** Complete AWS infrastructure configuration  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [S3 Bucket Setup](#s3-bucket-setup)
4. [IAM Roles and Policies](#iam-roles-and-policies)
5. [SNS Topic Configuration](#sns-topic-configuration)
6. [Textract Setup](#textract-setup)
7. [Bedrock Setup](#bedrock-setup)
8. [Lambda Configuration](#lambda-configuration)
9. [Security Best Practices](#security-best-practices)
10. [Verification](#verification)

---

## Overview

### Required AWS Services

| Service | Purpose | Cost |
|---------|---------|------|
| **S3** | Document storage | $0.023/GB/month |
| **Textract** | OCR extraction | $0.015/page |
| **Bedrock** | AI extraction | $0.0008-0.0024/1K tokens |
| **SNS** | Job notifications | $0.50/million requests |
| **IAM** | Access control | Free |
| **Lambda** | Processing runtime | $0.20/million requests |

### Total Estimated Cost

**For 100 documents/day (3 pages each):**
- S3: $0.50/month
- Textract: $13.50/month (300 pages × $0.045)
- Bedrock: $42.00/month (300 docs × $0.014)
- SNS: $0.15/month
- Lambda: $6.00/month
- **Total: ~$62/month**

---

## Architecture

### Service Interaction Diagram

```
???????????????????????????????????????????????????????????????????
?  AWS INFRASTRUCTURE ARCHITECTURE ?
???????????????????????????????????????????????????????????????????
?      ?
?  ?????????????   ?
?  ?   S3      ? Documents stored in date-based folders ?
?  ?  Bucket   ? s3://bucket/uploads/YYYY-MM-DD/...  ?
?  ?????????????  ?
?        ? Trigger    ?
?  ?????????????     ?
?  ?  Lambda ? Document processor function ?
?  ? Function  ? Invoked manually or on S3 event  ?
?  ?????????????      ?
?   ? Read    ?
?  ?????????????     ?
?  ?  Textract ? Async OCR jobs   ?
?  ?  Service  ? Extracts text, forms, tables ?
?  ?????????????      ?
?        ? Notify ?
?  ?????????????  ?
?  ?    SNS    ? Job completion notifications ?
?  ?   Topic   ? Status: SUCCEEDED / FAILED?
?  ?????????????  ?
?        ? Subscribe  ?
?  ?????????????   ?
?  ?  Lambda   ? Poll or receive notification  ?
?  ?????????????     ?
?        ? Process?
?  ?????????????     ?
?  ?  Bedrock  ? AI extraction   ?
?  ?   Model   ? Nova, Qwen, Claude, Titan  ?
?  ?????????????     ?
?        ? Save   ?
?  ?????????????      ?
?  ?  Lambda   ? Write results to disk/S3 ?
?  ?  Storage  ? JSON + analysis reports?
?  ?????????????  ?
?       ?
?  ?????????????      ?
?  ?    IAM    ? Controls all access?
?  ?   Roles   ? Lambda ? S3, Textract, Bedrock?
?  ?????????????      ?
???????????????????????????????????????????????????????????????????
```

---

## S3 Bucket Setup

### Create S3 Bucket

**AWS Console:**
1. Navigate to S3
2. Click "Create bucket"
3. Bucket name: `testbucket-sudhir-bsi1` (must be globally unique)
4. Region: `us-east-1` (or your preferred region)
5. Block Public Access: **Enabled** (recommended)
6. Versioning: Optional
7. Create bucket

**AWS CLI:**
```bash
aws s3 mb s3://testbucket-sudhir-bsi1 --region us-east-1
```

### Folder Structure

```
s3://testbucket-sudhir-bsi1/
??? uploads/
    ??? 2025-01-20/
    ?   ??? deed_001/
    ?   ?   ??? deed_001.tif
    ?   ??? contract_002/
    ?       ??? contract_002.pdf
    ??? 2025-01-21/
        ??? invoice_003/
        ??? invoice_003.tif
```

### Bucket Policy (Optional - For Cross-Account Access)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowTextractAccess",
    "Effect": "Allow",
      "Principal": {
        "Service": "textract.amazonaws.com"
      },
      "Action": [
        "s3:GetObject"
      ],
    "Resource": "arn:aws:s3:::testbucket-sudhir-bsi1/*",
      "Condition": {
    "StringEquals": {
   "aws:SourceAccount": "912532823432"
        }
    }
    }
  ]
}
```

---

## IAM Roles and Policies

### 1. Textract Service Role

**Role Name:** `accesstextract-role`  
**ARN:** `arn:aws:iam::912532823432:role/accesstextract-role`

**Trust Relationship:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "textract.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

**Policy: `TextractServicePolicy`**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
    "Sid": "S3ReadAccess",
      "Effect": "Allow",
      "Action": [
   "s3:GetObject",
  "s3:ListBucket"
 ],
      "Resource": [
    "arn:aws:s3:::testbucket-sudhir-bsi1",
        "arn:aws:s3:::testbucket-sudhir-bsi1/*"
      ]
    },
    {
      "Sid": "SNSPublishAccess",
      "Effect": "Allow",
      "Action": [
        "sns:Publish"
    ],
      "Resource": "arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo"
    }
  ]
}
```

**Create Role (AWS CLI):**
```bash
# Create trust policy file
cat > textract-trust-policy.json << EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
 "Service": "textract.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
EOF

# Create role
aws iam create-role \
  --role-name accesstextract-role \
  --assume-role-policy-document file://textract-trust-policy.json

# Attach policy
aws iam put-role-policy \
  --role-name accesstextract-role \
  --policy-name TextractServicePolicy \
  --policy-document file://textract-service-policy.json
```

---

### 2. Lambda Execution Role

**Role Name:** `lambda-textract-processor-role`  
**Purpose:** Allow Lambda to access S3, Textract, Bedrock

**Trust Relationship:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
   "Effect": "Allow",
      "Principal": {
        "Service": "lambda.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

**Policy: `LambdaTextractProcessorPolicy`**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
 "Sid": "S3Access",
      "Effect": "Allow",
      "Action": [
     "s3:GetObject",
        "s3:PutObject",
      "s3:ListBucket"
 ],
      "Resource": [
    "arn:aws:s3:::testbucket-sudhir-bsi1",
     "arn:aws:s3:::testbucket-sudhir-bsi1/*"
    ]
    },
    {
      "Sid": "TextractAccess",
    "Effect": "Allow",
 "Action": [
        "textract:StartDocumentAnalysis",
"textract:GetDocumentAnalysis"
      ],
      "Resource": "*"
    },
    {
      "Sid": "BedrockAccess",
      "Effect": "Allow",
   "Action": [
        "bedrock:InvokeModel",
    "bedrock:InvokeModelWithResponseStream"
      ],
      "Resource": [
        "arn:aws:bedrock:*::foundation-model/amazon.nova-lite-v1:0",
        "arn:aws:bedrock:*::foundation-model/qwen.qwen2.5-7b-instruct",
        "arn:aws:bedrock:*::foundation-model/anthropic.claude-3-haiku*",
        "arn:aws:bedrock:*::foundation-model/amazon.titan-text-express-v1"
      ]
    },
    {
      "Sid": "CloudWatchLogs",
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogGroup",
  "logs:CreateLogStream",
        "logs:PutLogEvents"
      ],
      "Resource": "arn:aws:logs:*:*:*"
    }
  ]
}
```

---

## SNS Topic Configuration

### Create SNS Topic

**Topic Name:** `sns-topic-textract.fifo`  
**Type:** FIFO (First-In-First-Out)  
**ARN:** `arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo`

**AWS Console:**
1. Navigate to SNS
2. Click "Create topic"
3. Type: **FIFO**
4. Name: `sns-topic-textract.fifo`
5. Content-based deduplication: **Enabled**
6. Create topic

**AWS CLI:**
```bash
aws sns create-topic \
  --name sns-topic-textract.fifo \
  --attributes FifoTopic=true,ContentBasedDeduplication=true \
  --region us-east-1
```

### SNS Topic Policy

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowTextractPublish",
      "Effect": "Allow",
    "Principal": {
    "Service": "textract.amazonaws.com"
      },
      "Action": "SNS:Publish",
      "Resource": "arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo",
      "Condition": {
        "StringEquals": {
          "aws:SourceAccount": "912532823432"
        }
      }
    }
  ]
}
```

### Subscribe Lambda to SNS (Optional)

If you want Lambda to be notified automatically:

```bash
aws sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo \
  --protocol lambda \
  --notification-endpoint arn:aws:lambda:us-east-1:912532823432:function:textract-processor
```

---

## Textract Setup

### Enable Textract

Textract is available by default in AWS accounts. No special activation needed.

### Supported Regions

Textract is available in:
- us-east-1 (N. Virginia)
- us-east-2 (Ohio)
- us-west-1 (N. California)
- us-west-2 (Oregon)
- eu-west-1 (Ireland)
- And others...

### Feature Types

```csharp
FeatureTypes = new List<string> 
{ 
    "TABLES",    // Extract table structure
    "FORMS",     // Extract key-value pairs
  "LAYOUT",  // Analyze document layout
  "SIGNATURES" // Detect signatures (optional)
};
```

### Service Limits

| Limit | Value |
|-------|-------|
| Max file size (S3) | 512 MB |
| Max pages per document | 3,000 pages |
| Concurrent jobs | 100 (can request increase) |
| API rate limit | 10 requests/sec |

---

## Bedrock Setup

### Enable Bedrock Models

**AWS Console:**
1. Navigate to Bedrock
2. Click "Model access"
3. Request access to:
   - Amazon Nova Lite (`amazon.nova-lite-v1:0`)
   - Qwen 2.5 (`qwen.qwen2.5-7b-instruct`)
   - Claude 3 Haiku (`anthropic.claude-3-haiku-20240307-v1:0`)
   - Amazon Titan Text Express (`amazon.titan-text-express-v1`)

**AWS CLI:**
```bash
aws bedrock get-foundation-model \
  --model-identifier qwen.qwen2.5-7b-instruct \
  --region us-west-2
```

### Model Availability by Region

| Model | Regions |
|-------|---------|
| **Nova Lite** | us-west-1, us-west-2 |
| **Qwen 3** | us-west-2 |
| **Claude 3** | us-east-1, us-west-2, eu-west-1 |
| **Titan Express** | us-east-1, us-west-2 |

### Bedrock Pricing

| Model | Input (per 1K tokens) | Output (per 1K tokens) |
|-------|----------------------|------------------------|
| Nova Lite | $0.0004 | $0.0012 |
| Qwen 3 | $0.0008 | $0.0024 |
| Claude 3 | $0.00025 | $0.00125 |
| Titan | $0.0008 | $0.0016 |

---

## Lambda Configuration

### Create Lambda Function

**Function Name:** `textract-processor`  
**Runtime:** .NET 8  
**Architecture:** x86_64  
**Memory:** 512 MB  
**Timeout:** 15 minutes (900 seconds)

**AWS Console:**
1. Navigate to Lambda
2. Click "Create function"
3. Function name: `textract-processor`
4. Runtime: .NET 8
5. Architecture: x86_64
6. Execution role: Use existing role `lambda-textract-processor-role`
7. Create function

**AWS CLI:**
```bash
aws lambda create-function \
  --function-name textract-processor \
  --runtime dotnet8 \
  --role arn:aws:iam::912532823432:role/lambda-textract-processor-role \
  --handler TextractProcessor::TextractProcessor.Function::FunctionHandler \
  --zip-file fileb://function.zip \
  --timeout 900 \
  --memory-size 512 \
  --region us-east-1
```

### Environment Variables

```bash
aws lambda update-function-configuration \
  --function-name textract-processor \
  --environment Variables='{
    "BUCKET_NAME":"testbucket-sudhir-bsi1",
    "TEXTRACT_ROLE_ARN":"arn:aws:iam::912532823432:role/accesstextract-role",
    "SNS_TOPIC_ARN":"arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo",
    "OUTPUT_DIRECTORY":"/tmp/CachedFiles_OutputFiles"
  }'
```

### Lambda Layers (Optional)

For large dependencies:
```bash
# Create layer for AWS SDK extensions
aws lambda publish-layer-version \
  --layer-name aws-sdk-extensions \
  --zip-file fileb://layer.zip \
  --compatible-runtimes dotnet8
```

---

## Security Best Practices

### 1. Least Privilege Access

? Grant only necessary permissions  
? Use specific resource ARNs (not `*`)  
? Separate roles for each service

### 2. Encryption

? Enable S3 bucket encryption (AES-256 or KMS)  
? Use HTTPS for all API calls  
? Encrypt sensitive data in transit and at rest

### 3. Logging and Monitoring

? Enable CloudWatch Logs for Lambda  
? Enable CloudTrail for API auditing  
? Set up CloudWatch Alarms for errors

### 4. Access Control

? Use IAM roles (not access keys) for services  
? Rotate credentials regularly  
? Enable MFA for console access

### 5. Cost Controls

? Set up billing alerts  
? Use S3 lifecycle policies  
? Monitor Textract and Bedrock usage

---

## Verification

### Test S3 Access

```bash
# Upload test file
aws s3 cp test.tif s3://testbucket-sudhir-bsi1/uploads/2025-01-20/test/test.tif

# Verify upload
aws s3 ls s3://testbucket-sudhir-bsi1/uploads/2025-01-20/test/
```

### Test Textract

```bash
# Start job
aws textract start-document-analysis \
  --document-location '{
    "S3Object": {
      "Bucket": "testbucket-sudhir-bsi1",
      "Name": "uploads/2025-01-20/test/test.tif"
    }
  }' \
  --feature-types TABLES FORMS LAYOUT \
  --notification-channel '{
    "RoleArn": "arn:aws:iam::912532823432:role/accesstextract-role",
    "SNSTopicArn": "arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo"
  }'

# Get results (use JobId from above)
aws textract get-document-analysis --job-id <job-id>
```

### Test Bedrock

```bash
# Test Qwen model
aws bedrock-runtime invoke-model \
  --model-id qwen.qwen2.5-7b-instruct \
--body '{"messages":[{"role":"user","content":"Hello"}],"max_tokens":100}' \
  --region us-west-2 \
  output.json

cat output.json
```

### Test Lambda

```bash
# Invoke function
aws lambda invoke \
  --function-name textract-processor \
  --payload '{}' \
  response.json

# View response
cat response.json

# Check logs
aws logs tail /aws/lambda/textract-processor --follow
```

---

## Configuration Checklist

- [ ] S3 bucket created with correct name
- [ ] Folder structure: `uploads/{date}/{filename}/`
- [ ] IAM role for Textract with S3 and SNS permissions
- [ ] IAM role for Lambda with S3, Textract, Bedrock permissions
- [ ] SNS FIFO topic created
- [ ] SNS topic policy allows Textract to publish
- [ ] Bedrock models access requested and approved
- [ ] Lambda function created with correct runtime
- [ ] Lambda execution role attached
- [ ] Lambda timeout set to 15 minutes
- [ ] Lambda memory set to 512 MB minimum
- [ ] Environment variables configured
- [ ] CloudWatch Logs enabled
- [ ] Billing alerts configured
- [ ] All services tested individually

---

## Quick Reference

### ARNs
```
S3 Bucket: s3://testbucket-sudhir-bsi1
Textract Role: arn:aws:iam::912532823432:role/accesstextract-role
SNS Topic: arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo
Lambda Function: arn:aws:lambda:us-east-1:912532823432:function:textract-processor
```

### Regions
```
S3/Textract/SNS: us-east-1
Bedrock Nova: us-west-1
Bedrock Qwen: us-west-2
Bedrock Claude: us-east-1
```

### Costs (Approximate)
```
S3: $0.023/GB/month
Textract: $0.045/page (3 features)
Bedrock Qwen: $0.0008 input + $0.0024 output per 1K tokens
Lambda: $0.20/million requests
SNS: $0.50/million requests
```

---

**Need troubleshooting help?** See [PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md) for common issues.
