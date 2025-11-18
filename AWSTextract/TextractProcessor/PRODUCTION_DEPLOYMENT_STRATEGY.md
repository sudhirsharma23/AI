# Production Deployment Strategy for TextractProcessor

**Release Management and Deployment Best Practices**

> **Strategy Type:** Blue/Green with Canary Deployments  
> **Rollback Time:** < 2 minutes  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Deployment Strategy Overview](#deployment-strategy-overview)
2. [Release Versioning](#release-versioning)
3. [Deployment Methods](#deployment-methods)
4. [Blue/Green Deployment](#bluegreen-deployment)
5. [Canary Deployment](#canary-deployment)
6. [Rollback Procedures](#rollback-procedures)
7. [Testing Strategy](#testing-strategy)
8. [Release Checklist](#release-checklist)
9. [Incident Response](#incident-response)
10. [Post-Deployment Validation](#post-deployment-validation)

---

## Deployment Strategy Overview

### Deployment Philosophy

```
┌────────────────────────────────────────────────────────────────┐
│        DEPLOYMENT STRATEGY PHILOSOPHY        │
├────────────────────────────────────────────────────────────────┤
│     │
│  1. SAFETY FIRST         │
│     • Zero-downtime deployments      │
│   • Instant rollback capability     │
│     • Automated health checks       │
│             │
│  2. PROGRESSIVE DELIVERY        │
│     • Deploy to dev/staging first      │
│     • Canary deployments (10% → 50% → 100%)   │
│     • Monitor metrics at each stage      │
│            │
│  3. AUTOMATION           │
│     • CI/CD pipeline handles deployment    │
│     • Automated testing at every stage   │
│   • Manual approval only for production  │
│                │
│4. OBSERVABILITY    │
│     • Real-time monitoring during deployment   │
│     • Automated alerting on anomalies     │
│     • Detailed deployment logs        │
│     │
│  5. RELIABILITY  │
│     • Version control for all releases     │
│     • Immutable deployments       │
│     • Tested rollback procedures      │
└────────────────────────────────────────────────────────────────┘
```

### Deployment Environments

| Environment | Purpose | Deployment Trigger | Approval | Monitoring Level |
|-------------|---------|-------------------|----------|------------------|
| **Development** | Feature testing | Push to `develop` | None | Basic |
| **Staging** | Pre-production validation | Push to `release/*` | Optional | Standard |
| **Production** | Live workload | Push to `main` | Required (2 approvers) | Comprehensive |

---

## Release Versioning

### Semantic Versioning

**Format:** `MAJOR.MINOR.PATCH`

- **MAJOR:** Breaking changes (e.g., API changes, schema changes)
- **MINOR:** New features (backward compatible)
- **PATCH:** Bug fixes (backward compatible)

**Examples:**
- `1.0.0` - Initial production release
- `1.1.0` - Added new Bedrock model support
- `1.1.1` - Fixed Textract pagination bug
- `2.0.0` - Changed output JSON schema (breaking)

### Version Tagging Strategy

```bash
# Development releases (automatic)
dev-1.2.3-20250120-103045
dev-1.2.3-abc1234

# Staging releases
staging-1.2.3
staging-1.2.3-rc1

# Production releases
v1.2.3
v1.2.3-hotfix
```

### Release Naming Convention

```
v{MAJOR}.{MINOR}.{PATCH}-{ENVIRONMENT}-{TIMESTAMP}

Examples:
- v1.0.0-prod-20250120
- v1.1.0-rc1-20250121
- v1.0.1-hotfix-20250122
```

---

## Deployment Methods

### Method 1: Direct Lambda Update (Fastest)

**Use Case:** Development, small changes

```bash
# Build and package
dotnet lambda package --configuration Release --output-package function.zip

# Deploy directly
aws lambda update-function-code \
  --function-name textract-processor-prod \
  --zip-file fileb://function.zip

# Publish version
aws lambda publish-version \
  --function-name textract-processor-prod \
  --description "v1.2.3 - Bug fixes"
```

**Pros:**
- ✅ Fast (< 30 seconds)
- ✅ Simple
- ✅ Good for quick fixes

**Cons:**
- ❌ No traffic shifting
- ❌ Risk of immediate impact
- ❌ Manual rollback required

---

### Method 2: Blue/Green with Aliases (Recommended)

**Use Case:** Production deployments

```bash
# Step 1: Deploy new version (Green)
aws lambda publish-version \
  --function-name textract-processor-prod \
  --description "v1.2.3"
# Returns: Version 10

# Step 2: Test green version
aws lambda invoke \
  --function-name textract-processor-prod:10 \
  --payload '{"test": true}' \
  response.json

# Step 3: Switch alias from Blue (v9) to Green (v10)
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 10

# Step 4: Rollback if needed (instant)
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 9
```

**Pros:**
- ✅ Zero-downtime
- ✅ Instant rollback
- ✅ Can test green before switching

**Cons:**
- ❌ Requires alias management
- ❌ More complex setup

---

### Method 3: Canary Deployment (Most Safe)

**Use Case:** High-risk changes, major releases

```bash
# Step 1: Deploy new version
aws lambda publish-version \
  --function-name textract-processor-prod \
  --description "v2.0.0 - Major update"
# Returns: Version 10

# Step 2: Route 10% traffic to new version
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 10 \
  --routing-config AdditionalVersionWeights='{"9":0.9}'

# Step 3: Monitor for 10 minutes
# Check error rates, latency, success rates

# Step 4: Increase to 50% if healthy
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 10 \
  --routing-config AdditionalVersionWeights='{"9":0.5}'

# Step 5: Full cutover (100%)
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version 10
```

**Pros:**
- ✅ Lowest risk
- ✅ Progressive rollout
- ✅ Early detection of issues

**Cons:**
- ❌ Slower deployment
- ❌ More monitoring required
- ❌ Complex traffic management

---

## Blue/Green Deployment

### Architecture

```
┌────────────────────────────────────────────────────────────────┐
│       BLUE/GREEN DEPLOYMENT FLOW         │
├────────────────────────────────────────────────────────────────┤
│           │
│  BEFORE DEPLOYMENT            │
│  ┌──────────────────────────────────────────┐   │
│  │ Production Alias → Version 9 (BLUE)      │      │
│  │ All traffic → Blue          │      │
│  └──────────────────────────────────────────┘      │
│     │
│  DEPLOYMENT STEPS     │
│  ┌──────────────────────────────────────────┐      │
│  │ Step 1: Deploy Version 10 (GREEN) │      │
│  │  New code deployed, not live      │      │
│  └──────────────────────────────────────────┘      │
│   │
│  ┌──────────────────────────────────────────┐      │
│  │ Step 2: Test GREEN directly              │      │
│  │     textract-processor-prod:10       │      │
│  │         Run smoke tests          │      │
│  └──────────────────────────────────────────┘      │
│    │
│┌──────────────────────────────────────────┐      │
│  │ Step 3: Switch Alias│      │
│  │         Production Alias → Version 10    │      │
│  │         All traffic now on GREEN         │      │
│  └──────────────────────────────────────────┘      │
│    │
│  AFTER DEPLOYMENT      │
│  ┌──────────────────────────────────────────┐      │
│  │ Production Alias → Version 10 (GREEN)    │      │
│  │ Version 9 (BLUE) still available         │      │
│  │ Can rollback instantly if needed         │      │
│  └──────────────────────────────────────────┘      │
└────────────────────────────────────────────────────────────────┘
```

### Implementation Script

**File:** `scripts/blue-green-deploy.sh`

```bash
#!/bin/bash
set -e

# Configuration
FUNCTION_NAME="textract-processor-prod"
ALIAS_NAME="production"
REGION="us-east-1"
VERSION_DESCRIPTION="$1"

if [ -z "$VERSION_DESCRIPTION" ]; then
  echo "Usage: ./blue-green-deploy.sh 'Version description'"
  exit 1
fi

echo "========================================="
echo "Blue/Green Deployment"
echo "Function: $FUNCTION_NAME"
echo "========================================="

# Get current version (BLUE)
CURRENT_VERSION=$(aws lambda get-alias \
  --function-name $FUNCTION_NAME \
  --name $ALIAS_NAME \
  --query 'FunctionVersion' \
  --output text \
  --region $REGION)

echo "Current version (BLUE): $CURRENT_VERSION"

# Build and package
echo "Building Lambda package..."
cd TextractProcessor/src/TextractProcessor
dotnet lambda package --configuration Release --output-package function.zip

# Update function code
echo "Updating function code..."
aws lambda update-function-code \
  --function-name $FUNCTION_NAME \
  --zip-file fileb://function.zip \
  --region $REGION

# Wait for update to complete
echo "Waiting for update to complete..."
aws lambda wait function-updated \
  --function-name $FUNCTION_NAME \
  --region $REGION

# Publish new version (GREEN)
echo "Publishing new version (GREEN)..."
NEW_VERSION=$(aws lambda publish-version \
  --function-name $FUNCTION_NAME \
  --description "$VERSION_DESCRIPTION" \
  --query 'Version' \
  --output text \
  --region $REGION)

echo "New version (GREEN): $NEW_VERSION"

# Test GREEN version
echo "Testing GREEN version..."
TEST_RESPONSE=$(aws lambda invoke \
  --function-name $FUNCTION_NAME:$NEW_VERSION \
  --payload '{"test": true}' \
  --region $REGION \
  test-response.json)

if grep -q "FunctionError" test-response.json; then
  echo "❌ GREEN version test failed!"
  cat test-response.json
  exit 1
fi

echo "✅ GREEN version test passed"

# Switch alias to GREEN
echo "Switching alias to GREEN version..."
aws lambda update-alias \
  --function-name $FUNCTION_NAME \
  --name $ALIAS_NAME \
  --function-version $NEW_VERSION \
  --region $REGION

echo "========================================="
echo "✅ Deployment Complete!"
echo "Previous version (BLUE): $CURRENT_VERSION"
echo "Current version (GREEN): $NEW_VERSION"
echo ""
echo "Rollback command:"
echo "aws lambda update-alias \\"
echo "  --function-name $FUNCTION_NAME \\"
echo "  --name $ALIAS_NAME \\"
echo "  --function-version $CURRENT_VERSION \\"
echo "  --region $REGION"
echo "========================================="
```

---

## Canary Deployment

### Traffic Shifting Strategy

```
┌────────────────────────────────────────────────────────────────┐
│        CANARY DEPLOYMENT STAGES          │
├────────────────────────────────────────────────────────────────┤
│     │
│  Stage 1: 10% Canary (Monitor 10 minutes)      │
│  ┌──────────────────────────────────────────┐      │
│  │ Version 9 (OLD) ████████████████████ 90% │      │
│  │ Version 10 (NEW) ██ 10%  │      │
│  └──────────────────────────────────────────┘      │
│           │
│  If healthy, proceed to Stage 2...         │
│   │
│  Stage 2: 50% Canary (Monitor 10 minutes)      │
│  ┌──────────────────────────────────────────┐      │
│  │ Version 9 (OLD) ██████████ 50%  │      │
│  │ Version 10 (NEW) ██████████ 50%        │      │
│  └──────────────────────────────────────────┘      │
│   │
│  If healthy, proceed to Stage 3...      │
│      │
│  Stage 3: 100% Cutover     │
│  ┌──────────────────────────────────────────┐      │
│  │ Version 10 (NEW) ████████████████████ 100%│     │
│  └──────────────────────────────────────────┘      │
│            │
│  Health Check Criteria at Each Stage:       │
│  • Error rate < 1%             │
│  • Latency < 10s average    │
│  • No critical logs     │
│  • Success rate > 99%           │
└────────────────────────────────────────────────────────────────┘
```

### Implementation Script

**File:** `scripts/canary-deploy.sh`

```bash
#!/bin/bash
set -e

# Configuration
FUNCTION_NAME="textract-processor-prod"
ALIAS_NAME="production"
REGION="us-east-1"
CANARY_STAGES=(10 50 100)
WAIT_TIME=600  # 10 minutes per stage

echo "========================================="
echo "Canary Deployment"
echo "Function: $FUNCTION_NAME"
echo "Stages: ${CANARY_STAGES[@]}%"
echo "========================================="

# Get current version
OLD_VERSION=$(aws lambda get-alias \
  --function-name $FUNCTION_NAME \
  --name $ALIAS_NAME \
  --query 'FunctionVersion' \
  --output text \
  --region $REGION)

echo "Current version: $OLD_VERSION"

# Deploy new version
cd TextractProcessor/src/TextractProcessor
dotnet lambda package --configuration Release --output-package function.zip

aws lambda update-function-code \
  --function-name $FUNCTION_NAME \
  --zip-file fileb://function.zip \
  --region $REGION

aws lambda wait function-updated \
  --function-name $FUNCTION_NAME \
  --region $REGION

NEW_VERSION=$(aws lambda publish-version \
  --function-name $FUNCTION_NAME \
  --description "Canary deployment $(date)" \
  --query 'Version' \
  --output text \
  --region $REGION)

echo "New version: $NEW_VERSION"

# Function to check health
check_health() {
  local version=$1
  local percentage=$2
  
  echo "Checking health for $percentage% canary..."
  
  # Get error rate
  ERRORS=$(aws cloudwatch get-metric-statistics \
    --namespace AWS/Lambda \
    --metric-name Errors \
    --dimensions Name=FunctionName,Value=$FUNCTION_NAME \
    --start-time $(date -u -d '5 minutes ago' +%Y-%m-%dT%H:%M:%S) \
    --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
    --period 300 \
    --statistics Sum \
    --query 'Datapoints[0].Sum' \
--output text \
    --region $REGION)
  
  # Get invocation count
  INVOCATIONS=$(aws cloudwatch get-metric-statistics \
    --namespace AWS/Lambda \
    --metric-name Invocations \
    --dimensions Name=FunctionName,Value=$FUNCTION_NAME \
    --start-time $(date -u -d '5 minutes ago' +%Y-%m-%dT%H:%M:%S) \
    --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
    --period 300 \
    --statistics Sum \
    --query 'Datapoints[0].Sum' \
    --output text \
    --region $REGION)
  
  # Calculate error rate
  if [ "$INVOCATIONS" != "None" ] && [ "$INVOCATIONS" -gt "0" ]; then
    ERROR_RATE=$(echo "scale=2; ($ERRORS / $INVOCATIONS) * 100" | bc)
    echo "Error rate: $ERROR_RATE% ($ERRORS errors / $INVOCATIONS invocations)"
    
    # Check threshold (1%)
    if (( $(echo "$ERROR_RATE > 1" | bc -l) )); then
      echo "❌ Error rate too high!"
      return 1
    fi
  fi
  
  echo "✅ Health check passed"
  return 0
}

# Execute canary stages
for stage in "${CANARY_STAGES[@]}"; do
  if [ $stage -eq 100 ]; then
    echo "========================================="
 echo "Stage: Full cutover ($stage%)"
    echo "========================================="
    
    aws lambda update-alias \
      --function-name $FUNCTION_NAME \
      --name $ALIAS_NAME \
      --function-version $NEW_VERSION \
   --region $REGION
  else
    echo "========================================="
    echo "Stage: $stage% canary"
 echo "========================================="
    
    # Calculate old version weight
    OLD_WEIGHT=$(echo "scale=2; (100 - $stage) / 100" | bc)
    
    aws lambda update-alias \
      --function-name $FUNCTION_NAME \
      --name $ALIAS_NAME \
      --function-version $NEW_VERSION \
      --routing-config "AdditionalVersionWeights={\"$OLD_VERSION\":$OLD_WEIGHT}" \
      --region $REGION
    
    echo "Monitoring for $WAIT_TIME seconds..."
    sleep $WAIT_TIME
    
    # Check health
    if ! check_health $NEW_VERSION $stage; then
      echo "❌ Health check failed! Rolling back..."
      
      aws lambda update-alias \
        --function-name $FUNCTION_NAME \
 --name $ALIAS_NAME \
        --function-version $OLD_VERSION \
        --region $REGION
      
      echo "Rollback complete"
      exit 1
    fi
  fi
done

echo "========================================="
echo "✅ Canary Deployment Complete!"
echo "Old version: $OLD_VERSION"
echo "New version: $NEW_VERSION"
echo "========================================="
```

---

## Rollback Procedures

### Instant Rollback (< 2 minutes)

**Scenario:** Production issue detected immediately after deployment

```bash
# Method 1: AWS CLI
aws lambda update-alias \
  --function-name textract-processor-prod \
  --name production \
  --function-version <previous-version> \
  --region us-east-1

# Method 2: Using script
./scripts/rollback.sh <previous-version>
```

### Rollback Script

**File:** `scripts/rollback.sh`

```bash
#!/bin/bash
set -e

FUNCTION_NAME="textract-processor-prod"
ALIAS_NAME="production"
REGION="us-east-1"
TARGET_VERSION="$1"

if [ -z "$TARGET_VERSION" ]; then
  echo "Usage: ./rollback.sh <target-version>"
  echo ""
  echo "Available versions:"
  aws lambda list-versions-by-function \
    --function-name $FUNCTION_NAME \
    --query 'Versions[?Version!=`$LATEST`].[Version,Description]' \
    --output table \
    --region $REGION
  exit 1
fi

# Get current version
CURRENT_VERSION=$(aws lambda get-alias \
  --function-name $FUNCTION_NAME \
  --name $ALIAS_NAME \
  --query 'FunctionVersion' \
  --output text \
  --region $REGION)

echo "========================================="
echo "Rollback Operation"
echo "Current version: $CURRENT_VERSION"
echo "Target version: $TARGET_VERSION"
echo "========================================="

# Confirm rollback
read -p "Proceed with rollback? (yes/no): " confirm
if [ "$confirm" != "yes" ]; then
  echo "Rollback cancelled"
  exit 0
fi

# Execute rollback
echo "Rolling back..."
aws lambda update-alias \
  --function-name $FUNCTION_NAME \
  --name $ALIAS_NAME \
  --function-version $TARGET_VERSION \
  --region $REGION

echo "========================================="
echo "✅ Rollback Complete!"
echo "Reverted from version $CURRENT_VERSION to $TARGET_VERSION"
echo "========================================="

# Notify team
if [ -n "$SLACK_WEBHOOK" ]; then
  curl -X POST $SLACK_WEBHOOK \
    -H 'Content-Type: application/json' \
    -d "{\"text\":\"⚠️ Production rollback: $CURRENT_VERSION → $TARGET_VERSION\"}"
fi
```

### Rollback Decision Tree

```
┌────────────────────────────────────────────────────────────────┐
│       ROLLBACK DECISION TREE       │
├────────────────────────────────────────────────────────────────┤
│       │
│  Is there a production issue?          │
│    │               │
│    ├─ NO → Continue monitoring         │
│    │        │
│    └─ YES → Assess severity          │
│           │      │
│     ├─ MINOR (< 1% error rate)       │
│           │  → Monitor closely, prepare rollback    │
│           │          │
│├─ MODERATE (1-5% error rate)      │
│    │  → Rollback immediately        │
│ │        │
│   └─ CRITICAL (> 5% error rate or data loss)   │
│    → IMMEDIATE ROLLBACK + INCIDENT RESPONSE│
│                │
│  Rollback Execution Time Target: < 2 minutes     │
└────────────────────────────────────────────────────────────────┘
```

---

## Testing Strategy

### Pre-Deployment Testing

**1. Unit Tests (Automated)**
```bash
dotnet test TextractProcessor.Tests/TextractProcessor.Tests.csproj --verbosity normal
```

**2. Integration Tests (Automated)**
```bash
# Test against dev environment
./scripts/integration-test.sh dev
```

**3. Smoke Tests (Automated)**
```bash
# Upload test file and verify output
aws s3 cp tests/fixtures/sample.tif s3://testbucket/incoming/
# Wait for processing
sleep 60
# Check output
aws s3 ls s3://testbucket/processed/ | grep sample || exit 1
```

### Post-Deployment Validation

**1. Synthetic Monitoring**
```bash
# Invoke Lambda with test payload
aws lambda invoke \
  --function-name textract-processor-prod \
  --payload '{"test": true}' \
  response.json

# Verify response
cat response.json | jq '.statusCode' | grep 200 || exit 1
```

**2. Real Traffic Monitoring**
```bash
# Monitor CloudWatch metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Invocations \
  --dimensions Name=FunctionName,Value=textract-processor-prod \
  --start-time $(date -u -d '5 minutes ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300 \
  --statistics Sum
```

---

## Release Checklist

### Pre-Release (1 day before)

- [ ] Code freeze on `main` branch
- [ ] All tests passing in CI/CD
- [ ] Dev environment validated
- [ ] Staging environment validated
- [ ] Release notes prepared
- [ ] Rollback plan documented
- [ ] On-call team notified
- [ ] Stakeholders informed

### Release Day (Production Deployment)

**Morning (09:00 AM)**
- [ ] Team standup: Review deployment plan
- [ ] Verify all systems healthy
- [ ] Create release tag (`v1.2.3`)
- [ ] Merge to `main` branch

**Deployment Window (10:00 AM - 11:00 AM)**
- [ ] Start deployment via GitHub Actions
- [ ] Monitor CI/CD pipeline
- [ ] Approve production deployment
- [ ] Monitor canary stages (if applicable)
- [ ] Verify smoke tests pass
- [ ] Monitor error rates for 30 minutes

**Post-Deployment (11:00 AM - 12:00 PM)**
- [ ] Validate all metrics normal
- [ ] Check CloudWatch logs for errors
- [ ] Run integration tests
- [ ] Update documentation
- [ ] Notify stakeholders of success
- [ ] Close release ticket

### Rollback Checklist (If Needed)

- [ ] Identify issue severity
- [ ] Execute rollback script
- [ ] Verify rollback successful
- [ ] Monitor metrics post-rollback
- [ ] Create incident ticket
- [ ] Notify stakeholders
- [ ] Schedule post-mortem

---

## Incident Response

### Severity Levels

| Severity | Definition | Response Time | Rollback Required? |
|----------|------------|---------------|-------------------|
| **P0 (Critical)** | Data loss, 100% failure | Immediate (< 5 min) | YES |
| **P1 (High)** | > 10% error rate | < 15 minutes | YES |
| **P2 (Medium)** | 1-10% error rate | < 30 minutes | Evaluate |
| **P3 (Low)** | < 1% error rate, degraded performance | < 2 hours | NO |

### Incident Response Procedure

```
┌────────────────────────────────────────────────────────────────┐
│       INCIDENT RESPONSE FLOW          │
├────────────────────────────────────────────────────────────────┤
│     │
│  1. DETECT   │
│     • CloudWatch alarm triggered        │
│   • Manual report from user         │
│     • Monitoring dashboard shows anomaly      │
│         │
│  2. ASSESS         │
│     • Determine severity (P0-P3)      │
│     • Check recent deployments    │
│     • Review CloudWatch logs          │
│  │
│  3. RESPOND    │
│     • If P0/P1: ROLLBACK IMMEDIATELY       │
│     • If P2/P3: Investigate and fix       │
│     • Notify stakeholders          │
│            │
│  4. RESOLVE    │
│  • Execute rollback if needed        │
│     • Monitor metrics post-rollback       │
│     • Verify issue resolved          │
│              │
│  5. POST-MORTEM            │
│     • Document incident           │
│  • Root cause analysis          │
│   • Action items for prevention        │
└────────────────────────────────────────────────────────────────┘
```

---

## Post-Deployment Validation

### Validation Checklist (First 24 Hours)

**Hour 1:**
- [ ] No errors in CloudWatch Logs
- [ ] Lambda invocation count matches expected
- [ ] Average duration < 10 seconds
- [ ] S3 output files being created

**Hour 4:**
- [ ] Error rate < 0.1%
- [ ] No customer complaints
- [ ] DynamoDB writes successful
- [ ] Bedrock API calls succeeding

**Hour 24:**
- [ ] Cost within expected range
- [ ] No performance degradation
- [ ] All processed files have outputs
- [ ] Release notes published

### Monitoring Dashboard

**Key Metrics to Watch:**

| Metric | Threshold | Action if Exceeded |
|--------|-----------|-------------------|
| Error Rate | < 1% | Investigate logs |
| Duration | < 10s avg | Check performance |
| Throttles | 0 | Increase concurrency |
| Dead Letter Queue | 0 messages | Investigate failures |
| Cost per Invocation | < $0.15 | Review usage patterns |

---

## Summary

### Recommended Deployment Strategy

**For Regular Releases:**
1. Deploy to Dev (automatic)
2. Manual validation in Dev
3. Deploy to Staging (automatic)
4. Manual validation in Staging
5. Deploy to Production using **Blue/Green** (manual approval)
6. Monitor for 30 minutes
7. Complete release

**For Major Releases / High-Risk Changes:**
1. Deploy to Dev (automatic)
2. Deploy to Staging (automatic)
3. Deploy to Production using **Canary** (manual approval)
   - 10% for 10 minutes
   - 50% for 10 minutes
   - 100% cutover
4. Monitor for 2 hours
5. Complete release

**For Hotfixes:**
1. Create hotfix branch from `main`
2. Fix issue + tests
3. Deploy to Dev for quick validation
4. Deploy to Production using **Blue/Green** (expedited approval)
5. Monitor closely for 1 hour

---

**Deployment Strategy Version:** 1.0  
**Last Updated:** January 2025  
**Next Review:** April 2025  
**Owner:** DevOps Team
