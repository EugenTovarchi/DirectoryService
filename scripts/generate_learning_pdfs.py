from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = Path(r"D:\Projects\LearningDocs")
FONT_PATH = r"C:\Windows\Fonts\arial.ttf"

DOCUMENTS = [
    (
        REPO_ROOT / "backend/docs/services/auth-public-hardening-summary.md",
        "auth-public-hardening-summary.pdf",
    ),
    (
        REPO_ROOT / "backend/docs/architecture/grpc-service-token-flow-summary.md",
        "grpc-service-token-flow-summary.pdf",
    ),
]


def escape(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def build_styles():
    pdfmetrics.registerFont(TTFont("Arial", FONT_PATH))
    styles = getSampleStyleSheet()
    styles.add(
        ParagraphStyle(
            name="TitleRu",
            parent=styles["Title"],
            fontName="Arial",
            fontSize=20,
            leading=24,
            alignment=TA_CENTER,
            spaceAfter=12,
        )
    )
    styles.add(
        ParagraphStyle(
            name="H1Ru",
            parent=styles["Heading1"],
            fontName="Arial",
            fontSize=15,
            leading=18,
            spaceBefore=10,
            spaceAfter=6,
        )
    )
    styles.add(
        ParagraphStyle(
            name="H2Ru",
            parent=styles["Heading2"],
            fontName="Arial",
            fontSize=12,
            leading=15,
            spaceBefore=8,
            spaceAfter=4,
        )
    )
    styles.add(
        ParagraphStyle(
            name="BodyRu",
            parent=styles["BodyText"],
            fontName="Arial",
            fontSize=9.5,
            leading=13,
            spaceAfter=5,
        )
    )
    styles.add(
        ParagraphStyle(
            name="BulletRu",
            parent=styles["BodyText"],
            fontName="Arial",
            fontSize=9.5,
            leading=13,
            leftIndent=12,
            firstLineIndent=-8,
            spaceAfter=3,
        )
    )
    styles.add(
        ParagraphStyle(
            name="CodeRu",
            parent=styles["Code"],
            fontName="Arial",
            fontSize=8.2,
            leading=10.5,
            backColor=colors.whitesmoke,
            borderColor=colors.lightgrey,
            borderWidth=0.5,
            borderPadding=4,
            spaceAfter=6,
        )
    )
    return styles


def paragraph(styles, text: str, style: str = "BodyRu") -> Paragraph:
    return Paragraph(escape(text), styles[style])


def code_block(styles, lines: list[str]) -> Paragraph:
    text = "\n".join(lines)
    return Paragraph(
        '<font name="Arial">' + escape(text).replace("\n", "<br/>") + "</font>",
        styles["CodeRu"],
    )


def markdown_table(styles, lines: list[str]) -> Table:
    rows: list[list[str]] = []
    for line in lines:
        stripped = line.strip()
        if not stripped.startswith("|") or "---" in stripped:
            continue
        cells = [cell.strip() for cell in stripped.strip("|").split("|")]
        rows.append(cells)

    column_count = max(len(row) for row in rows)
    width = 170 * mm / column_count
    table = Table(
        [[Paragraph(escape(cell), styles["BodyRu"]) for cell in row] for row in rows],
        colWidths=[width] * column_count,
    )
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.lightgrey),
                ("GRID", (0, 0), (-1, -1), 0.25, colors.grey),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("FONTNAME", (0, 0), (-1, -1), "Arial"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return table


def flush_paragraph(story, styles, lines: list[str]) -> None:
    if not lines:
        return

    story.append(paragraph(styles, " ".join(line.strip() for line in lines if line.strip())))
    lines.clear()


def parse_markdown(source: Path, styles) -> list:
    story: list = []
    paragraph_lines: list[str] = []
    code_lines: list[str] = []
    table_lines: list[str] = []
    in_code = False

    for raw_line in source.read_text(encoding="utf-8").splitlines():
        line = raw_line.rstrip()

        if line.startswith("```"):
            flush_paragraph(story, styles, paragraph_lines)
            if table_lines:
                story.append(markdown_table(styles, table_lines))
                story.append(Spacer(1, 6))
                table_lines.clear()
            if in_code:
                story.append(code_block(styles, code_lines))
                code_lines.clear()
                in_code = False
            else:
                in_code = True
            continue

        if in_code:
            code_lines.append(line)
            continue

        if line.startswith("|"):
            flush_paragraph(story, styles, paragraph_lines)
            table_lines.append(line)
            continue

        if table_lines:
            story.append(markdown_table(styles, table_lines))
            story.append(Spacer(1, 6))
            table_lines.clear()

        if not line.strip():
            flush_paragraph(story, styles, paragraph_lines)
            continue

        if line.startswith("# "):
            flush_paragraph(story, styles, paragraph_lines)
            story.append(Paragraph(escape(line[2:].strip()), styles["TitleRu"]))
            story.append(Spacer(1, 4))
            continue

        if line.startswith("## "):
            flush_paragraph(story, styles, paragraph_lines)
            story.append(Paragraph(escape(line[3:].strip()), styles["H1Ru"]))
            continue

        if line.startswith("### "):
            flush_paragraph(story, styles, paragraph_lines)
            story.append(Paragraph(escape(line[4:].strip()), styles["H2Ru"]))
            continue

        if line.startswith("- "):
            flush_paragraph(story, styles, paragraph_lines)
            story.append(Paragraph("• " + escape(line[2:].strip()), styles["BulletRu"]))
            continue

        if line[0].isdigit() and ". " in line[:4]:
            flush_paragraph(story, styles, paragraph_lines)
            story.append(Paragraph(escape(line.strip()), styles["BulletRu"]))
            continue

        paragraph_lines.append(line)

    flush_paragraph(story, styles, paragraph_lines)
    if table_lines:
        story.append(markdown_table(styles, table_lines))
        story.append(Spacer(1, 6))
    if code_lines:
        story.append(code_block(styles, code_lines))

    return story


def build_pdf(source: Path, output_name: str) -> None:
    styles = build_styles()
    story = parse_markdown(source, styles)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    SimpleDocTemplate(
        str(OUTPUT_DIR / output_name),
        pagesize=A4,
        rightMargin=16 * mm,
        leftMargin=16 * mm,
        topMargin=14 * mm,
        bottomMargin=14 * mm,
    ).build(story)


def main() -> None:
    for source, output_name in DOCUMENTS:
        build_pdf(source, output_name)


if __name__ == "__main__":
    main()
