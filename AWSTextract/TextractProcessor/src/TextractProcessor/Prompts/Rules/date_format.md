# Date Formatting Rules

## Objective
Convert all dates to consistent YYYY-MM-DD format (ISO 8601) for database storage and processing.

## Standard Format
**Target Format**: `YYYY-MM-DD`

**Examples:**
- January 15, 2025 ? `2025-01-15`
- 12/31/2024 ? `2024-12-31`
- Oct 5, 2023 ? `2023-10-05`

## Conversion Rules by Input Format

### Format 1: Month Name with Day and Year
**Pattern**: "Month DD, YYYY"

**Examples:**
- "January 15, 2025" ? `2025-01-15`
- "December 31, 2024" ? `2024-12-31`

### Format 2: MM/DD/YYYY (US Format)
**Pattern**: "MM/DD/YYYY"

**Examples:**
- "01/15/2025" ? `2025-01-15`
- "3/5/2025" ? `2025-03-05`

### Format 3: Partial Dates
**When only year and month available:**
- Use first day of month: "January 2025" ? `2025-01-01`

## Special Cases

### Case 1: Missing Leading Zeros
**Always add leading zeros:**
- "1/5/2025" ? `2025-01-05`

### Case 2: Invalid Dates
**If date is invalid or unclear:**
- Return `null`
- Do NOT guess

## Validation Rules

### Rule 1: Valid Day Range
- Day must be 01-31
- Respect month-specific limits

### Rule 2: Valid Month Range
- Month must be 01-12

## Priority Rules

1. **ALWAYS** convert to YYYY-MM-DD format
2. **ALWAYS** use zero-padding for month and day
3. **ALWAYS** use 4-digit year
4. **VALIDATE** date is actually possible
5. **RETURN** null for unparseable dates
