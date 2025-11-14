# Date Formatting Rules

## Objective
Convert all dates to consistent YYYY-MM-DD format (ISO 8601) for database storage and processing.

## Standard Format
**Target Format**: `YYYY-MM-DD`
- **Y** = 4-digit year
- **M** = 2-digit month (01-12)
- **D** = 2-digit day (01-31)

**Examples:**
- January 15, 2025 ? `2025-01-15`
- 12/31/2024 ? `2024-12-31`
- Oct 5, 2023 ? `2023-10-05`

## Conversion Rules by Input Format

### Format 1: Month Name with Day and Year
**Pattern**: "Month DD, YYYY" or "Month D, YYYY"

**Examples:**
- "January 15, 2025" ? `2025-01-15`
- "February 5, 2025" ? `2025-02-05`
- "December 31, 2024" ? `2024-12-31`

**Month Names:**
- January (Jan) ? 01
- February (Feb) ? 02
- March (Mar) ? 03
- April (Apr) ? 04
- May ? 05
- June (Jun) ? 06
- July (Jul) ? 07
- August (Aug) ? 08
- September (Sep, Sept) ? 09
- October (Oct) ? 10
- November (Nov) ? 11
- December (Dec) ? 12

### Format 2: MM/DD/YYYY (US Format)
**Pattern**: "MM/DD/YYYY" or "M/D/YYYY"

**Examples:**
- "01/15/2025" ? `2025-01-15`
- "12/31/2024" ? `2024-12-31`
- "3/5/2025" ? `2025-03-05`
- "10/1/2024" ? `2024-10-01`

**Conversion:**
1. Extract month (first part)
2. Extract day (second part)
3. Extract year (third part)
4. Format as YYYY-MM-DD with zero-padding

### Format 3: DD/MM/YYYY (International Format)
**Pattern**: "DD/MM/YYYY" (less common in US documents)

**Note**: Only use if explicitly stated as international format or if day > 12

**Examples:**
- "31/12/2024" ? `2024-12-31`
- "15/01/2025" ? `2025-01-15`

### Format 4: YYYYMMDD (Compact)
**Pattern**: "YYYYMMDD" (8 digits, no separators)

**Examples:**
- "20250115" ? `2025-01-15`
- "20241231" ? `2024-12-31`

**Conversion:**
1. Extract first 4 digits (year)
2. Extract next 2 digits (month)
3. Extract last 2 digits (day)
4. Format as YYYY-MM-DD

### Format 5: Month DD YYYY (No Comma)
**Pattern**: "Month DD YYYY"

**Examples:**
- "January 15 2025" ? `2025-01-15`
- "Dec 31 2024" ? `2024-12-31`

### Format 6: DD-Mon-YYYY
**Pattern**: "DD-Mon-YYYY" (with hyphens)

**Examples:**
- "15-Jan-2025" ? `2025-01-15`
- "31-Dec-2024" ? `2024-12-31`

### Format 7: Partial Dates
**When only year and month available:**
- Use first day of month: "January 2025" ? `2025-01-01`

**When only year available:**
- Use January 1st: "2025" ? `2025-01-01`

## Special Cases

### Case 1: Ambiguous Dates (US vs International)
**Problem**: "03/04/2025" could be:
- March 4, 2025 (US format)
- April 3, 2025 (International format)

**Solution**: **ALWAYS** assume US format (MM/DD/YYYY) unless context indicates otherwise
- "03/04/2025" ? `2025-03-04` (March 4, 2025)

### Case 2: Two-Digit Years
**Pattern**: Dates with YY instead of YYYY

**Rule**: 
- If YY >= 50: 19YY
- If YY < 50: 20YY

**Examples:**
- "12/31/99" ? `1999-12-31`
- "01/15/25" ? `2025-01-15`
- "06/30/50" ? `1950-06-30`

### Case 3: Missing Leading Zeros
**Always add leading zeros:**
- "1/5/2025" ? `2025-01-05`
- "10/3/2024" ? `2024-10-03`

