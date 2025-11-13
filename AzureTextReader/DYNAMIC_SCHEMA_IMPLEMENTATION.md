# Dynamic Schema Extension Implementation Guide

## Overview
The ImageTextExtractor now supports **dynamic schema extension** - the AI model can identify and include fields found in OCR documents that aren't in the base schema.

## Key Features Implemented

### 1. Dynamic Schema Extension in Prompt
The system prompt now instructs the model to:
- Analyze ALL document content first
- Identify relevant fields NOT in the base schema
- Add those fields to appropriate sections with proper naming
- Follow snake_case convention for new fields
- Choose appropriate data types

### 2. Schema Extension Analysis
After extraction, the system analyzes the output and:
- Compares extracted JSON with base schema
- Identifies all extended fields
- Generates a report showing:
  - Field paths
  - Data types
  - Recommendations for schema updates

### 3. File Outputs (3 files total)
1. **`combined_ocr_results_{timestamp}.md`** - OCR data from all documents
2. **`final_output_{timestamp}.json`** - Complete JSON with base + extended fields
3. **`schema_extensions_{timestamp}.md`** - Report of fields added beyond base schema

## Common Extended Fields

The prompt suggests the model look for these additional fields:

### Document Details
- `document_recording_number`, `book_number`, `page_number`, `instrument_number`
- `filing_date`, `execution_date`, `acknowledgment_date`
- `documentary_transfer_tax`, `recording_fee`, `total_fees`

### Property Details
- `property_tax_year`, `property_tax_amount`, `property_tax_status`
- `zoning_classification`, `land_use_type`, `lot_number`, `block_number`
- `square_footage`, `acreage`, `number_of_units`
- `flood_zone`, `fire_hazard_zone`, `seismic_zone`

### Transfer/Transaction Details
- `consideration_amount`, `cash_consideration`, `other_consideration`
- `financing_type`, `loan_amount`, `lender_name`
- `escrow_number`, `escrow_company`, `title_company`
- `real_estate_agent`, `broker_information`

### Owner/Buyer Details (added to customFields)
- `owner_phone_number`, `owner_email`, `owner_marital_status`
- `entity_type`, `entity_state_of_formation`
- `contact_phone`, `contact_email`
- `mailing_address_different_from_property`

### Legal/Compliance
- `affidavit_of_death`, `court_order_number`
- `prop_13_base_year_transfer`, `prop_19_eligibility`
- `senior_citizen_exemption`, `disabled_veteran_exemption`

### Transfer Information (Preliminary Change of Ownership)
- Any additional checkboxes beyond base schema
- Preparer information, signature dates
- Code section references

## Example Output Structure

```json
{
  "records": [{
    "saleData": {
      // BASE SCHEMA FIELDS
      "cos_price": 450000,
      "recorded_date": "2025-01-15",
   "document_source_id": "2025-123456",
      
      // DYNAMICALLY ADDED FIELDS
      "document_recording_number": "20250115-00123456",
      "book_number": "12345",
      "page_number": "678",
      "documentary_transfer_tax": 495.00,
      "recording_fee": 75.00,
      "property_tax_year": "2024-2025",
      "escrow_number": "ESC-2025-001234",
      "title_company": "First American Title",
  
      "transfer_information": {
        // BASE SCHEMA FIELDS
        "change_in_ownership_or_control": "yes",
     "transfer_of_interest_in_real_property": "yes",
        
        // DYNAMICALLY ADDED FIELDS
        "prop_13_base_year_value_transfer": "no",
        "prop_19_eligible_transfer": "no",
  "preparer_name": "John Doe",
        "preparer_phone": "555-1234",
        "preparer_signature_date": "2025-01-10",
        "revenue_code_section_applied": "Section 62(a)(1)"
      },
      
      "parcel_match_cards_component": {
        "mainParcels": [{
          "oldOwners": [
            {
       "lastName": "Smith",
          "percentage": 50,
              "customFields": {
    // BASE SCHEMA FIELDS
          "owner_full_name": "John A. Smith",
       "owner_date_acquired": "2020-05-15",
   
                // DYNAMICALLY ADDED FIELDS
            "owner_phone_number": "555-9876",
                "owner_email": "john.smith@example.com",
        "owner_marital_status": "married",
              "owner_citizenship": "US Citizen"
  }
   }
          ],
 
          // DYNAMICALLY ADDED FIELDS at parcel level
   "lot_number": "15",
          "block_number": "A",
        "square_footage": 2500,
          "zoning_classification": "R-1"
        }]
      }
    }
  }]
}
```

