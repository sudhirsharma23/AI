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

### 4. Multiple People Detection
Identify when text contains multiple people:

**Indicators of MULTIPLE people:**
- "AND" between names: "JOHN SMITH AND JANE DOE" = 2 people
- Commas with "and": "JOHN, MARY, AND BOB" = 3 people
- "husband and wife": Always 2 people
- "co-trustees": Multiple people

**Indicators of SINGLE person:**
- "an individual": 1 person
- "Trustee" (without "co-"): 1 person

### 5. Entity Names (Not People)
Recognize when name is for an entity, not a person:
- Corporation: "ABC CORPORATION"
- LLC: "XYZ PROPERTIES, LLC"
- Trust: "SMITH FAMILY TRUST"

**For entities:**
- Store full name as string
- Set entity_type field
- Don't split into firstName/lastName

## Validation Checklist

Before finalizing name extraction:
- [ ] All names converted to Title Case
- [ ] Middle names/initials captured separately
- [ ] Multiple people separated into individual objects
- [ ] Entities identified (not treated as people)
