import os
import pytest

# Skip if no Gemini key - allow_module_level needed for module-level skip
if not os.environ.get("GEMINI_API_KEY"):
    pytest.skip("GEMINI_API_KEY not set - skipping live test", allow_module_level=True)


@pytest.mark.asyncio
async def test_gemini_interactions_live():
    """Test real Gemini Interactions API call."""
    from app.core.ai_client import get_ai_client
    
    client = get_ai_client()
    # Simple test prompt
    response = await client.generate_content("Xin chào, thế giới!")
    assert response is not None
    assert len(response) > 0