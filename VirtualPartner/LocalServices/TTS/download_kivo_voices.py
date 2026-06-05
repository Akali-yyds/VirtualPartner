#!/usr/bin/env python3
"""Download Kivo Wiki character voices as paired .ogg/.txt prompt files."""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any, Iterable


API_BASE = "https://api.kivo.wiki/api/v1"
USER_AGENT = "VirtualPartner-KivoVoiceDownloader/1.0"

SERVER_ALIASES = {
    "jp": "jp",
    "ja": "jp",
    "japan": "jp",
    "日服": "jp",
    "日本": "jp",
    "cn": "cn",
    "zh": "cn",
    "china": "cn",
    "国服": "cn",
    "国服官中": "cn",
    "kr": "kr",
    "ko": "kr",
    "korea": "kr",
    "韩服": "kr",
    "韓服": "kr",
}

VOICE_FIELDS = {
    "jp": "voice",
    "cn": "voice_cn",
    "kr": "voice_kr",
}

TEXT_CANDIDATES = {
    "jp": ("text_original", "text"),
    "cn": ("text", "text_original"),
    "kr": ("text", "text_original"),
}

INVALID_FILENAME_CHARS = re.compile(r'[<>:"/\\|?*\x00-\x1f]')


class DownloadError(RuntimeError):
    """Raised when a Kivo request cannot be completed."""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Download one Kivo character's selected server voices and write "
            "flat .ogg/.txt pairs for VirtualPartner TTS."
        )
    )
    parser.add_argument(
        "character_url",
        help="Kivo character URL, for example https://kivo.wiki/data/character/77",
    )
    parser.add_argument(
        "-s",
        "--server",
        default="jp",
        help="Voice server: jp/日服, cn/国服, or kr/韩服. Default: jp.",
    )
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        required=True,
        help="Output folder, for example voices/toki/default.",
    )
    parser.add_argument(
        "--categories",
        help=(
            "Comma-separated category filter, for example battle,lobby,event. "
            "Default: all categories on the voice page."
        ),
    )
    parser.add_argument(
        "--text-field",
        choices=("auto", "text", "text_original"),
        default="auto",
        help="Text field to write into .txt files. Default: auto.",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Overwrite existing .ogg/.txt files. By default existing files are kept.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the files that would be written without downloading.",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=30.0,
        help="HTTP timeout in seconds. Default: 30.",
    )
    parser.add_argument(
        "--retries",
        type=int,
        default=3,
        help="Download retry count per request. Default: 3.",
    )
    return parser.parse_args()


def normalize_server(value: str) -> str:
    server = SERVER_ALIASES.get(value.strip().lower())
    if not server:
        allowed = ", ".join(sorted(SERVER_ALIASES))
        raise DownloadError(f"Unsupported server '{value}'. Allowed aliases: {allowed}")
    return server


def extract_character_id(character_url: str) -> str:
    value = character_url.strip()
    if value.isdigit():
        return value

    parsed = urllib.parse.urlparse(value)
    match = re.search(r"/data/character/(\d+)", parsed.path)
    if match:
        return match.group(1)

    raise DownloadError(
        "Cannot find character id. Expected a URL like "
        "https://kivo.wiki/data/character/77"
    )


def request_json(url: str, timeout: float, retries: int) -> dict[str, Any]:
    raw = request_bytes(url, timeout=timeout, retries=retries)
    try:
        return json.loads(raw.decode("utf-8"))
    except json.JSONDecodeError as exc:
        raise DownloadError(f"Invalid JSON from {url}: {exc}") from exc


def request_bytes(url: str, timeout: float, retries: int) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    last_error: Exception | None = None
    attempts = max(1, retries)

    for attempt in range(1, attempts + 1):
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                return response.read()
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            last_error = exc
            if attempt < attempts:
                time.sleep(min(2.0, 0.5 * attempt))

    raise DownloadError(f"Request failed after {attempts} attempts: {url}: {last_error}")


def fetch_student(character_id: str, timeout: float, retries: int) -> dict[str, Any]:
    payload = request_json(
        f"{API_BASE}/data/students/{character_id}", timeout=timeout, retries=retries
    )
    if not payload.get("success"):
        message = payload.get("message") or payload.get("codename") or "unknown error"
        raise DownloadError(f"Kivo API returned an error: {message}")

    data = payload.get("data")
    if not isinstance(data, dict):
        raise DownloadError("Kivo API response does not contain a student data object.")
    return data


def normalize_file_url(file_value: str, timeout: float, retries: int) -> str:
    if not file_value:
        raise DownloadError("Voice entry has an empty file URL.")

    if file_value.startswith("//"):
        return "https:" + file_value

    parsed = urllib.parse.urlparse(file_value)
    if parsed.scheme:
        return file_value

    host = fetch_static_host(timeout=timeout, retries=retries)
    return urllib.parse.urljoin(f"https://{host}/", file_value.lstrip("/"))


def fetch_static_host(timeout: float, retries: int) -> str:
    payload = request_json(
        f"{API_BASE}/upload/file_server", timeout=timeout, retries=retries
    )
    host = (payload.get("data") or {}).get("server_host")
    if not host:
        raise DownloadError("Kivo file_server response did not include server_host.")
    return str(host)


