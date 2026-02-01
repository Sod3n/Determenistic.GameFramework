#!/bin/bash

# Compile all documentation articles into a single markdown file

OUTPUT="compiled-documentation.md"

echo "# TurnBasedPrototype.Server.Core - Complete Documentation" > "$OUTPUT"
echo "" >> "$OUTPUT"
echo "Generated: $(date)" >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Add introduction
cat articles/intro.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Installation
echo "# Installation" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/installation/00-installation-setup.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/installation/01-quick-start.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Getting Started
echo "# Getting Started" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/getting-started/01-hello-world.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/getting-started/02-reactions.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/getting-started/03-domain-hierarchy.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Network
echo "# Network" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/network/00-hello-world-multiplayer.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/network/01-network-game-state.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/network/02-network-actions.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/network/03-collective-actions.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/network/04-time-synchronization.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/network/05-network-threads.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# Advanced
echo "# Advanced Topics" >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/01-action-injection.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/02-determinism.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/03-duck-typing.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/04-global-notifications.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
cat articles/advanced/05-observable-attributes.md >> "$OUTPUT"
echo "" >> "$OUTPUT"
echo "---" >> "$OUTPUT"
echo "" >> "$OUTPUT"

# TODO
cat articles/TODO.md >> "$OUTPUT"

echo "Documentation compiled to: $OUTPUT"
