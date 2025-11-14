# Quick Start: V2 Dynamic Extraction

## ? Ready to Use!

Your ImageTextExtractor now supports **TWO extraction modes**:
- **V1**: Schema-based (existing, production-ready)
- **V2**: Dynamic (new, discovers all fields)

---

## ?? Run It Now

```powershell
cd E:\Sudhir\GitRepo\AzureTextReader\src
dotnet run
```

**Both versions run automatically!**

---

## ?? What You'll Get

After running, check `OutputFiles/` folder:

### V1 (Schema-based) Outputs:
- `final_output_TIMESTAMP_v1_schema.json` - Extraction using your schema
- `schema_extensions_TIMESTAMP.md` - Fields found beyond schema

### V2 (Dynamic) Outputs:
- `final_output_TIMESTAMP_v2_dynamic.json` - Dynamic extraction (no schema)
- `v2_extraction_summary_TIMESTAMP.md` - Statistics and key info

### Common Outputs:
- `combined_ocr_results_TIMESTAMP.md` - Raw OCR text

---

## ?? What to Look For

### 1. Open V1 Output (Schema-based)
```powershell
code OutputFiles\final_output_*_v1_schema.json
```

**Check:**
- ? Buyer names and percentages
- ? Seller (old owner) information
- ? Property address and legal description
- ? Transaction details

### 2. Open V2 Output (Dynamic)
```powershell
code OutputFiles\final_output_*_v2_dynamic.json
```

**Look for:**
- ?? More buyer/seller details (addresses, dates)
- ?? Additional property information
- ?? PCOR checkboxes (all of them!)
- ?? Tax exemptions and property tax info
- ?? Escrow and title company details
- ?? Any other fields not in V1

### 3. Compare Them!

**Question: Did V2 find information that V1 missed?**

**Example:**
```json
// V1 might have:
"buyers": [
  {
    "lastName": "Smith",
  "percentage": 50
  }
]

// V2 might have:
"buyers": [
  {
  "fullName": "John David Smith",
    "firstName": "John",
    "middleName": "David",
  "lastName": "Smith",
    "ownershipPercentage": 50,
    "address": {
    "street": "123 Main St",
      "city": "Los Angeles",
    "state": "CA",
    "zipCode": "90001"
    },
    "vesting": "as joint tenants",
 "contactInfo": {
   "phone": "(555) 123-4567"
    }
  }
]
```

**If V2 found more details ? Consider adding them to your schema!**

---

## ?? Quick Analysis

### Step 1: Check V2 Summary
```powershell
code OutputFiles\v2_extraction_summary_*.md
```

**Shows:**
- Total fields extracted
- Number of buyers/sellers
- Property address
- Sale price
- Whether PCOR was present

### Step 2: Check V1 Extensions
```powershell
code OutputFiles\schema_extensions_*.md
```

**Shows:**
- Fields AI tried to add to schema
- Suggestions for schema improvements

---

## ?? Your Documents (Grant Deed + PCOR)

Your documents contain:
1. **Grant Deed** - Property transfer from old owners to new buyers
2. **PCOR** (Preliminary Change of Ownership Report) - Checkboxes and tax info

### V1 Will Extract:
- ? Structured fields from schema
- ? Buyer/seller basics
- ? Property address
- ? Some PCOR fields

### V2 Will Extract:
- ? **Everything** V1 extracts PLUS:
- ?? Complete buyer/seller addresses
- ?? Ownership vesting type
- ?? ALL PCOR checkboxes (not just predefined ones)
- ?? Property tax year and amount
- ?? Tax exemptions (Prop 13, 19, 58, 193)
- ?? Escrow/title company info
- ?? Recording fees and documentary transfer tax
- ?? Notary information
- ?? Any other details in the documents

---

## ?? Focus Areas for Your Analysis

### A. Sale Data
**What you wanted to see:**
- How many buyers?
- Buyer names, addresses, percentages
- Contact information

**Check in V2:**
```json
"buyerInformation": {
  "totalBuyers": 2,
  "buyers": [
    {
      "fullName": "...",
      "address": { "street": "...", "city": "...", "state": "...", "zipCode": "..." },
"ownershipPercentage": 50,
      "vesting": "as joint tenants",
      "contactInfo": { "phone": "...", "email": "..." }
 }
  ]
}
```

### B. Land Records Information
**Check in V2:**
```json
"propertyInformation": {
  "legalDescription": {
    "lotNumber": "...",
    "blockNumber": "...",
 "tract": "...",
    "subdivision": "...",
    "fullLegalDescription": "..."
  }
}
```

