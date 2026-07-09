import sys
import os
from PIL import Image

def convert_png_to_ico(png_path, ico_path="app_icon.ico"):
    if not os.path.exists(png_path):
        print(f"Error: File '{png_path}' not found.")
        return False
        
    try:
        img = Image.open(png_path)
        # Standard icon sizes for high-DPI Windows displays
        sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
        
        # Save as multi-resolution ICO file
        img.save(ico_path, format="ICO", sizes=sizes)
        print(f"Success! Converted '{png_path}' to multi-resolution '{ico_path}'.")
        return True
    except Exception as e:
        print(f"Failed to convert image: {e}")
        return False

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python convert_icon.py <path_to_your_png_file>")
    else:
        convert_png_to_ico(sys.argv[1])
