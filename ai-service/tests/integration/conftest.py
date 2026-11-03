"""
Integration test configuration.
"""
import pytest

def pytest_configure(config):
    """Register custom markers."""
    config.addinivalue_line(
        "markers", "integration: mark test as integration test (requires real AI provider or mock)"
    )
