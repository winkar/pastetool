#!/usr/bin/env python3
"""Generate a simple clipboard icon for PasteTool"""

from PIL import Image, ImageDraw

def create_icon():
    # Create images at different sizes
    sizes = [(16, 16), (32, 32), (48, 48), (256, 256)]
    images = []

    for size in sizes:
        # Create a new image with transparency
        img = Image.new('RGBA', size, (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)

        # Colors
        bg_color = (246, 240, 230)  # #F6F0E6
        border_color = (42, 31, 27)  # #2A1F1B
        accent_color = (201, 100, 66)  # #C96442

        w, h = size
        margin = max(2, w // 16)

        # Draw clipboard background
        clipboard_rect = [margin, margin + h // 8, w - margin, h - margin]
        draw.rounded_rectangle(clipboard_rect, radius=max(2, w // 16),
                             fill=bg_color, outline=border_color, width=max(1, w // 32))

        # Draw clipboard clip at top
        clip_width = w // 3
        clip_height = h // 8
        clip_x = (w - clip_width) // 2
        clip_rect = [clip_x, margin // 2, clip_x + clip_width, margin // 2 + clip_height]
        draw.rounded_rectangle(clip_rect, radius=max(1, w // 32),
                             fill=accent_color, outline=border_color, width=max(1, w // 48))

        # Draw lines representing text
        line_margin = margin + w // 8
        line_start_y = margin + h // 4
        line_spacing = max(2, h // 12)
        line_count = 3

        for i in range(line_count):
            y = line_start_y + i * line_spacing
            line_width = w - 2 * line_margin - (i * w // 16)  # Varying widths
            if y + line_spacing // 2 < h - margin:
                draw.line([(line_margin, y), (line_margin + line_width, y)],
                         fill=accent_color, width=max(1, w // 32))

        images.append(img)

    # Save as ICO
    images[0].save('C:/Users/winka/Documents/pastetool/src/PasteTool.App/Resources/app.ico',
                   format='ICO', sizes=[(img.width, img.height) for img in images])
    print("Icon created successfully!")

if __name__ == '__main__':
    create_icon()
