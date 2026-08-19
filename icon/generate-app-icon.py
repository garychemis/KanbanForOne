"""Generate the Kanban41 PNG and multi-resolution Windows icon.

The geometry mirrors kanban41.svg so the checked-in SVG remains the editable
design source while the generated files stay compatible with WPF and Win32.
"""

from pathlib import Path

from PIL import Image, ImageDraw


CANVAS_SIZE = 1024
NAVY = "#172730"
PAPER = "#F7F6F1"
TEAL = "#5C9187"
ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def create_master() -> Image.Image:
    image = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle((40, 40, 984, 984), radius=224, fill=NAVY)
    draw.rounded_rectangle((152, 184, 336, 744), radius=48, fill=PAPER)
    draw.rounded_rectangle((420, 184, 604, 452), radius=48, fill=PAPER)
    draw.rounded_rectangle((420, 492, 604, 744), radius=48, fill=PAPER)
    draw.rounded_rectangle((688, 184, 872, 364), radius=48, fill=PAPER)
    draw.rounded_rectangle((688, 404, 872, 744), radius=48, fill=TEAL)
    draw.line(
        ((729, 574), (764, 610), (832, 533)),
        fill=PAPER,
        width=46,
        joint="curve",
    )

    # Pillow's line joints do not round the two exposed endpoints, so cap them.
    cap_radius = 23
    for x, y in ((729, 574), (832, 533)):
        draw.ellipse(
            (x - cap_radius, y - cap_radius, x + cap_radius, y + cap_radius),
            fill=PAPER,
        )

    return image


def main() -> None:
    icon_dir = Path(__file__).resolve().parent
    master = create_master()
    master.save(icon_dir / "icon-transparent.png", optimize=True)
    master.save(
        icon_dir / "icon-transparent.ico",
        format="ICO",
        sizes=[(size, size) for size in ICON_SIZES],
        bitmap_format="png",
    )


if __name__ == "__main__":
    main()
