#!/bin/bash

# Compile all documentation articles into a single markdown file

OUTPUT="compiled-documentation.md"

echo "# Deterministic Game Framework V2 - Documentation" > "$OUTPUT"
echo "" >> "$OUTPUT"
echo "Generated: $(date)" >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Introduction
cat articles/intro.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Getting Started
echo "# Getting Started" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/getting-started/quickstart.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Core Concepts
echo "# Core Concepts" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/concepts/determinism.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/concepts/ecs.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/concepts/actions-reactions.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/concepts/networking.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/concepts/serialization.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Advanced Topics
echo "# Advanced Topics" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/generators.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/best-practices.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/testing.md >> "$OUTPUT"
echo "" >> "$OUTPUT"

echo "Documentation compiled to: $OUTPUT"
