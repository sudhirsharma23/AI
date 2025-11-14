# Version 2 Dynamic Extraction - Implementation Complete

## ? Implementation Summary

**Date**: 2025-01-28  
**Feature**: Dual-version prompt system (V1 Schema-based + V2 Dynamic)  
**Status**: ? Build Successful  
**Impact**: Can now compare schema-driven vs dynamic extraction

---

## ?? What Was Implemented

### 1. **New Prompt Template - V2 (Dynamic)**
**File**: `..\AzureTextReader\src\Prompts\SystemPrompts\deed_extraction_v2.txt`

**Key Differences from V1:**
- ? **NO predefined schema** - AI discovers structure dynamically
- ? **NO examples** - Pure analysis of OCR content
- ? **NO rules** - AI infers best practices
- ? **FULL document analysis** - Extracts everything found
- ? **Comprehensive JSON output** - All relevant information

**Extraction Focus:**
- Buyer information (names, addresses, percentages, all details)
- Seller information (old owners with complete data)
- Property details (address, legal description, parcel info)
- Land records information
- Transaction details (prices, dates, fees)
- PCOR information (checkboxes, tax info, exemptions)

### 2. **Modified Program.cs Architecture**

**Before** (Single version):
```
ProcessWithChatCompletion()
??? Uses schema from invoice_schema - Copy.json
```

**After** (Dual version):
```
ProcessWithChatCompletion()
??? ProcessVersion1() [Schema-based]
?   ??? Loads invoice_schema - Copy.json
?   ??? Uses deed_extraction_v1.txt
?   ??? Applies rules and examples
?   ??? Saves as: final_output_{timestamp}_v1_schema.json
?
??? ProcessVersion2() [Dynamic]
    ??? NO schema loading
  ??? Uses deed_extraction_v2.txt
    ??? NO rules or examples
    ??? Saves as: final_output_{timestamp}_v2_dynamic.json
```

### 3. **New Analysis Features**

**For V1** (Schema-based):
- Schema extensions report: `schema_extensions_{timestamp}.md`
- Shows fields extracted beyond schema

**For V2** (Dynamic):
- Extraction summary: `v2_extraction_summary_{timestamp}.md`
- Statistics on fields extracted
- Key information summary
- Comparison notes

---

## ?? Output Files Generated

After running the application, you'll see these files in `OutputFiles/`:

| File | Description | Version |
|------|-------------|---------|
| `combined_ocr_results_{timestamp}.md` | Raw OCR text from both documents | Both |
| `final_output_{timestamp}_v1_schema.json` | Extraction using schema | V1 |
| `schema_extensions_{timestamp}.md` | Fields beyond schema | V1 |
| `final_output_{timestamp}_v2_dynamic.json` | Dynamic extraction | V2 |
| `v2_extraction_summary_{timestamp}.md` | V2 statistics & summary | V2 |

---

## ?? How V2 Extraction Works

### Dynamic Analysis Process

1. **Document Identification**
   - Scans for Grant Deed keywords
   - Identifies PCOR document
   - Notes which document each section comes from

2. **Buyer Extraction** (NEW OWNERS/GRANTEES)
   ```json
   {
     "buyerInformation": {
       "totalBuyers": 2,
       "buyers": [
     {
    "sequenceNumber": 1,
  "isPrimary": true,
         "fullName": "John Smith",
      "firstName": "John",
      "lastName": "Smith",
   "ownershipPercentage": 50,
           "vesting": "as joint tenants",
           "address": {
        "street": "123 Main St",
      "city": "Los Angeles",
             "state": "CA",
     "zipCode": "90001"
        }
         }
         // ... more buyers
       ]
     }
   }
   ```

3. **Seller Extraction** (OLD OWNERS/GRANTORS)
   - Same structure as buyers
   - Includes date acquired
   - Includes previous vesting type

4. **Property Information**
   ```json
   {
     "propertyInformation": {
       "address": { /* complete address */ },
       "legalDescription": {
  "lotNumber": "15",
      "blockNumber": "A",
         "tract": "12345",
         "subdivision": "Sunset Hills"
       },
       "parcelInformation": {
    "assessorParcelNumber": "1234-567-890",
    "parcelSize": "0.25 acres"
       }
     }
   }
   ```

5. **PCOR Information**
   - All checkboxes (yes/no)
   - Tax exemptions (Prop 13, 19, 58, etc.)
   - Property tax information
   - Preparer information

6. **Transaction Details**
   - Sale price
   - Recording date, execution date
   - Fees (recording, transfer tax, escrow)
   - Loan information

---

## ?? V1 vs V2 Comparison

| Aspect | V1 (Schema-based) | V2 (Dynamic) |
|--------|-------------------|--------------|
| **Schema** | ? Uses `invoice_schema - Copy.json` | ? No schema |
| **Structure** | ? Predefined fields | ? Discovers fields |
| **Examples** | ? Uses 3 examples | ? No examples |
| **Rules** | ? 3 rule files | ? No rules |
| **Consistency** | ? High (schema-driven) | ?? Variable (AI-driven) |
| **Completeness** | ?? Limited to schema | ? Extracts everything |
| **New Fields** | ?? Must update schema | ? Automatic |
| **Best For** | Production, consistency | Analysis, discovery |