## Schema Extensions Report Example

```markdown
# Schema Extensions Report
Generated: 2025-01-28 12:00:00 UTC

Total Extended Fields: 15

## Extended Fields:
```
  + saleData.document_recording_number : string
  + saleData.book_number : string
  + saleData.page_number : string
  + saleData.documentary_transfer_tax : number
  + saleData.recording_fee : number
  + saleData.property_tax_year : string
  + saleData.escrow_number : string
  + saleData.title_company : string
  + saleData.transfer_information.prop_13_base_year_value_transfer : string
  + saleData.transfer_information.prop_19_eligible_transfer : string
  + saleData.transfer_information.preparer_name : string
  + saleData.transfer_information.preparer_phone : string
  + saleData.parcel_match_cards_component.mainParcels[0].lot_number : string
  + saleData.parcel_match_cards_component.mainParcels[0].square_footage : number
  + saleData.parcel_match_cards_component.mainParcels[0].oldOwners[0].customFields.owner_phone_number : string
```

## Recommendations:
Consider updating the base schema to include frequently occurring extended fields.
```

## How It Works

### Step 1: Model Receives Enhanced Prompt
The model is instructed to:
1. Process all base schema fields
2. Identify additional relevant fields in OCR data
3. Add them with proper naming and typing
4. Return comprehensive JSON

### Step 2: Extraction and Cleaning
- Model returns JSON with both base and extended fields
- JSON is cleaned (removes nulls, empty objects)
- Saved to `final_output_{timestamp}.json`

### Step 3: Schema Analysis
- Compares extracted JSON structure with base schema
- Recursively finds all fields not in base
- Generates extension report
- Saves to `schema_extensions_{timestamp}.md`

### Step 4: Review and Update
- Review the extensions report
- Identify frequently occurring fields
- Update base schema to include common extensions
- Reduces extension overhead over time

## Benefits

### 1. Comprehensive Data Capture
- Nothing is lost - all relevant fields captured
- Adapts to document variations automatically
- No manual schema updates needed immediately

### 2. Evolution Tracking
- Clear visibility into what fields are being added
- Data-driven schema evolution
- Identify patterns across multiple documents

### 3. Flexibility
- Handles unexpected document formats
- Captures jurisdiction-specific fields
- Accommodates new document types

### 4. Maintainability
- Extension reports guide schema updates
- Gradual evolution from dynamic to static
- Reduces prompt complexity over time

## Usage Tips

### 1. Review Extension Reports Regularly
- Check weekly or monthly
- Look for frequently occurring fields
- Update base schema with common patterns

### 2. Field Naming Consistency
The model follows these conventions:
- snake_case for all names
- Descriptive names: `property_tax_year` not just `tax`
- Context prefixes: `buyer_phone`, `seller_phone`
- Date suffix for dates: `signature_date`
- Amount suffix for money: `transfer_tax_amount`

### 3. Data Type Patterns
- Dates ? always YYYY-MM-DD format
- Numbers ? no quotes, decimal for money
- Yes/No ? string type with lowercase values
- Arrays ? for multiple items

### 4. Strategic Schema Updates
After reviewing extensions, add to base schema:
- Fields appearing in >50% of documents
- Critical business fields
- Regulatory/compliance requirements
- Frequently queried data points

## Implementation Status

? Dynamic schema extension in prompt  
? Schema analysis after extraction  
? Extension report generation  
? Field path tracking  
? Data type inference  
? Recommendations output  
? Build successful

## Next Steps

1. **Test with Real Documents** - Process actual deeds/ownership reports
2. **Review Extension Reports** - Analyze what fields are being added
3. **Update Base Schema** - Add common extensions to invoice_schema.json
4. **Refine Prompts** - Adjust field naming guidance based on results
5. **Monitor Patterns** - Track which document types add which fields

---

**Implementation Date**: 2025-01-28  
**Target Framework**: .NET 9  
**Status**: ? Complete & Tested
