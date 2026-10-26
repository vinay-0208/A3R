import os
from PIL import Image, ImageDraw

OUTPUT_DIR = r"C:\Fall Semister\2026\AR&VR\project\ARVR\unity_project\Assets\UI\Sprites"
os.makedirs(OUTPUT_DIR, exist_ok=True)

def create_rounded_rect_texture(filename, size=(128, 128), radius=20, bg_color=(11, 17, 32, 235), border_color=(56, 189, 248, 70), border_width=2, scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    sradius = radius * scale
    sbw = border_width * scale

    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    if border_width > 0 and border_color[3] > 0:
        draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sradius, fill=border_color)
        draw.rounded_rectangle([sbw, sbw, sw - 1 - sbw, sh - 1 - sbw], radius=max(1, sradius - sbw), fill=bg_color)
    else:
        draw.rounded_rectangle([0, 0, sw - 1, sh - 1], radius=sradius, fill=bg_color)

    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_pill_texture(filename, size=(128, 48), bg_color=(15, 23, 42, 220), border_color=(56, 189, 248, 100), border_width=2, scale=4):
    radius = size[1] // 2
    create_rounded_rect_texture(filename, size=size, radius=radius, bg_color=bg_color, border_color=border_color, border_width=border_width, scale=scale)

def create_circle_texture(filename, size=(64, 64), fill_color=(255, 255, 255, 255), scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([0, 0, sw - 1, sh - 1], fill=fill_color)
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_circle_check_texture(filename="ui_circle_check.png", size=(96, 96), scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([0, 0, sw - 1, sh - 1], fill=(16, 185, 129, 70))
    pad = 5 * scale
    draw.ellipse([pad, pad, sw - 1 - pad, sh - 1 - pad], fill=(6, 78, 59, 240), outline=(52, 211, 153, 240), width=3 * scale)
    cx, cy = sw // 2, sh // 2
    p1 = (cx - 15 * scale, cy + 2 * scale)
    p2 = (cx - 4 * scale, cy + 13 * scale)
    p3 = (cx + 17 * scale, cy - 10 * scale)
    draw.line([p1, p2, p3], fill=(255, 255, 255, 255), width=5 * scale, joint="curve")
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_camera_icon(filename="ui_icon_camera.png", size=(64, 64), scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    color = (56, 189, 248, 255)
    bw, bh = 36 * scale, 24 * scale
    bx = (sw - bw) // 2
    by = (sh - bh) // 2 + 2 * scale
    draw.rounded_rectangle([bx, by, bx + bw, by + bh], radius=4 * scale, outline=color, width=3 * scale)
    tw, th = 14 * scale, 5 * scale
    tx = (sw - tw) // 2
    ty = by - th + 1 * scale
    draw.rounded_rectangle([tx, ty, tx + tw, ty + th], radius=2 * scale, fill=color)
    cx, cy = sw // 2, by + bh // 2
    r = 7 * scale
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=color, width=3 * scale)
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_arcore_icon(filename="ui_icon_arcore.png", size=(64, 64), scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    color = (56, 189, 248, 255)
    wth = 3 * scale
    l = 10 * scale
    m = 12 * scale
    draw.line([(m, m), (m + l, m)], fill=color, width=wth)
    draw.line([(m, m), (m, m + l)], fill=color, width=wth)
    draw.line([(sw - m, m), (sw - m - l, m)], fill=color, width=wth)
    draw.line([(sw - m, m), (sw - m, m + l)], fill=color, width=wth)
    draw.line([(m, sh - m), (m + l, sh - m)], fill=color, width=wth)
    draw.line([(m, sh - m), (m, sh - m - l)], fill=color, width=wth)
    draw.line([(sw - m, sh - m), (sw - m - l, sh - m)], fill=color, width=wth)
    draw.line([(sw - m, sh - m), (sw - m, sh - m - l)], fill=color, width=wth)
    cx, cy = sw // 2, sh // 2
    r = 3 * scale
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_ai_icon(filename="ui_icon_ai.png", size=(64, 64), scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    color = (56, 189, 248, 255)
    wth = 2 * scale
    cx, cy = sw // 2, sh // 2
    nodes = [
        (cx - 12 * scale, cy - 8 * scale),
        (cx + 12 * scale, cy - 8 * scale),
        (cx, cy - 14 * scale),
        (cx - 14 * scale, cy + 6 * scale),
        (cx + 14 * scale, cy + 6 * scale),
        (cx, cy + 12 * scale),
        (cx, cy)
    ]
    edges = [
        (0, 2), (1, 2), (0, 6), (1, 6), (3, 6), (4, 6), (3, 5), (4, 5), (0, 3), (1, 4)
    ]
    for u, v in edges:
        draw.line([nodes[u], nodes[v]], fill=(56, 189, 248, 160), width=wth)
    for px, py in nodes:
        r = 3 * scale
        draw.ellipse([px - r, py - r, px + r, py + r], fill=color)
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_warning_icon(filename="ui_icon_warning.png", size=(64, 64), scale=4):
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    color = (251, 191, 36, 255)
    p1 = (sw // 2, 8 * scale)
    p2 = (8 * scale, sh - 10 * scale)
    p3 = (sw - 8 * scale, sh - 10 * scale)
    draw.polygon([p1, p2, p3], outline=color, width=3 * scale)
    draw.line([(sw // 2, 22 * scale), (sw // 2, 36 * scale)], fill=color, width=3 * scale)
    r = 2 * scale
    draw.ellipse([(sw // 2) - r, 43 * scale - r, (sw // 2) + r, 43 * scale + r], fill=color)
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def create_timeline_graph(filename="ui_timeline_graph.png", size=(360, 110), scale=4):
    import math
    w, h = size
    sw, sh = w * scale, h * scale
    img = Image.new("RGBA", (sw, sh), (15, 23, 42, 240))
    draw = ImageDraw.Draw(img)
    for gy in range(18 * scale, sh, 24 * scale):
        draw.line([(10 * scale, gy), (sw - 10 * scale, gy)], fill=(30, 41, 59, 140), width=1 * scale)
    pts_green = []
    pts_amber = []
    steps = 100
    dx = (sw - 30 * scale) / steps
    for i in range(steps + 1):
        x = 15 * scale + i * dx
        t = i / float(steps)
        yg = sh * 0.70 - math.sin(t * 4.5) * (sh * 0.18) - (t * (sh * 0.10)) + math.sin(t * 12.0) * (sh * 0.04)
        ya = sh * 0.60 + math.sin(t * 3.8 + 1.2) * (sh * 0.22) + math.cos(t * 9.0) * (sh * 0.05)
        pts_green.append((x, max(10 * scale, min(sh - 10 * scale, yg))))
        pts_amber.append((x, max(10 * scale, min(sh - 10 * scale, ya))))
    draw.line(pts_amber, fill=(245, 158, 11, 220), width=2 * scale, joint="curve")
    draw.line(pts_green, fill=(52, 211, 153, 240), width=3 * scale, joint="curve")
    res = img.resize((w, h), Image.Resampling.LANCZOS)
    out_path = os.path.join(OUTPUT_DIR, filename)
    res.save(out_path, "PNG")
    print(f"Generated: {out_path} ({w}x{h})")

def main():
    print("Generating UI redesign 9-slice textures and icons...")
    create_rounded_rect_texture("ui_card_glass.png", size=(128, 128), radius=20, bg_color=(11, 18, 32, 230), border_color=(56, 189, 248, 70), border_width=2)
    create_rounded_rect_texture("ui_card_accent.png", size=(128, 128), radius=20, bg_color=(15, 23, 42, 240), border_color=(6, 182, 212, 190), border_width=2)
    create_rounded_rect_texture("ui_card_alert.png", size=(128, 128), radius=20, bg_color=(36, 22, 10, 245), border_color=(245, 158, 11, 230), border_width=2)
    create_rounded_rect_texture("ui_card_square.png", size=(128, 128), radius=18, bg_color=(15, 23, 42, 225), border_color=(56, 189, 248, 80), border_width=2)
    
    create_rounded_rect_texture("ui_btn_cyan.png", size=(128, 64), radius=18, bg_color=(8, 145, 178, 255), border_color=(103, 232, 249, 200), border_width=2)
    create_rounded_rect_texture("ui_btn_red.png", size=(128, 64), radius=18, bg_color=(185, 28, 28, 255), border_color=(248, 113, 113, 180), border_width=2)
    create_rounded_rect_texture("ui_btn_dark.png", size=(128, 64), radius=18, bg_color=(26, 36, 52, 230), border_color=(71, 85, 105, 180), border_width=2)
    
    create_pill_texture("ui_pill_badge.png", size=(128, 48), bg_color=(15, 23, 42, 220), border_color=(56, 189, 248, 120), border_width=2)
    create_pill_texture("ui_pill_green.png", size=(128, 48), bg_color=(6, 78, 59, 240), border_color=(52, 211, 153, 230), border_width=2)
    create_pill_texture("ui_pill_amber.png", size=(128, 48), bg_color=(120, 53, 15, 240), border_color=(251, 191, 36, 230), border_width=2)
    
    create_circle_texture("ui_circle_dot.png", size=(64, 64), fill_color=(255, 255, 255, 255))
    create_rounded_rect_texture("ui_timeline_track.png", size=(128, 24), radius=12, bg_color=(30, 41, 59, 200), border_color=(51, 65, 85, 160), border_width=1)
    
    create_circle_check_texture("ui_circle_check.png")
    create_camera_icon("ui_icon_camera.png")
    create_arcore_icon("ui_icon_arcore.png")
    create_ai_icon("ui_icon_ai.png")
    create_warning_icon("ui_icon_warning.png")
    create_timeline_graph("ui_timeline_graph.png")
    print("All UI redesign textures generated successfully.")

if __name__ == "__main__":
    main()
