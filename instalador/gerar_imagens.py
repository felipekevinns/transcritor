"""Gera as imagens do assistente de instalacao (Inno Setup) em varios tamanhos de DPI."""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "imagens")
os.makedirs(OUT, exist_ok=True)

INK = (28, 27, 25)
PAPER = (243, 241, 236)
MUTED = (154, 149, 139)
ACCENT = (156, 193, 171)
FONTS = r"C:\Windows\Fonts"


def font(names, size):
    for n in names:
        p = os.path.join(FONTS, n)
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


SERIF = ["SitkaVF.ttf", "georgia.ttf", "cambria.ttc"]
SANS = ["segoeui.ttf", "arial.ttf"]
MONO = ["CascadiaMono.ttf", "consola.ttf"]

# Imagem lateral (paginas de boas-vindas e conclusao): 164x314 a 100%
for scale in (100, 125, 150, 175, 200, 250):
    k = scale / 100
    w, h = round(164 * k), round(314 * k)
    im = Image.new("RGB", (w, h), INK)
    d = ImageDraw.Draw(im)
    m = round(20 * k)
    # marca
    big = font(SERIF, round(64 * k))
    d.text((m - round(2 * k), round(26 * k)), "T", font=big, fill=PAPER)
    d.rectangle([m, round(104 * k), m + round(34 * k), round(106 * k)], fill=ACCENT)
    # nome e linha de apoio
    d.text((m, h - round(92 * k)), "Transcritor", font=font(SERIF, round(21 * k)), fill=PAPER)
    d.text((m, h - round(60 * k)), "Transcrição local", font=font(SANS, round(10 * k)), fill=MUTED)
    d.text((m, h - round(44 * k)), "com WhisperX", font=font(SANS, round(10 * k)), fill=MUTED)
    d.text((m, h - round(22 * k)), "v1.1.0", font=font(MONO, round(8 * k)), fill=MUTED)
    im.save(os.path.join(OUT, f"lateral-{scale}.bmp"))

# Imagem pequena (canto superior das demais paginas): 55x55 a 100%
for scale in (100, 125, 150, 175, 200, 250):
    k = scale / 100
    s = round(55 * k)
    im = Image.new("RGB", (s, s), INK)
    d = ImageDraw.Draw(im)
    f = font(SERIF, round(36 * k))
    bb = d.textbbox((0, 0), "T", font=f)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    d.text(((s - tw) / 2 - bb[0], (s - th) / 2 - bb[1] - round(3 * k)), "T", font=f, fill=PAPER)
    lw = round(16 * k)
    d.rectangle([(s - lw) // 2, s - round(12 * k), (s + lw) // 2, s - round(12 * k) + max(1, round(1.5 * k))], fill=ACCENT)
    im.save(os.path.join(OUT, f"pequena-{scale}.bmp"))

print("imagens geradas em", OUT)
