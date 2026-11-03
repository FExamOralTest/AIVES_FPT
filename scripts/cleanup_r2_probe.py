"""Xoá object test rác (probe/) khỏi bucket R2. Dùng khi chạy probe signing."""
import json

import boto3
from botocore.config import Config

CFG = r"C:\Users\thinh\Documents\GitHub\AIVES_FPT\backend\Aives.Api\appsettings.Development.json"

with open(CFG, encoding="utf-8") as f:
    r = json.load(f)["CloudflareR2"]

s3 = boto3.client(
    "s3",
    endpoint_url=f"https://{r['AccountId']}.r2.cloudflarestorage.com",
    aws_access_key_id=r["AccessKeyId"],
    aws_secret_access_key=r["SecretAccessKey"],
    region_name="auto",
    config=Config(signature_version="s3v4", request_checksum_calculation="when_required"),
)

keys = [
    o["Key"]
    for o in s3.list_objects_v2(Bucket=r["BucketName"]).get("Contents", [])
    if o["Key"].startswith("probe/")
]
for k in keys:
    s3.delete_object(Bucket=r["BucketName"], Key=k)

print(f"deleted probe objects: {len(keys)}")
for k in keys:
    print(f"  - {k}")
