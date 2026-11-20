#!/usr/bin/env python3
"""Crossref metadata screening for public Smart Campus bibliography records.

This produces a review queue, not an APA 7 certificate or a claim-level source check.
No private source materials, accounts, credentials, or model providers are used.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
from difflib import SequenceMatcher
from html import unescape
import json
from pathlib import Path
import re
import time
import unicodedata
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import Request, urlopen

ROW = re.compile(r"^\|\s*(\d+)\s*\|\s*(.*?)\s*\|\s*\[Source\]\((https?://[^)]+)\)\s*\|\s*$")
DOI = re.compile(r"https?://(?:dx\.)?doi\.org/(10\.\S+)", re.I)
YEAR = re.compile(r"\((\d{4})[ab]?\)\.\s*")
LIMITATION = ("Crossref is a publisher-deposited DOI metadata crosscheck only; "
              "it does not independently validate quoted research claims, "
              "the thesis format, APA punctuation, or non-Crossref sources.")


def parse_register(text):
    entries = []
    for line in text.splitlines():
        match = ROW.match(line)
        if not match:
            continue
        number, citation, url = match.groups()
        doi_match = DOI.search(citation) or DOI.search(url)
        date = YEAR.search(citation)
        if not date:
            raise ValueError(f"reference {number}: publication year missing")
        title = citation[date.end():].split(". ", 1)[0].rstrip(". ")
        entries.append({
            "number": int(number),
            "first_author": citation.split(",", 1)[0].strip(),
            "year": int(date.group(1)),
            "title": title,
            "doi": doi_match.group(1).rstrip(".,;") if doi_match else None,
            "source_url": url,
        })
    if not entries or [x["number"] for x in entries] != list(range(1, len(entries) + 1)):
        raise ValueError("bibliography rows are empty, missing, duplicated, or out of order")
    return entries


def normalized(text):
    text = re.sub(r"<[^>]*>", "", unescape(text or ""))
    text = unicodedata.normalize("NFKD", text).lower()
    text = "".join(c for c in text if not unicodedata.combining(c))
    return re.sub(r"\s+", " ", re.sub(r"[^a-z0-9]+", " ", text)).strip()


def family_key(text):
    return normalized(text).replace(" ", "")


def published_years(message):
    years = set()
    for field in ("published", "published-print", "published-online", "issued"):
        date_parts = (message.get(field) or {}).get("date-parts") or []
        if date_parts and date_parts[0] and isinstance(date_parts[0][0], int):
            years.add(date_parts[0][0])
    return years


def compare_entry(entry, message):
    titles = message.get("title") or message.get("original-title") or []
    observed_title = titles[0] if titles else ""
    authors = message.get("author") or []
    observed_family = authors[0].get("family", "") if authors else ""
    years = published_years(message)
    similarity = round(SequenceMatcher(
        None, normalized(entry["title"]), normalized(observed_title)
    ).ratio(), 4)
    author_matches = (bool(family_key(observed_family)) and
                      family_key(entry["first_author"]) == family_key(observed_family))
    year_matches = entry["year"] in years
    plausible = author_matches and year_matches and similarity >= 0.80
    return {
        "status": "LIKELY_ALIGNED" if plausible else "REVIEW_METADATA",
        "crossref_title": observed_title,
        "crossref_first_author": observed_family,
        "crossref_publication_years": sorted(years),
        "crossref_venue": (message.get("container-title") or [""])[0],
        "crossref_volume": message.get("volume", ""),
        "crossref_issue": message.get("issue", ""),
        "crossref_page": message.get("page", ""),
        "title_similarity": similarity,
        "first_author_matches": author_matches,
        "publication_year_matches": year_matches,
    }


def crossref_lookup(doi, timeout):
    url = "https://api.crossref.org/works/" + quote(doi, safe="/")
    request = Request(url, headers={
        "Accept": "application/json",
        "User-Agent": "SmartCampusAcademicBibAudit/1.0 (noncommercial university source review)",
    })
    with urlopen(request, timeout=timeout) as response:
        result = json.load(response)
    return result["message"]


def audit(entries, timeout=12, delay=0.30, lookup=crossref_lookup):
    result = []
    last_request = False
    for entry in entries:
        receipt = dict(entry)
        if not entry["doi"]:
            receipt["status"] = "NON_DOI_SOURCE_REVIEW"
        else:
            if last_request and delay > 0:
                time.sleep(delay)
            last_request = True
            for attempt in range(2):
                try:
                    receipt.update(compare_entry(entry, lookup(entry["doi"], timeout)))
                    break
                except (HTTPError, URLError, TimeoutError, OSError, ValueError,
                        KeyError, json.JSONDecodeError) as error:
                    if attempt == 0 and isinstance(error, HTTPError) and error.code in (429, 500, 502, 503):
                        time.sleep(1)
                        continue
                    receipt.update(status="CROSSREF_NOT_VERIFIED",
                                   error=type(error).__name__,
                                   http_status=error.code if isinstance(error, HTTPError) else None)
                    break
        result.append(receipt)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", default="docs/REFERENCE_AUDIT_2026-11-20.md")
    parser.add_argument("--output", default="test-results/reference-crossref-audit.json")
    parser.add_argument("--timeout", type=float, default=12)
    parser.add_argument("--delay", type=float, default=0.30)
    args = parser.parse_args()
    entries = parse_register(Path(args.input).read_text(encoding="utf-8"))
    if len(entries) != 91:
        raise SystemExit(f"Expected 91 registered references; found {len(entries)}")
    checked = audit(entries, args.timeout, args.delay)
    summary = dict(sorted(Counter(x["status"] for x in checked).items()))
    report = {
        "kind": "bibliography_external_metadata_screen",
        "observed_utc": datetime.now(timezone.utc).isoformat(),
        "input": args.input,
        "registered_entries": len(entries),
        "crossref_doi_entries": sum(bool(x["doi"]) for x in entries),
        "notice": LIMITATION,
        "counts": summary,
        "records": checked,
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                      encoding="utf-8")
    print(json.dumps({
        "registered_entries": report["registered_entries"],
        "crossref_doi_entries": report["crossref_doi_entries"],
        "counts": summary,
        "output": str(output),
        "notice": LIMITATION,
    }, ensure_ascii=False))
    if sum(x["status"] in ("LIKELY_ALIGNED", "REVIEW_METADATA") for x in checked) == 0:
        raise SystemExit("No Crossref results were retrieved: external audit NOT VERIFIED")


if __name__ == "__main__":
    main()
