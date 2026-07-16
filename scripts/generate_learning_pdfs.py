from __future__ import annotations

import os
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle


OUTPUT_DIR = Path(r"D:\Projects\LearningDocs")
FONT_PATH = r"C:\Windows\Fonts\arial.ttf"


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
            fontSize=8.5,
            leading=11,
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


def bullets(styles, items: list[str]) -> list[Paragraph]:
    return [Paragraph("• " + escape(item), styles["BulletRu"]) for item in items]


def add_table(styles, rows: list[list[str]]) -> Table:
    table = Table(
        [[Paragraph(escape(str(cell)), styles["BodyRu"]) for cell in row] for row in rows],
        colWidths=[55 * mm, 110 * mm],
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


def build_pdf(file_name: str, title: str, sections: list[tuple[str, list[tuple[str, object]]]]) -> None:
    styles = build_styles()
    story = [Paragraph(escape(title), styles["TitleRu"]), Spacer(1, 4)]

    for heading, blocks in sections:
        story.append(Paragraph(escape(heading), styles["H1Ru"]))
        for kind, data in blocks:
            if kind == "p":
                story.append(paragraph(styles, str(data)))
            elif kind == "b":
                story.extend(bullets(styles, list(data)))
            elif kind == "code":
                story.append(
                    Paragraph(
                        '<font name="Arial">' + escape(str(data)).replace("\n", "<br/>") + "</font>",
                        styles["CodeRu"],
                    )
                )
            elif kind == "table":
                story.append(add_table(styles, list(data)))
                story.append(Spacer(1, 6))

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    path = OUTPUT_DIR / file_name
    SimpleDocTemplate(
        str(path),
        pagesize=A4,
        rightMargin=16 * mm,
        leftMargin=16 * mm,
        topMargin=14 * mm,
        bottomMargin=14 * mm,
    ).build(story)


def main() -> None:
    auth_sections = [
        (
            "Что сделали",
            [
                ("p", "Добавили первый защитный слой вокруг публичных auth endpoints AuthService."),
                (
                    "b",
                    [
                        "Login теперь временно блокируется после 3 неверных попыток пароля.",
                        "Публичные auth endpoints получили ограничение частоты запросов по IP.",
                        "Публичные ошибки остались одинаковыми и не раскрывают причину отказа.",
                    ],
                ),
            ],
        ),
        (
            "Какие endpoints защищены",
            [
                (
                    "b",
                    [
                        "POST /api/auth/login",
                        "POST /api/auth/refresh",
                        "POST /api/auth/request-password-reset",
                        "POST /api/auth/reset-password",
                        "POST /api/users/{userId}/resend-invite",
                    ],
                )
            ],
        ),
        (
            "Как работает временная блокировка входа",
            [
                ("p", "Это не деактивация аккаунта, а временный запрет нового login."),
                (
                    "b",
                    [
                        "AccessFailedCount увеличивается при неверном пароле.",
                        "После 3 неверных попыток выставляется LockoutEnd.",
                        "Блокировка длится 15 минут.",
                        "Даже правильный пароль во время блокировки возвращает credentials.is.invalid.",
                        "IsActive, роли, permissions и refresh tokens не меняются.",
                    ],
                ),
            ],
        ),
        (
            "Как работает ограничение частоты запросов",
            [
                (
                    "p",
                    "Лимит считается по IP-адресу, потому что часть auth flows анонимная.",
                ),
                (
                    "table",
                    [
                        ["Группа", "Лимит"],
                        ["Login", "10 запросов за 60 секунд"],
                        ["Refresh", "30 запросов за 60 секунд"],
                        ["Password reset", "3 запроса за 60 секунд"],
                        ["Invite resend", "10 запросов за 60 секунд"],
                    ],
                ),
                ("p", "При превышении лимита endpoint возвращает 429 Too Many Requests."),
            ],
        ),
        (
            "Что это дало",
            [
                (
                    "b",
                    [
                        "Снизили риск перебора паролей.",
                        "Снизили риск массовых повторных запросов к reset/invite endpoints.",
                        "Не стали раскрывать наружу, существует ли email или заблокирован ли user.",
                        "Сохранили текущую session model: неверные попытки login не отзывают refresh tokens.",
                    ],
                )
            ],
        ),
        (
            "Проверки",
            [
                (
                    "code",
                    "dotnet build AuthService.sln -> 0 warnings / 0 errors\n"
                    "dotnet test AuthService.sln -> unit 6/6, integration 100/100",
                )
            ],
        ),
    ]

    grpc_sections = [
        (
            "Что изменилось в картине gRPC flow",
            [
                (
                    "p",
                    "DirectoryService ходит в FileService через gRPC не анонимно, а с service JWT, который выдает AuthService.",
                ),
                (
                    "b",
                    [
                        "DirectoryService handler работает только с IFileCommunicationService.",
                        "FileService.Contracts adapter получает service token у AuthService.",
                        "Token добавляется в gRPC metadata как Authorization: Bearer <token>.",
                        "FileService проверяет JWT и claim service_permission=file-service.internal.",
                    ],
                ),
            ],
        ),
        (
            "Полный flow",
            [
                (
                    "code",
                    "DirectoryService handler\n"
                    "  -> IFileCommunicationService\n"
                    "  -> FileService.Contracts adapter\n"
                    "  -> AuthService /api/auth/service-token\n"
                    "  -> result.accessToken\n"
                    "  -> gRPC metadata Authorization: Bearer <service-token>\n"
                    "  -> FileService gRPC endpoint\n"
                    "  -> policy service_permission=file-service.internal\n"
                    "  -> gRPC reply\n"
                    "  -> DirectoryService response",
                )
            ],
        ),
        (
            "Почему не user JWT",
            [
                (
                    "p",
                    "User JWT отвечает на вопрос: какой пользователь делает запрос. Service JWT отвечает на вопрос: какой backend-сервис делает internal request.",
                ),
                (
                    "b",
                    [
                        "Не смешиваем files.read с internal service permission.",
                        "FileService может открыть только конкретный internal gRPC endpoint.",
                        "DirectoryService не получает доступ ко всему FileService API как пользователь.",
                    ],
                ),
            ],
        ),
        (
            "Какие проблемы решились",
            [
                (
                    "b",
                    [
                        "Internal gRPC endpoint больше не анонимный.",
                        "Граница доступа проверяется на стороне FileService.",
                        "Application layer DirectoryService не знает про token, headers или gRPC metadata.",
                        "Service token кэшируется, поэтому каждый gRPC call не ходит заново в AuthService.",
                    ],
                )
            ],
        ),
        (
            "Что важно помнить",
            [
                (
                    "b",
                    [
                        "AuthService возвращает service token внутри envelope: result.accessToken.",
                        "Если clientId/clientSecret неверные, DirectoryService не получит token.",
                        "Если token валиден, но нет service_permission, FileService вернет запрет доступа.",
                        "RabbitMQ остается для событий, gRPC используется когда нужен ответ прямо сейчас.",
                    ],
                )
            ],
        ),
    ]

    build_pdf("auth-public-hardening-summary.pdf", "AuthService: Public Auth Hardening", auth_sections)
    build_pdf("grpc-service-token-flow-summary.pdf", "gRPC Service-to-Service Flow", grpc_sections)


if __name__ == "__main__":
    main()