---

## ?? How to Run

### Run Both Versions
```powershell
cd E:\Sudhir\GitRepo\AzureTextReader\src
dotnet run
```

**Console Output:**
```
=== Processing with Azure OpenAI ChatCompletion ===

--- Version 1: Schema-Based Extraction ---
Building V1 prompt from templates (with schema)...
V1 Prompt built successfully (System: ~5000 chars, User: ~2000 chars)
Cache miss - Calling ChatClient.CompleteChat for V1...
Saved final cleaned JSON to: OutputFiles\final_output_20250128120000_v1_schema.json
Found 15 extended fields not in base schema:
  + saleData.custom_field1 : string
  ...
Saved schema extensions report to: OutputFiles\schema_extensions_20250128120000.md

--- Version 2: Dynamic Extraction (No Schema) ---
Building V2 prompt from templates (dynamic, no schema)...
V2 Prompt built successfully (System: ~8000 chars, User: ~500 chars)
Cache miss - Calling ChatClient.CompleteChat for V2...
Saved final cleaned JSON to: OutputFiles\final_output_20250128120000_v2_dynamic.json
=== Analyzing V2 Dynamic Extraction ===
- Total fields extracted: 150
Saved V2 extraction summary to: OutputFiles\v2_extraction_summary_20250128120000.md
```

---

## ?? Comparing Results

### Step 1: Review V1 Output
```powershell
code OutputFiles\final_output_*_v1_schema.json
```

**Check:**
- ? All schema fields populated?
- ? Buyer/seller percentages correct?
- ? Dates in YYYY-MM-DD format?
- ?? Any missing information?

### Step 2: Review V2 Output
```powershell
code OutputFiles\final_output_*_v2_dynamic.json
```

**Check:**
- ? More fields than V1?
- ? Additional buyer/seller details?
- ? PCOR checkboxes captured?
- ? Property tax information?

### Step 3: Compare Side-by-Side
```powershell
# Open both files
code OutputFiles\final_output_*_v1_schema.json
code OutputFiles\final_output_*_v2_dynamic.json
```

**Analysis Questions:**
1. Did V2 find fields that V1 missed?
2. Is V2's structure intuitive and well-organized?
3. Did V2 capture all PCOR checkboxes?
4. Are buyer/seller details more complete in V2?
5. Should any V2 fields be added to the V1 schema?

### Step 4: Review Reports
```powershell
# V1 schema extensions
code OutputFiles\schema_extensions_*.md

# V2 extraction summary
code OutputFiles\v2_extraction_summary_*.md
```

---

## ?? Use Cases

### Use Case 1: Schema Validation
**Goal**: Verify if current schema captures all relevant information

**Process:**
1. Run both versions on sample documents
2. Compare `_v1_schema.json` with `_v2_dynamic.json`
3. Check `schema_extensions_*.md` for missing fields
4. Update schema if needed

**Example Finding:**
```
V2 found: "propertyTaxYear": "2024-2025"
V1 schema: No field for property tax year
Action: Add "property_tax_year" to schema
```

### Use Case 2: Discover New Fields
**Goal**: Find what other information is in the documents

**Process:**
1. Review `_v2_dynamic.json`
2. Look for unexpected sections
3. Identify business-critical data points
4. Add to V1 schema for consistency

**Example:**
```json
// V2 discovered these fields:
"transactionDetails": {
  "escrowInformation": {
    "escrowNumber": "ESC-2025-001",
    "escrowCompany": "First American Escrow",
    "titleCompany": "Chicago Title"
  }
}
// Consider adding to V1 schema
```

### Use Case 3: PCOR Analysis
**Goal**: Extract all PCOR checkboxes dynamically

**Process:**
1. Check `_v2_dynamic.json` ? `pcorInformation`
2. Review all `transferCircumstances`
3. Verify against physical PCOR document
4. Identify any missed checkboxes

**V2 Advantage:**
- Extracts ALL checkboxes found
- Not limited to predefined list
- Captures custom fields

### Use Case 4: Training Data Generation
**Goal**: Create diverse examples for model fine-tuning

**Process:**
1. Run V2 on multiple documents
2. Collect varied extraction patterns
3. Use as training data
4. Improve future extraction accuracy

---

## ?? Configuration Options

### Enable/Disable Versions

**Option 1: Run only V1 (Schema-based)**
Edit `ProcessWithChatCompletion()`:
```csharp
// Comment out V2
// await ProcessVersion2(memoryCache, chatClient, combinedMarkdown, timestamp);
```

**Option 2: Run only V2 (Dynamic)**
Edit `ProcessWithChatCompletion()`:
```csharp
// Comment out V1
// await ProcessVersion1(memoryCache, chatClient, combinedMarkdown, timestamp);
```

