#!/usr/bin/env bash
# Convert SVG to PNG using best available tool (Inkscape, rsvg-convert, ImageMagick)
# Usage: ./scripts/convert-svg.sh input.svg output.png [width] [height]
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <input.svg> <output.png> [width] [height]"
  exit 2
fi

INPUT="$1"
OUTPUT="$2"
WIDTH="${3:-}"
HEIGHT="${4:-}"

if [ ! -f "$INPUT" ]; then
  echo "Input file not found: $INPUT" >&2
  exit 3
fi

# Prefer Inkscape (1.0+)
if command -v inkscape >/dev/null 2>&1; then
  echo "Using Inkscape to render PNG..."
  if [ -n "$WIDTH" ] || [ -n "$HEIGHT" ]; then
    ARGS=("--export-type=png" "--export-filename=$OUTPUT")
    [ -n "$WIDTH" ] && ARGS+=("--export-width=$WIDTH")
    [ -n "$HEIGHT" ] && ARGS+=("--export-height=$HEIGHT")
    ARGS+=("$INPUT")
    inkscape "${ARGS[@]}"
  else
    inkscape "$INPUT" --export-type=png --export-filename="$OUTPUT"
  fi
  echo "Wrote $OUTPUT"
  exit 0
fi

# Next try rsvg-convert
if command -v rsvg-convert >/dev/null 2>&1; then
  echo "Using rsvg-convert to render PNG..."
  ARGS=("-o" "$OUTPUT" "$INPUT")
  [ -n "$WIDTH" ] && ARGS=("-w" "$WIDTH" "-o" "$OUTPUT" "$INPUT")
  [ -n "$HEIGHT" ] && ARGS=("-h" "$HEIGHT" "-o" "$OUTPUT" "$INPUT")
  # If both provided, include both
  if [ -n "$WIDTH" ] && [ -n "$HEIGHT" ]; then
    rsvg-convert -w "$WIDTH" -h "$HEIGHT" -o "$OUTPUT" "$INPUT"
  else
    rsvg-convert "$INPUT" -o "$OUTPUT"
  fi
  echo "Wrote $OUTPUT"
  exit 0
fi

# Fallback to ImageMagick (magick or convert)
if command -v magick >/dev/null 2>&1; then
  echo "Using ImageMagick (magick) to render PNG..."
  if [ -n "$WIDTH" ] || [ -n "$HEIGHT" ]; then
    SIZE="${WIDTH}x${HEIGHT}"
    magick convert -background none "$INPUT" -resize "$SIZE" "$OUTPUT"
  else
    magick convert -background none "$INPUT" "$OUTPUT"
  fi
  echo "Wrote $OUTPUT"
  exit 0
fi

if command -v convert >/dev/null 2>&1; then
  echo "Using ImageMagick (convert) to render PNG..."
  if [ -n "$WIDTH" ] || [ -n "$HEIGHT" ]; then
    SIZE="${WIDTH}x${HEIGHT}"
    convert -background none "$INPUT" -resize "$SIZE" "$OUTPUT"
  else
    convert -background none "$INPUT" "$OUTPUT"
  fi
  echo "Wrote $OUTPUT"
  exit 0
fi

echo "No suitable SVG renderer found. Install Inkscape, librsvg (rsvg-convert) or ImageMagick." >&2
exit 4
