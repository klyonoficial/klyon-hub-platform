import pymysql
import boto3
from botocore.config import Config
from botocore.exceptions import ClientError

FILE_PATH = "oracle://klyon-hub-bucket/contracts/contract_CT-2026-000007_20260820001924.pdf"

conn = pymysql.connect(
    host="168.75.93.94",
    port=3306,
    user="gestorhub",
    password="F@z3rd@rC3rt0",
    database="klyon_hub",
    charset="utf8mb4",
)
cur = conn.cursor()
cur.execute(
    "SELECT `Key`, Value FROM SystemConfigurations WHERE `Key` LIKE 'Oracle.OS.%' ORDER BY `Key`"
)
settings = {k: v for k, v in cur.fetchall()}
cur.close()
conn.close()

namespace = settings.get("Oracle.OS.Namespace")
region = settings.get("Oracle.OS.Region", "sa-saopaulo-1")
bucket = settings.get("Oracle.OS.BucketName")
access_key = settings.get("Oracle.OS.AccessKeyId")
secret_key = settings.get("Oracle.OS.SecretAccessKey")

if not all([namespace, bucket, access_key, secret_key]):
    raise SystemExit(f"Missing Oracle config: namespace={namespace}, bucket={bucket}")

key = FILE_PATH.replace("oracle://", "").split("/", 1)[1]
endpoint = f"https://{namespace.strip()}.compat.objectstorage.{region.strip()}.oci.customer-oci.com"

s3 = boto3.client(
    "s3",
    endpoint_url=endpoint,
    aws_access_key_id=access_key,
    aws_secret_access_key=secret_key,
    region_name=region.strip(),
    config=Config(signature_version="s3v4", s3={"addressing_style": "path"}),
)

print(f"Bucket: {bucket}")
print(f"Key: {key}")
print(f"Endpoint: {endpoint}")

try:
    s3.head_object(Bucket=bucket, Key=key)
    print("File exists before delete: yes")
except ClientError as e:
    code = e.response.get("Error", {}).get("Code")
    print(f"File exists before delete: no ({code})")

try:
    s3.delete_object(Bucket=bucket, Key=key)
    print("DeleteObject: OK")
except ClientError as e:
    print(f"DeleteObject failed: {e}")
    raise

try:
    s3.head_object(Bucket=bucket, Key=key)
    print("File exists after delete: yes (unexpected)")
except ClientError as e:
    code = e.response.get("Error", {}).get("Code")
    print(f"File exists after delete: no ({code})")
