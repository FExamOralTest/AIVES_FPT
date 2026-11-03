import hashlib
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

key = sys.argv[1]
local = sys.argv[2]

obj = s3.get_object(Bucket=r2["BucketName"], Key=key)
r2_bytes = obj["Body"].read()
r2_hash = hashlib.sha256(r2_bytes).hexdigest().upper()

with open(local, "rb") as f:
    local_bytes = f.read()
local_hash = hashlib.sha256(local_bytes).hexdigest().upper()

print(f"R2    {key}")
print(f"  size   : {len(r2_bytes):,} bytes")
print(f"  sha256 : {r2_hash}")
print(f"  etag   : {obj['ETag']}")
print(f"LOCAL {local}")
print(f"  size   : {len(local_bytes):,} bytes")
print(f"  sha256 : {local_hash}")
print("-" * 70)
print("MATCH" if r2_hash == local_hash and len(r2_bytes) == len(local_bytes) else "MISMATCH!")

# Dọn multipart orphan (nếu có key cụ thể được truyền vào)
if len(sys.argv) > 3:
    orphan_key = sys.argv[3]
    mp = s3.list_multipart_uploads(Bucket=r2["BucketName"], Prefix=orphan_key)
    for u in mp.get("Uploads", []):
        s3.abort_multipart_upload(Bucket=r2["BucketName"], Key=u["Key"], UploadId=u["UploadId"])
        print(f"aborted orphan multipart: {u['Key']}")
