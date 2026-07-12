#!/bin/bash

if ! command -v node >/dev/null 2>&1; then
  exit 0
fi

node scripts/config-doctor.js 2>/dev/null || true
exit 0
