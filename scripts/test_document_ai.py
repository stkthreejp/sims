import argparse
import base64
import json
import time
from pathlib import Path

import requests
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding


TOKEN_URL = "https://oauth2.googleapis.com/token"
SCOPE = "https://www.googleapis.com/auth/cloud-platform"


def main() -> None:
    parser = argparse.ArgumentParser(description="Run local PDFs through Google Document AI and write sanitized summaries.")
    parser.add_argument("--env", default=".env", help="Path to the local env file.")
    parser.add_argument("--input-dir", required=True, help="Directory containing PDFs to process.")
    parser.add_argument("--output-dir", required=True, help="Directory for JSON summaries.")
    args = parser.parse_args()

    env = load_env(Path(args.env))
    credentials_json = env.get("DOCUMENTAI_CREDENTIALS_JSON") or env.get("GOOGLE_APPLICATION_CREDENTIALS_JSON")
    if not credentials_json:
        raise RuntimeError("DOCUMENTAI_CREDENTIALS_JSON is not configured.")
    credentials = json.loads(credentials_json)
    project_id = required(env, "DOCUMENTAI_PROJECT_ID")
    location = required(env, "DOCUMENTAI_LOCATION")
    processor_id = required(env, "DOCUMENTAI_PROCESSOR_ID")

    access_token = get_access_token(credentials)
    input_dir = Path(args.input_dir)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    for pdf_path in sorted(input_dir.glob("*.pdf*")):
        summary = process_pdf(pdf_path, project_id, location, processor_id, access_token)
        output_path = output_dir / f"{pdf_path.name}.documentai.summary.json"
        output_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
        print(f"{pdf_path.name}: {summary['field_count']} fields, {summary['entity_count']} entities, {summary['page_count']} pages")


def load_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        values[key.strip()] = value.strip().strip('"').strip("'")
    return values


def required(values: dict[str, str], key: str) -> str:
    value = values.get(key)
    if not value:
        raise RuntimeError(f"{key} is not configured.")
    return value


def get_access_token(credentials: dict[str, str]) -> str:
    now = int(time.time())
    header = {"alg": "RS256", "typ": "JWT"}
    claims = {
        "iss": credentials["client_email"],
        "scope": SCOPE,
        "aud": TOKEN_URL,
        "iat": now,
        "exp": now + 3600,
    }
    unsigned = f"{b64_json(header)}.{b64_json(claims)}".encode("ascii")
    private_key = serialization.load_pem_private_key(credentials["private_key"].encode("utf-8"), password=None)
    signature = private_key.sign(unsigned, padding.PKCS1v15(), hashes.SHA256())
    assertion = unsigned.decode("ascii") + "." + b64(signature)

    response = requests.post(
        TOKEN_URL,
        data={
            "grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
            "assertion": assertion,
        },
        timeout=30,
    )
    response.raise_for_status()
    return response.json()["access_token"]


def process_pdf(pdf_path: Path, project_id: str, location: str, processor_id: str, access_token: str) -> dict:
    endpoint = (
        f"https://{location}-documentai.googleapis.com/v1/"
        f"projects/{project_id}/locations/{location}/processors/{processor_id}:process"
    )
    payload = {
        "rawDocument": {
            "content": base64.b64encode(pdf_path.read_bytes()).decode("ascii"),
            "mimeType": "application/pdf",
        }
    }
    response = requests.post(
        endpoint,
        headers={"Authorization": f"Bearer {access_token}", "Content-Type": "application/json"},
        json=payload,
        timeout=120,
    )
    response.raise_for_status()
    document = response.json().get("document", {})
    text = document.get("text", "")
    pages = document.get("pages", [])
    entities = document.get("entities", [])
    fields = extract_form_fields(text, pages)

    return {
        "file_name": pdf_path.name,
        "page_count": len(pages),
        "text_length": len(text),
        "field_count": len(fields),
        "entity_count": len(entities),
        "sample_fields": fields[:40],
        "sample_entities": [
            {
                "type": entity.get("type", ""),
                "mention_text": truncate(entity.get("mentionText", "")),
                "confidence": entity.get("confidence", 0),
                "page_anchor": entity.get("pageAnchor", {}),
            }
            for entity in entities[:20]
        ],
    }


def extract_form_fields(text: str, pages: list[dict]) -> list[dict]:
    fields = []
    for page_index, page in enumerate(pages, start=1):
        for field in page.get("formFields", []):
            name = anchor_text(text, field.get("fieldName", {}).get("textAnchor", {}))
            value = anchor_text(text, field.get("fieldValue", {}).get("textAnchor", {}))
            if not name and not value:
                continue
            fields.append(
                {
                    "name": truncate(name),
                    "value": truncate(value),
                    "confidence": field.get("fieldValue", {}).get("confidence")
                    or field.get("fieldName", {}).get("confidence")
                    or 0,
                    "page": page_index,
                }
            )
    return fields


def anchor_text(text: str, anchor: dict) -> str:
    pieces = []
    for segment in anchor.get("textSegments", []):
        start = int(segment.get("startIndex", 0))
        end = int(segment.get("endIndex", 0))
        if end > start:
            pieces.append(text[start:end])
    return " ".join(pieces).replace("\n", " ").strip()


def truncate(value: str, limit: int = 160) -> str:
    return value if len(value) <= limit else value[: limit - 3] + "..."


def b64_json(value: dict) -> str:
    return b64(json.dumps(value, separators=(",", ":")).encode("utf-8"))


def b64(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).decode("ascii").rstrip("=")


if __name__ == "__main__":
    main()
