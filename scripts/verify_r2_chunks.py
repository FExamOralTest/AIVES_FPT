import json
import sys

import boto3
from botocore.config import Config

cfg = json.load(open(r"C:\Users\thinh\Documents\GitHub\AIVES_FPT\backend\Aives.Api\appsettings.Development.json", encoding="utf-8"))
r2 = cfg["CloudflareR2"]

s3 = boto3.client(
    "s3",
    endpoint_url=f"https://{r2['AccountId']}.r2.cloudflarestorage.com",
    aws_access_key_id=r2["AccessKeyId"],
    aws_secret_access_key=r2["SecretAccessKey"],
    region_name="auto",
    config=Config(signature_version="s3v4", request_checksum_calculation="when_required"),
)

prefix = sys.argv[1] if len(sys.argv) > 1 else ""
bucket = r2["BucketName"]

print(f"bucket={bucket} prefix={prefix!r}")
print("-" * 70)

paginator = s3.get_paginator("list_objects_v2")
total = 0
for page in paginator.paginate(Bucket=bucket, Prefix=prefix):
    for obj in page.get("Contents", []):
        total += 1
        print(f"  {obj['Size']:>12,} bytes  {obj['Key']}")
print("-" * 70)
print(f"total objects: {total}")

# Kiểm tra multipart uploads đang dang dở (chunks chưa gộp)
mp = s3.list_multipart_uploads(Bucket=bucket)
parts = mp.get("Uploads", [])
print(f"dang multipart (chua gop): {len(parts)}")
for u in parts:
    print(f"  key={u['Key']} uploadId={u['UploadId']}")
