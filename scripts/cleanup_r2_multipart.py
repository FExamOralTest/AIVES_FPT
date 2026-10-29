"""Dọn các multipart upload dang dở (orphan) trên Cloudflare R2."""
import json

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

bucket = r2["BucketName"]
mp = s3.list_multipart_uploads(Bucket=bucket)
uploads = mp.get("Uploads", [])
print(f"dang multipart: {len(uploads)}")
for u in uploads:
    s3.abort_multipart_upload(Bucket=bucket, Key=u["Key"], UploadId=u["UploadId"])
    print(f"  aborted: {u['Key']}")

after = s3.list_multipart_uploads(Bucket=bucket).get("Uploads", [])
print(f"con lai: {len(after)}")