### C. Parcel Details with Owner Information
**Check in V2:**
```json
"sellerInformation": {
  "totalSellers": 2,
  "sellers": [
    {
      "fullName": "...",
      "ownershipPercentage": 50,
      "address": { ... },
      "dateAcquired": "YYYY-MM-DD",
      "vesting": "as husband and wife"
    }
  ]
},
"propertyInformation": {
  "parcelInformation": {
    "assessorParcelNumber": "1234-567-890",
    "parcelSize": "0.25 acres"
  }
}
```

---

## ?? Comparison Workflow

### Your Goal: Compare Schema vs Dynamic

1. **Run the application** ? Generates both V1 and V2 outputs

2. **Open V1 output** ? See what schema captured
   ```powershell
 code OutputFiles\final_output_*_v1_schema.json
   ```

3. **Open V2 output** ? See what AI discovered
   ```powershell
   code OutputFiles\final_output_*_v2_dynamic.json
   ```

4. **Compare sections:**
   - Buyer information: V2 more complete?
   - Seller information: V2 has addresses?
   - Property details: V2 has more fields?
   - PCOR data: V2 captured all checkboxes?

5. **Decide:**
   - Add V2 fields to schema? (for consistency)
   - Keep V2 for exploratory analysis?
 - Use hybrid approach?

---

## ??? If You Want to Run Only V2

Edit `Program.cs`, line ~195:
```csharp
// Comment out V1
// await ProcessVersion1(memoryCache, chatClient, combinedMarkdown, timestamp);

// Keep V2
await ProcessVersion2(memoryCache, chatClient, combinedMarkdown, timestamp);
```

Rebuild and run:
```powershell
dotnet build
dotnet run
```

---

## ?? Example: What V2 Might Find

For a Grant Deed + PCOR document set:

### V2 Structure:
```json
{
  "documentAnalysis": {
    "documentsIdentified": ["Grant Deed", "PCOR"]
  },
  
  "buyerInformation": {
    "totalBuyers": 2,
    "buyers": [
  /* Complete buyer details with addresses */
  ]
  },
  
  "sellerInformation": {
    "totalSellers": 2,
    "sellers": [
      /* Complete seller details with acquisition dates */
    ]
  },
  
  "propertyInformation": {
    "address": { /* Full property address */ },
    "legalDescription": { /* Lot, block, tract */ },
    "parcelInformation": { /* APN, size, type */ }
  },
  
  "transactionDetails": {
    "salePrice": 450000,
    "recordingInformation": { /* Date, doc number, fees */ },
    "dates": { /* Execution, notary, transfer */ },
    "fees": { /* Transfer tax, recording, escrow */ }
  },
  
  "pcorInformation": {
    "ownershipChange": { /* Transfer details */ },
    "transferCircumstances": {
      /* ALL checkboxes: yes/no for each */
      "purchaseFromRelated": "no",
      "receivedAsGift": "no",
      "transferIntoTrust": "no",
      /* ... many more */
    },
    "taxExemptions": {
      "prop13Applicable": "no",
      "prop19Applicable": "no",
      /* ... */
    },
    "propertyTaxInformation": {
      "currentTaxAmount": 5400,
      "taxYear": "2024-2025"
    }
}
}
```

---

## ? Quick Tips

### Tip 1: Focus on Differences
Don't read entire JSON files. Instead:
- Check buyer count: Same in V1 and V2?
- Check buyer addresses: Present in V2, missing in V1?
- Check PCOR section: More checkboxes in V2?

### Tip 2: Use JSON Viewers
Install VS Code JSON extension or use online tools:
- https://jsonviewer.stack.hu/
- https://jsonformatter.org/

### Tip 3: Search for Key Fields
Use Ctrl+F to find specific data:
- Search "phone" in V2 ? Found contact info?
- Search "taxYear" in V2 ? Found property tax year?
- Search "escrowCompany" in V2 ? Found escrow details?

### Tip 4: Check File Sizes
```powershell
dir OutputFiles\final_output_*_v*.json
```
If V2 file is **much larger** than V1 ? V2 found more data!

---

## ? Success Checklist

After running:
- [ ] Both V1 and V2 files generated
- [ ] V1 has buyer/seller basic info
- [ ] V2 has more buyer/seller details
- [ ] V2 has property tax information
- [ ] V2 has PCOR checkboxes
- [ ] Summary files created
- [ ] Can compare side-by-side

---

## ?? You're Ready!

**Run the application and see the magic:**
```powershell
dotnet run
```

**Then analyze:**
1. V1 vs V2 JSON files
2. Summary reports
3. Decide on schema improvements

**Questions?**
- Full docs: `V2_DYNAMIC_EXTRACTION_COMPLETE.md`
- Prompt details: `deed_extraction_v2.txt`

---

**Implementation**: ? Complete  
**Build**: ? Successful  
**Your Next Step**: Run `dotnet run` and compare outputs!
