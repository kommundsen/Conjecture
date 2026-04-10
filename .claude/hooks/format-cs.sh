#!/bin/bash
# Auto-format .cs files after Edit/Write tool calls.
# Runs dotnet format on the changed file only; always exits 0 (non-blocking).

FILE_PATH=$(cat | jq -r '.tool_input.file_path')

case "$FILE_PATH" in
  *.cs)
    REL_PATH="${FILE_PATH#$CLAUDE_PROJECT_DIR/}"
    dotnet format src/ --include "$REL_PATH" --exclude-diagnostics IDE0130 2>/dev/null
    ;;
esac

exit 0
