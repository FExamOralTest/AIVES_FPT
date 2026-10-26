"""
Quick test GeminiAIClient sau khi sửa.
"""
import asyncio
import sys
import os

# Thêm path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from app.core.ai_client import GeminiAIClient

async def test():
    client = GeminiAIClient()
    
    system = "You are a helpful assistant. Return valid JSON only."
    user = '{"task": "test", "question": "What is 2+2?"}'
    
    print("Testing GeminiAIClient...", file=sys.stderr)
    result = await client.complete(system, user)
    print(f"SUCCESS: {result[:200]}", file=sys.stderr)
    print("OK")

if __name__ == "__main__":
    try:
        asyncio.run(test())
    except Exception as e:
        print(f"FAILED: {e}", file=sys.stderr)
        sys.exit(1)