def select_entries(
    student: dict[str, Any], server: str, categories: set[str] | None
) -> list[dict[str, Any]]:
    field = VOICE_FIELDS[server]
    entries = student.get(field) or []
    if not isinstance(entries, list):
        raise DownloadError(f"Kivo field '{field}' is not a list.")

    selected: list[dict[str, Any]] = []
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        if categories and str(entry.get("category") or "") not in categories:
            continue
        selected.append(entry)
    return selected


def parse_categories(value: str | None) -> set[str] | None:
    if not value:
        return None
    categories = {part.strip() for part in value.split(",") if part.strip()}
    return categories or None


def choose_text(entry: dict[str, Any], server: str, text_field: str) -> str:
    if text_field != "auto":
        return str(entry.get(text_field) or "")

    for candidate in TEXT_CANDIDATES[server]:
        text = entry.get(candidate)
        if text:
            return str(text)
    return ""


def safe_base_name(entry: dict[str, Any], file_url: str, used: set[str]) -> str:
    description = str(entry.get("description") or "").strip()
    if description:
        raw_name = Path(description).stem if description.lower().endswith(".ogg") else description
    else:
        raw_name = Path(urllib.parse.urlparse(file_url).path).stem or "voice"

    base = INVALID_FILENAME_CHARS.sub("_", raw_name).strip(" .")
    if not base:
        base = "voice"

    candidate = base
    suffix = 2
    while candidate.lower() in used:
        candidate = f"{base}_{suffix}"
        suffix += 1

    used.add(candidate.lower())
    return candidate


def write_bytes(path: Path, data: bytes, overwrite: bool) -> bool:
    if path.exists() and not overwrite:
        return False
    path.write_bytes(data)
    return True


def write_text(path: Path, text: str, overwrite: bool) -> bool:
    if path.exists() and not overwrite:
        return False
    path.write_text(text, encoding="utf-8", newline="\n")
    return True


def summarize_categories(entries: Iterable[dict[str, Any]]) -> dict[str, int]:
    counts: dict[str, int] = {}
    for entry in entries:
        category = str(entry.get("category") or "unknown")
        counts[category] = counts.get(category, 0) + 1
    return dict(sorted(counts.items()))


def download_entries(
    entries: list[dict[str, Any]],
    server: str,
    output_dir: Path,
    text_field: str,
    overwrite: bool,
    dry_run: bool,
    timeout: float,
    retries: int,
) -> tuple[int, int, int]:
    if not dry_run:
        output_dir.mkdir(parents=True, exist_ok=True)

    used_names: set[str] = set()
    written_audio = 0
    written_text = 0
    skipped_existing = 0

    for index, entry in enumerate(entries, start=1):
        file_url = normalize_file_url(str(entry.get("file") or ""), timeout, retries)
        base_name = safe_base_name(entry, file_url, used_names)
        audio_path = output_dir / f"{base_name}.ogg"
        text_path = output_dir / f"{base_name}.txt"
        text = choose_text(entry, server=server, text_field=text_field)

        category = entry.get("category") or "unknown"
        if dry_run:
            print(f"[dry-run] {index:03d} [{category}] {audio_path.name}", flush=True)
            continue

        audio_written = False
        if audio_path.exists() and not overwrite:
            skipped_existing += 1
        else:
            audio_data = request_bytes(file_url, timeout=timeout, retries=retries)
            audio_written = write_bytes(audio_path, audio_data, overwrite=overwrite)

        text_written = write_text(text_path, text, overwrite=overwrite)

        if audio_written:
            written_audio += 1
        if text_written:
            written_text += 1
        if not text_written and text_path.exists() and not overwrite:
            skipped_existing += 1

        print(f"[ok] {index:03d}/{len(entries):03d} [{category}] {base_name}", flush=True)

    return written_audio, written_text, skipped_existing


def main() -> int:
    try:
        args = parse_args()
        server = normalize_server(args.server)
        character_id = extract_character_id(args.character_url)
        categories = parse_categories(args.categories)

        student = fetch_student(
            character_id=character_id, timeout=args.timeout, retries=args.retries
        )
        entries = select_entries(student, server=server, categories=categories)
        if not entries:
            raise DownloadError("No voice entries matched the selected server/categories.")

        print(f"Character: {character_id}", flush=True)
        print(f"Server: {server} ({VOICE_FIELDS[server]})", flush=True)
        print(f"Output: {args.output.resolve()}", flush=True)
        print(f"Entries: {len(entries)}", flush=True)
        print(f"Categories: {summarize_categories(entries)}", flush=True)

        written_audio, written_text, skipped_existing = download_entries(
            entries=entries,
            server=server,
            output_dir=args.output,
            text_field=args.text_field,
            overwrite=args.overwrite,
            dry_run=args.dry_run,
            timeout=args.timeout,
            retries=args.retries,
        )

        if args.dry_run:
            print("Dry run complete.", flush=True)
        else:
            print(
                "Done. "
                f"audio_written={written_audio}, "
                f"text_written={written_text}, "
                f"skipped_existing={skipped_existing}",
                flush=True,
            )
        return 0
    except DownloadError as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("Interrupted.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
