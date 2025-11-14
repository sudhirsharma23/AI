# Name Parsing Rules

## Objective
Extract and properly format person names from property transaction documents.

## Core Rules

### 1. Full Name Parsing
When you encounter a full name, split it into components:
- **First word** ? `firstName`
- **Last word** ? `lastName`
- **Middle word(s)** ? `middleName`

**Examples:**
- "JOHN SMITH" ? firstName: "John", lastName: "Smith"
- "MARY ANN JOHNSON" ? firstName: "Mary", middleName: "Ann", lastName: "Johnson"
- "ROBERT J. WILLIAMS" ? firstName: "Robert", middleName: "J.", lastName: "Williams"

### 2. Name with Suffixes
Handle generational suffixes properly:
- "JOHN SMITH JR" ? firstName: "John", lastName: "Smith", suffix: "Jr"
- "ROBERT BROWN III" ? firstName: "Robert", lastName: "Brown", suffix: "III"
- "JAMES DAVIS SR" ? firstName: "James", lastName: "Davis", suffix: "Sr"

**Common Suffixes**: Jr, Sr, II, III, IV, Esq

### 3. Capitalization
Convert ALL CAPS names to proper Title Case:
- "JOHN SMITH" ? "John Smith"
- "MARY-ANN JOHNSON" ? "Mary-Ann Johnson"
- "O'BRIEN" ? "O'Brien"
- "MCDONALD" ? "McDonald"

### 4. Initials
Preserve initials with periods:
- "J." remains "J."
- "John D." ? firstName: "John", middleName: "D."
- "R.J. SMITH" ? firstName: "R.J.", lastName: "Smith"

### 5. Hyphenated Names
Keep hyphens intact:
- "MARY-ANN" ? "Mary-Ann" (single firstName)
- "SMITH-JONES" ? "Smith-Jones" (single lastName)
- "JOHN-PAUL GARCIA" ? firstName: "John-Paul", lastName: "Garcia"

### 6. Name Prefixes
Preserve name prefixes:
- "VAN DER BERG" ? lastName: "Van Der Berg"
- "DE LA CRUZ" ? lastName: "De La Cruz"
- "VON SCHMIDT" ? lastName: "Von Schmidt"

**Common Prefixes**: Van, Von, De, Del, La, Le, Du, Da, Di, Della

### 7. Multiple People Detection
Identify when text contains multiple people:

**Indicators of MULTIPLE people:**
- "AND" between names: "JOHN SMITH AND JANE DOE" = 2 people
- Commas with "and": "JOHN, MARY, AND BOB" = 3 people
- "husband and wife": Always 2 people
- "co-trustees": Multiple people
- Array with multiple elements: ["Name1", "Name2"] = 2 people

**Indicators of SINGLE person:**
- "an individual": 1 person
- "a single person": 1 person
- "Trustee" (without "co-"): 1 person
- Array with one element: ["Name1"] = 1 person

### 8. Entity Names (Not People)
Recognize when name is for an entity, not a person:
- Corporation: "ABC CORPORATION"
- LLC: "XYZ PROPERTIES, LLC"
- Trust: "SMITH FAMILY TRUST"
- Partnership: "JONES & ASSOCIATES, LP"

**For entities:**
- Store full name as string
- Set entity_type field
- Don't split into firstName/lastName

### 9. Special Characters
Handle special characters correctly:
- Apostrophes: "O'BRIEN" ? "O'Brien"
- Periods: "JOHN D. SMITH" ? firstName: "John", middleName: "D.", lastName: "Smith"
- Hyphens: Preserve as-is
- Remove extraneous punctuation

### 10. Name Extraction from Context

**From "Grantor" sections:**
```
"Grantor: JOHN SMITH, AN INDIVIDUAL"
Extract: firstName: "John", lastName: "Smith"
```

**From deed language:**
```
"CHARLES D. SHAPIRO and SUZANNE D. SHAPIRO, husband and wife"
Extract TWO people:
1. firstName: "Charles", middleName: "D.", lastName: "Shapiro"
2. firstName: "Suzanne", middleName: "D.", lastName: "Shapiro"
```

**From trust language:**
```
"JOHN SMITH, Trustee of the Smith Family Trust"
Extract: firstName: "John", lastName: "Smith"
Store separately: trustName: "Smith Family Trust"
```

## Common Patterns

### Pattern 1: Standard Name Format
```
Input: "JOHN DAVID SMITH"
Output:
{
  "firstName": "John",
  "middleName": "David",
  "lastName": "Smith"
}
```

### Pattern 2: Name with Initial
```
Input: "MARY J. JOHNSON"
Output:
{
  "firstName": "Mary",
  "middleName": "J.",
  "lastName": "Johnson"
}
```

### Pattern 3: Compound Last Name
```
Input: "CARLOS DE LA CRUZ"
Output:
{
  "firstName": "Carlos",
  "lastName": "De La Cruz"
}
```

### Pattern 4: Two People
```
Input: "JOHN SMITH AND JANE SMITH"
Output:
[
  {
    "firstName": "John",
    "lastName": "Smith"
  },
  {
    "firstName": "Jane",
    "lastName": "Smith"
  }
]
```

## Error Prevention

### Common Error #1: Splitting Entity Names
**WRONG:**
```json
{
  "firstName": "Smith",
  "lastName": "Trust"
}
```

**CORRECT:**
```json
{
  "entityName": "Smith Family Trust",
  "entityType": "Trust"
}
```

### Common Error #2: Missing Middle Name
**WRONG:**
```json
{
  "firstName": "John David",
  "lastName": "Smith"
}
```

**CORRECT:**
```json
{
  "firstName": "John",
  "middleName": "David",
  "lastName": "Smith"
}
```

### Common Error #3: Treating Two People as One
**WRONG:**
```json
{
  "firstName": "John and Jane",
  "lastName": "Smith"
}
```

**CORRECT:**
```json
[
  { "firstName": "John", "lastName": "Smith" },
  { "firstName": "Jane", "lastName": "Smith" }
]
```

## Validation Checklist

Before finalizing name extraction:
- [ ] All names converted to Title Case (not ALL CAPS)
- [ ] Middle names/initials captured separately
- [ ] Suffixes (Jr, Sr, III) handled correctly
- [ ] Multiple people separated into individual objects
- [ ] Entities identified (not treated as people)
- [ ] Hyphens and apostrophes preserved
- [ ] Name prefixes (Van, De, etc.) kept with last name

## Priority Rules

1. **ALWAYS** check for multiple people first (look for "AND")
2. **PRESERVE** middle names/initials as separate field
3. **DISTINGUISH** between people and entities
4. **MAINTAIN** proper capitalization (Title Case)
5. **RESPECT** cultural naming conventions (prefixes, hyphens)