### Case 4: Invalid Dates
**If date is invalid or unclear:**
- Return `null`
- Do NOT guess
- Log a warning if possible

**Examples:**
- "February 30, 2025" ? `null` (February has max 28/29 days)
- "13/45/2025" ? `null` (month 13 and day 45 don't exist)

### Case 5: Date Ranges
**Pattern**: "From DATE to DATE"

**Extract both dates separately:**
- "January 15, 2025 to February 28, 2025"
  - start_date: `2025-01-15`
  - end_date: `2025-02-28`

### Case 6: "Present" or "Current"
**When date says "present" or "current":**
- Use today's date in YYYY-MM-DD format
- Or use a special value: `"PRESENT"`

## Validation Rules

### Rule 1: Valid Day Range
- Day must be 01-31
- Respect month-specific limits:
  - Jan, Mar, May, Jul, Aug, Oct, Dec: 01-31
  - Apr, Jun, Sep, Nov: 01-30
  - Feb: 01-28 (or 01-29 for leap years)

### Rule 2: Valid Month Range
- Month must be 01-12

### Rule 3: Valid Year Range
For property documents:
- Minimum year: 1800
- Maximum year: Current year + 10

### Rule 4: Leap Year Calculation
**A year is a leap year if:**
- Divisible by 4 AND
- (NOT divisible by 100 OR divisible by 400)

**Leap Years**: 2000, 2004, 2008, 2012, 2016, 2020, 2024, 2028
**Not Leap Years**: 1900, 2100, 2200, 2300

## Common Patterns in Property Documents

### Recording Date
```
Input: "Recorded: January 15, 2025"
Output: { "recorded_date": "2025-01-15" }
```

### Notary Date
```
Input: "Acknowledged this 10th day of December, 2024"
Output: { "notary_date": "2024-12-10" }
```

### Execution Date
```
Input: "Executed on 12/20/2024"
Output: { "execution_date": "2024-12-20" }
```

### Filing Date
```
Input: "Filed 01/05/2025"
Output: { "filing_date": "2025-01-05" }
```

## Error Prevention

### Error #1: Wrong Format
**WRONG:**
```json
{
  "date": "01/15/2025"
}
```

**CORRECT:**
```json
{
  "date": "2025-01-15"
}
```

### Error #2: Missing Zero Padding
**WRONG:**
```json
{
  "date": "2025-1-5"
}
```

**CORRECT:**
```json
{
  "date": "2025-01-05"
}
```

### Error #3: Invalid Date
**WRONG:**
```json
{
  "date": "2025-02-30"
}
```

**CORRECT:**
```json
{
  "date": null
}
```

## Conversion Examples

| Input Format | Input Example | Output |
|--------------|---------------|--------|
| Month DD, YYYY | January 15, 2025 | 2025-01-15 |
| MM/DD/YYYY | 01/15/2025 | 2025-01-15 |
| M/D/YYYY | 1/5/2025 | 2025-01-05 |
| YYYYMMDD | 20250115 | 2025-01-15 |
| DD-Mon-YYYY | 15-Jan-2025 | 2025-01-15 |
| Mon DD YYYY | Jan 15 2025 | 2025-01-15 |
| MM/DD/YY | 01/15/25 | 2025-01-15 |
| DD/MM/YYYY | 31/12/2024 | 2024-12-31 |

## Validation Checklist

Before finalizing date extraction:
- [ ] All dates in YYYY-MM-DD format
- [ ] Month is 01-12 (not 1-12)
- [ ] Day is 01-31 (not 1-31)
- [ ] Year is 4 digits (not 2)
- [ ] Date is valid (no Feb 30, Apr 31, etc.)
- [ ] Leap years handled correctly
- [ ] Invalid dates set to null (not guessed)

## Priority Rules

1. **ALWAYS** convert to YYYY-MM-DD format
2. **ALWAYS** use zero-padding for month and day
3. **ALWAYS** use 4-digit year
4. **ASSUME** US format (MM/DD/YYYY) when ambiguous
5. **VALIDATE** date is actually possible (no invalid dates)
6. **RETURN** null for unparseable dates (don't guess)
