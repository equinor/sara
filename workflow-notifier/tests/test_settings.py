from workflow_notifier.config.settings import Settings

# Required env vars that Settings always needs.
_REQUIRED = {
    "SARA_SERVER_URL": "http://localhost:8100/api",
    "TENANT_ID": "00000000-0000-0000-0000-000000000000",
    "NOTIFIER_CLIENT_ID": "dummy-client-id",
    "SARA_APP_REG_SCOPE": "api://dummy/.default",
}


class TestSettingsAllowedAuthMethods:
    """Ensure ALLOWED_AUTH_METHODS is correctly parsed from env vars."""

    def test_default_value(self, monkeypatch):
        for k, v in _REQUIRED.items():
            monkeypatch.setenv(k, v)
        monkeypatch.delenv("ALLOWED_AUTH_METHODS", raising=False)

        s = Settings()
        assert s.allowed_auth_methods == ["WorkloadIdentity"]

    def test_single_value_from_env(self, monkeypatch):
        for k, v in _REQUIRED.items():
            monkeypatch.setenv(k, v)
        monkeypatch.setenv("ALLOWED_AUTH_METHODS", "ClientSecret")

        s = Settings()
        assert s.allowed_auth_methods == ["ClientSecret"]

    def test_comma_separated_from_env(self, monkeypatch):
        for k, v in _REQUIRED.items():
            monkeypatch.setenv(k, v)
        monkeypatch.setenv("ALLOWED_AUTH_METHODS", "WorkloadIdentity,ClientSecret")

        s = Settings()
        assert s.allowed_auth_methods == ["WorkloadIdentity", "ClientSecret"]

    def test_whitespace_is_stripped(self, monkeypatch):
        for k, v in _REQUIRED.items():
            monkeypatch.setenv(k, v)
        monkeypatch.setenv("ALLOWED_AUTH_METHODS", " WorkloadIdentity , ClientSecret ")

        s = Settings()
        assert s.allowed_auth_methods == ["WorkloadIdentity", "ClientSecret"]

    def test_empty_segments_ignored(self, monkeypatch):
        for k, v in _REQUIRED.items():
            monkeypatch.setenv(k, v)
        monkeypatch.setenv("ALLOWED_AUTH_METHODS", ",WorkloadIdentity,,ClientSecret,")

        s = Settings()
        assert s.allowed_auth_methods == ["WorkloadIdentity", "ClientSecret"]

    def test_raw_field_is_str(self, monkeypatch):
        for k, v in _REQUIRED.items():
            monkeypatch.setenv(k, v)
        monkeypatch.setenv("ALLOWED_AUTH_METHODS", "WorkloadIdentity")

        s = Settings()
        assert isinstance(s.ALLOWED_AUTH_METHODS, str)
        assert s.ALLOWED_AUTH_METHODS == "WorkloadIdentity"