**Option 3: Run both (Current Default)**
```csharp
await ProcessVersion1(memoryCache, chatClient, combinedMarkdown, timestamp);
await ProcessVersion2(memoryCache, chatClient, combinedMarkdown, timestamp);
```

### Modify V2 Prompt

**File**: `deed_extraction_v2.txt`

**Example Modifications:**
```text
// Focus more on specific sections
Extract ALL property tax information including:
- Tax year
- Current amount
- Supplemental tax
- Exemptions claimed
```

---

## ?? Troubleshooting

### Issue 1: V2 Output Different Structure Each Time
**Cause**: Dynamic extraction without schema constraints  
**Solution**: Add more specific instructions in `deed_extraction_v2.txt`

### Issue 2: V2 Misses Some Fields
**Cause**: OCR quality or ambiguous text  
**Solution**: Check `combined_ocr_results_*.md` for OCR accuracy

### Issue 3: V2 Too Much Data
**Cause**: Extracting everything, including noise  
**Solution**: Refine prompt to focus on specific sections

### Issue 4: Cannot Compare Outputs
**Cause**: Different JSON structures  
**Solution**: Use JSON diff tool or manual review

---

## ?? Success Metrics

### Completeness
- **V1**: Measures how well schema covers data
- **V2**: Measures how much data exists in documents

### Accuracy
- **V1**: Schema field accuracy (95%+ target)
- **V2**: Dynamic field accuracy (compare manually)

### Coverage
- **V1**: Fields populated vs total schema fields
- **V2**: Information extracted vs total information available

---

## ?? Best Practices

### 1. Always Run Both Versions Initially
- Understand what V1 captures vs V2
- Identify schema gaps
- Discover unexpected fields

### 2. Use V2 for Analysis, V1 for Production
- **V2**: Exploration and discovery
- **V1**: Consistent, reliable production

### 3. Regularly Update Schema
- Review `schema_extensions_*.md`
- Add frequently occurring fields to schema
- Keep schema aligned with reality

### 4. Compare Multiple Documents
- Run 10+ documents through both versions
- Look for patterns
- Identify common missing fields

### 5. Document Findings
- Keep notes on V1 vs V2 differences
- Track schema enhancement requests
- Monitor extraction quality over time

---

## ?? Future Enhancements

### Potential Improvements
1. **Automated Comparison** - Script to diff V1 vs V2 JSON
2. **Confidence Scoring** - Rate extraction quality
3. **Field Suggestions** - AI suggests schema additions
4. **Hybrid Mode** - Use schema for core fields, dynamic for extras
5. **Validation Layer** - Cross-check V1 vs V2 for accuracy

---

## ?? Example Comparison

### V1 Output (Schema-based)
```json
{
  "parcel_match_cards_component": {
    "mainParcels": [{
      "oldOwners": [{
  "lastName": "Smith",
        "percentage": 50,
        "customFields": {
          "owner_full_name": "John Smith"
        }
      }]
    }]
  }
}
```

### V2 Output (Dynamic)
```json
{
  "sellerInformation": {
    "totalSellers": 2,
    "sellers": [{
      "fullName": "John Smith",
      "firstName": "John",
      "lastName": "Smith",
      "ownershipPercentage": 50,
      "address": {
        "street": "456 Oak Ave",
     "city": "Los Angeles",
    "state": "CA",
    "zipCode": "90001"
      },
    "dateAcquired": "2015-03-15",
      "vesting": "as joint tenants"
    }]
  }
}
```

**Analysis:**
- ? V2 extracted seller address (not in V1 schema)
- ? V2 extracted date acquired (not in V1 schema)
- ? V2 extracted vesting type (not in V1 schema)
- ?? **Action**: Consider adding these fields to V1 schema

---

## ? Implementation Checklist

- [x] Created `deed_extraction_v2.txt` prompt template
- [x] Modified `ProcessWithChatCompletion()` to support dual versions
- [x] Added `ProcessVersion1()` method (schema-based)
- [x] Added `ProcessVersion2()` method (dynamic)
- [x] Updated `SaveFinalCleanedJson()` with version suffix
- [x] Added `CreateV2ExtractionSummary()` for V2 analysis
- [x] Added `CountJsonFields()` helper method
- [x] Restored `AnalyzeSchemaExtensions()` method
- [x] Restored `FindExtensions()` and `GetJsonValueType()` methods
- [x] Build successful ?
- [x] Documentation complete ?

---

## ?? Conclusion

You now have a **powerful dual-version extraction system**:

? **V1 (Schema-based)**: Consistent, production-ready  
? **V2 (Dynamic)**: Comprehensive, exploratory  

**Next Steps:**
1. Run on your Grant Deed + PCOR documents
2. Compare V1 and V2 outputs
3. Identify schema gaps
4. Update schema if needed
5. Use findings to improve extraction quality

**Documentation**: Keep this file for reference  
**Support**: Review V2 output summaries for insights  
**Iteration**: Continuously compare and improve

---

**Implementation Date**: 2025-01-28  
**Status**: ? Complete and Ready to Use  
**Build**: ? Successful  
**Feature**: Dual-version prompt system (V1 + V2)
