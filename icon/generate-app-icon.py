"""从 icon3.png 生成透明 PNG 与多分辨率 Windows 图标。

icon3.png 是圆角卡片设计：卡片主体为亮色（白色卡片、蓝/黄绿元素），
圆角四角为纯黑背景。处理方式：以 max(R,G,B) 作为 alpha —— 黑色背景
变为全透明，圆角处抗锯齿的灰色过渡像素自动获得中间 alpha，
从而保留平滑的圆角边缘。内部无暗色元素，不受亮度 alpha 影响。
"""

from pathlib import Path

from PIL import Image, ImageChops

ICON_DIR = Path(__file__).resolve().parent
SOURCE = ICON_DIR / "icon3.png"
CANVAS_SIZE = 1024
ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)


def to_transparent(image: Image.Image) -> Image.Image:
    image = image.convert("RGB")
    r, g, b = image.split()
    alpha = ImageChops.lighter(ImageChops.lighter(r, g), b)
    return Image.merge("RGBA", (r, g, b, alpha))


def main() -> None:
    master = to_transparent(Image.open(SOURCE)).resize(
        (CANVAS_SIZE, CANVAS_SIZE), Image.Resampling.LANCZOS
    )
    master.save(ICON_DIR / "icon-transparent.png", optimize=True)
    master.save(
        ICON_DIR / "icon-transparent.ico",
        format="ICO",
        sizes=[(size, size) for size in ICON_SIZES],
        bitmap_format="png",
    )


if __name__ == "__main__":
    main()
