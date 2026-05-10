from __future__ import annotations

import csv
import json
import re
from html.parser import HTMLParser
from pathlib import Path


SOURCE = Path(
    r"C:\ODSMM-OneDrive\SMM\OneDrive - ODonovan Insurance\Documents\oden_chart_builder_body.phtml.html"
)
ROOT = Path(__file__).resolve().parents[1]
SEED_OUT = ROOT / "backend" / "src" / "SIMS.API" / "Data" / "Seeds" / "oden-commercial-cancellation.json"
DOC_OUT = ROOT / "docs" / "commercial-cancellation-law-tracking-chart.md"
CSV_OUT = ROOT / "temp" / "oden-commercial-cancellation-sections.csv"

STATE_NAMES = {
    "Alabama",
    "Arkansas",
    "Florida",
    "Georgia",
    "Louisiana",
    "Maryland",
    "Mississippi",
    "North Carolina",
    "Oklahoma",
    "Pennsylvania",
    "South Carolina",
    "Tennessee",
    "Texas",
    "Virginia",
}

CATEGORY_HEADINGS = {
    "DEFINITIONS",
    "INSURER REQUIREMENTS",
    "NOTICE REQUIREMENTS",
    "REASONS",
    "REGULATION OF POLICY TYPES",
    "SPECIFIC POLICY TYPE OR COVERAGE REQUIREMENTS",
}

TOPIC_NAMES = [
    "Additional Information",
    "Liability Immunity",
    "Penalty for Noncompliance",
    "Return of Unearned Premium",
    "Notification to Mortgagee or Lienholder",
    "Notification to State Authority",
    "Proof of Notice",
    "Time Period",
    "Acceptable Reasons",
    "General Requirements",
    "Prohibited Reasons",
    "Exempt Policy Types",
    "Policy Types Regulated by Insurance Code",
    "Policy Types Regulated by Other Codes or Plans",
    "Automobile - For Hire",
    "Automobile",
    "Motor Carrier",
    "Surplus Lines",
    "Business Entity",
    "Commercial Building",
    "Domestic Violence",
    "Living Unit",
    "Mine Subsidence",
    "Miscellaneous Casualty Insurance",
    "Personal Injury Liability Insurance",
    "Property Damage Liability Insurance",
    "Residence",
]


class TableParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.in_table = False
        self.in_cell = False
        self.current_row: list[str] = []
        self.current_cell: list[str] = []
        self.rows: list[list[str]] = []

    def handle_starttag(self, tag: str, attrs) -> None:
        if tag == "table":
            self.in_table = True
        elif self.in_table and tag == "tr":
            self.current_row = []
        elif self.in_table and tag in {"td", "th"}:
            self.in_cell = True
            self.current_cell = []

    def handle_endtag(self, tag: str) -> None:
        if self.in_table and tag in {"td", "th"}:
            self.current_row.append(normalize_text("".join(self.current_cell)))
            self.in_cell = False
        elif self.in_table and tag == "tr":
            if self.current_row:
                self.rows.append(self.current_row)
        elif tag == "table":
            self.in_table = False

    def handle_data(self, data: str) -> None:
        if self.in_cell:
            self.current_cell.append(data)


def normalize_text(value: str) -> str:
    return " ".join(value.replace("\ufffd", "").split())


def split_heading(text: str) -> tuple[str, str]:
    for category in sorted(CATEGORY_HEADINGS, key=len, reverse=True):
        if text == category:
            return category, ""
        prefix = f"{category} : "
        if text.startswith(prefix):
            rest = text[len(prefix) :]
            for topic in TOPIC_NAMES:
                if rest == topic or rest.startswith(f"{topic} "):
                    return category, topic
            return category, rest.strip()
        if text.startswith(f"{category} "):
            return category, category.title()
    return "OTHER", ""


def extract_citations(text: str) -> list[str]:
    seen: set[str] = set()
    citations: list[str] = []
    for citation in re.findall(r"\[([^\]]+)\]", text):
        clean = normalize_text(citation)
        if clean and clean not in seen:
            seen.add(clean)
            citations.append(clean)
    return citations


