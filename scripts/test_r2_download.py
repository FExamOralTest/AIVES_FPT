"""Sinh presigned GET URL cho object trên R2 và tải thử về để xác nhận audio tải được."""
import json
import sys
import urllib.request

import boto3
from botocore.config import Config

CFG = r"C:\Users\thinh\Documents\GitHub\AIVES_FPT\backend\Aives.Api\appsettings.Development.json"

with open(CFG, encoding="utf-8") as f:
    r = json.load(f)["CloudflareR2"]

key = sys.argv[1] if len(sys.argv) > 1 else "exams/38/sessions/58/students/15/q_35.webm"

s3 = boto3.client(
    "s3",
    endpoint_url=f"https://{r['AccountId']}.r2.cloudflarestorage.com",
    aws_access_key_id=r["AccessKeyId"],
    aws_secret_access_key=r["SecretAccessKey"],
    region_name="auto",
    config=Config(signature_version="s3v4", request_checksum_calculation="when_required"),
)

url = s3.generate_presigned_url("get_object", Params={"Bucket": r["BucketName"], "Key": key}, ExpiresIn=3600)
print(f"key      : {key}")
print(f"presigned: {url[:160]}...")

req = urllib.request.Request(url)
with urllib.request.urlopen(req, timeout=60) as resp:
    data = resp.read()
    print(f"status   : {resp.status}")
    print(f"content-type: {resp.headers.get('Content-Type')}")
    print(f"bytes    : {len(data)}")

out = r"C:\Users\thinh\AppData\Local\Temp\r2_download_test.webm"
with open(out, "wb") as f:
    f.write(data)
print(f"saved    : {out}")
print(f"magic    : {data[:4]} ({data[:4].hex(' ')})")
