# Percentage Calculation Rules

## CRITICAL INSTRUCTIONS FOR OWNER/BUYER PERCENTAGES

### Rule 1: Count FIRST, Calculate SECOND
**ALWAYS** count the total number of owners/buyers BEFORE calculating percentages.

### Rule 2: Percentage Calculation Formula
```
IF 1 BUYER/OWNER  -> percentage = 100 (NOT 50!)
IF 2 BUYERS/OWNERS -> percentage = 50 for EACH
IF 3 BUYERS/OWNERS -> percentage = 33.33 for EACH (one gets 33.34 to total 100)
IF 4 BUYERS/OWNERS -> percentage = 25 for EACH
```

**Formula**: `percentage = 100 / (ACTUAL count of entries)`

### Rule 3: Multiple Entries Validation
- For buyer_names_component array: Create ONE object per buyer
- For oldOwners array: Create ONE object per previous owner
- NEVER combine multiple people into a single entry
- Each person must have their own complete object

### Rule 4: Setting Primary Flag
- Set `buyerIsPrimary = true` for the FIRST buyer only
- Set `buyerIsPrimary = false` for ALL others
- Set `principal = true` for the FIRST old owner only
- Set `principal = false` for ALL others

### Rule 5: Validation Checklist
Before returning JSON, verify:
- [ ] Count matches: number of entries = number of names found
- [ ] Percentages sum to 100 (allow 0.01 tolerance for rounding)
- [ ] First entry has isPrimary/principal = true
- [ ] Remaining entries have isPrimary/principal = false

## Common Patterns

### Pattern 1: Identifying Multiple People
These patterns indicate MULTIPLE people (create separate entries):
- "X AND Y" = 2 people
- "X, Y, AND Z" = 3 people
- "husband and wife" = 2 people
- "joint tenants" with multiple names = multiple people
- ownerNames: ["Name1", "Name2"] = 2 people

### Pattern 2: Identifying Single Person
These patterns indicate ONE person (percentage = 100):
- ownerNames: ["Name1"] (array with ONE element) = 1 person
- "John Smith, an individual" = 1 person
- "ABC Corporation" (no "and" connector) = 1 entity
- "X, Trustee" (no "and" connector) = 1 person

## ERROR PREVENTION

### Common Error #1: Wrong Percentage for Single Owner
**WRONG**:
```json
{
  "oldOwners": [{
    "firstName": "John",
    "lastName": "Smith",
    "percentage": 50
  }]
}
```

**CORRECT**:
```json
{
  "oldOwners": [{
    "firstName": "John",
    "lastName": "Smith",
    "percentage": 100
  }]
}
```

### Common Error #2: Missing Second Owner
**WRONG** (only one entry for two people):
```json
{
  "oldOwners": [{
    "firstName": "Charles",
    "lastName": "Shapiro",
    "percentage": 100
  }]
}
```

**CORRECT** (two entries for two people):
```json
{
  "oldOwners": [
    {
      "firstName": "Charles",
      "lastName": "Shapiro",
      "percentage": 50,
      "principal": true
    },
    {
   "firstName": "Suzanne",
    "lastName": "Shapiro",
      "percentage": 50,
      "principal": false
    }
  ]
}
```

### Common Error #3: Incorrect Three-Way Split
**WRONG**:
```json
[
  {"buyerPercentage": 33},
  {"buyerPercentage": 33},
  {"buyerPercentage": 33}
]
// Total = 99, not 100!
```

**CORRECT**:
```json
[
  {"buyerPercentage": 33.33},
  {"buyerPercentage": 33.33},
  {"buyerPercentage": 33.34}
]
// Total = 100.00
```

## Step-by-Step Process

### For Old Owners Array:
1. Look for "ownerNames" array in source data
2. **COUNT** the number of elements
3. Look for phrases like "X AND Y" in granting text
4. **COUNT** how many separate names found
5. Create EXACTLY that many entries
6. Calculate: percentage = 100 / count
7. Set first entry principal = true, rest = false

### For Buyer Names Component:
1. Look for grantee names in deed
2. **COUNT** how many buyers (look for "and" between names)
3. Create ONE entry per buyer
4. Calculate: buyerPercentage = 100 / count
5. Set first entry buyerIsPrimary = true, rest = false

## Quick Reference Table

| Number of Owners | Each Percentage | First Entry | Other Entries |
|-----------------|----------------|-------------|---------------|
| 1 | 100 | principal: true | N/A |
| 2 | 50 each | principal: true | principal: false |
| 3 | 33.33, 33.33, 33.34 | principal: true | principal: false |
| 4 | 25 each | principal: true | principal: false |

## Final Reminder

**DO NOT:**
- Default to 50% without counting
- Use 50% for single owners
- Forget to sum check (total must = 100)
- Skip creating entries for each person

**DO:**
- Count FIRST, calculate SECOND
- Verify totals = 100
- Create separate entries for each person
- Set primary flags correctly