def extract_sections(rows: list[list[str]]) -> list[dict]:
    sections: list[dict] = []
    current_state = ""
    sort_order = 0

    for row in rows:
        text = row[0] if row else ""
        if not text or text.startswith("COMMERCIAL INSURANCE") or text.startswith("IMPORTANT NOTE") or text.startswith("TABLE OF CONTENTS"):
            continue
        if text in STATE_NAMES:
            current_state = text
            sort_order = 0
            continue
        if not current_state:
            continue

        category, topic = split_heading(text)
        if text in CATEGORY_HEADINGS:
            continue

        heading = f"{category} : {topic}" if topic else category
        body = text
        if heading != "OTHER" and body.startswith(heading):
            body = normalize_text(body[len(heading) :])
        elif topic and body.startswith(f"{category} : {topic}"):
            body = normalize_text(body[len(f"{category} : {topic}") :])
        elif topic and body.startswith(f"{category} {topic}"):
            body = normalize_text(body[len(f"{category} {topic}") :])
        elif category != "OTHER" and body.startswith(category):
            body = normalize_text(body[len(category) :])

        sort_order += 1
        sections.append(
            {
                "state": current_state,
                "lineOfBusiness": "Commercial P&C",
                "action": "Cancellation",
                "category": category,
                "topic": topic or category.title(),
                "requirementText": body,
                "citations": extract_citations(body),
                "sourceName": "Oden Online",
                "sourceDocument": "COMMERCIAL INSURANCE - CANCELLATION - P&C",
                "sourceCreatedAt": "2026-05-10T21:26:33Z",
                "reviewStatus": "Seeded",
                "sortOrder": sort_order,
            }
        )
    return sections


def write_markdown(sections: list[dict]) -> None:
    DOC_OUT.parent.mkdir(parents=True, exist_ok=True)
    by_state: dict[str, list[dict]] = {}
    for section in sections:
        by_state.setdefault(section["state"], []).append(section)

    categories = sorted({section["category"] for section in sections})
    states = sorted(by_state)

    lines = [
        "# Commercial Cancellation Law Tracking Chart",
        "",
        "Source of truth: Oden Online export, `COMMERCIAL INSURANCE - CANCELLATION - P&C`, created 2026-05-10 09:26:33 PM.",
        "",
        "This is the initial tracking map for SIMS. Each row below should exist as a reviewable requirement section in the system, with citations preserved and future source scans compared against the stored text.",
        "",
        "## Law Areas To Track",
        "",
        "| Area | What SIMS should track |",
        "| --- | --- |",
        "| Definitions | State-specific terms that affect whether cancellation rules apply. |",
        "| Insurer requirements | Additional duties, liability immunity, penalties, and unearned premium handling. |",
        "| Notice requirements | Notice period, proof of notice, mortgagee/lienholder notice, and state authority notice. |",
        "| Reasons | Acceptable reasons, prohibited reasons, and general reason-statement rules. |",
        "| Regulation of policy types | Commercial policy types included, excluded, or governed by other codes/plans. |",
        "| Specific policy type or coverage requirements | Auto, motor carrier, surplus lines, and other line-specific rules. |",
        "",
        "## State Coverage",
        "",
        "| State | Sections | Categories Present | High-priority tracking topics |",
        "| --- | ---: | --- | --- |",
    ]

    for state in states:
        state_sections = by_state[state]
        present = sorted({s["category"] for s in state_sections})
        high_priority = sorted({s["topic"] for s in state_sections if s["category"] in {"NOTICE REQUIREMENTS", "REASONS", "SPECIFIC POLICY TYPE OR COVERAGE REQUIREMENTS"}})
        lines.append(
            f"| {state} | {len(state_sections)} | {', '.join(present)} | {', '.join(high_priority)} |"
        )

    lines.extend(["", "## Detailed Tracking Rows", ""])
    for state in states:
        lines.extend([f"### {state}", "", "| Category | Topic | Citations | Requirement Snapshot |", "| --- | --- | --- | --- |"])
        for section in by_state[state]:
            citations = ", ".join(section["citations"]) if section["citations"] else "None found"
            snapshot = section["requirementText"][:220].replace("|", "\\|")
            if len(section["requirementText"]) > 220:
                snapshot += "..."
            lines.append(f"| {section['category']} | {section['topic']} | {citations} | {snapshot} |")
        lines.append("")

    DOC_OUT.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    text = SOURCE.read_text(encoding="utf-8", errors="ignore")
    parser = TableParser()
    parser.feed(text)
    sections = extract_sections(parser.rows)

    SEED_OUT.parent.mkdir(parents=True, exist_ok=True)
    SEED_OUT.write_text(json.dumps(sections, indent=2), encoding="utf-8")

    CSV_OUT.parent.mkdir(parents=True, exist_ok=True)
    with CSV_OUT.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=list(sections[0].keys()))
        writer.writeheader()
        writer.writerows(sections)

    write_markdown(sections)
    print(f"Wrote {len(sections)} sections")
    print(SEED_OUT)
    print(DOC_OUT)
    print(CSV_OUT)


if __name__ == "__main__":
    main()
