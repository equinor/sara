"""Pytest setup. Provides dummy settings so importing the package does not
fail when no `.env` is present (matches CI behaviour).
"""

import os

os.environ.setdefault("SARA_SERVER_URL", "http://sara-test.local")
os.environ.setdefault("TENANT_ID", "test-tenant")
os.environ.setdefault("NOTIFIER_CLIENT_ID", "test-client-id")
os.environ.setdefault("NOTIFIER_CLIENT_SECRET", "test-client-secret")
os.environ.setdefault("SARA_APP_REG_SCOPE", "api://test/.default")
