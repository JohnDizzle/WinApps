"""Render a captioned WinApps screenshot tour; requires Pillow and FFmpeg."""
import argparse
from pathlib import Path
import math
import subprocess
from PIL import Image, ImageDraw, ImageFont, ImageOps

parser = argparse.ArgumentParser()
parser.add_argument('--ffmpeg', required=True)
parser.add_argument('--screenshots', default=str(Path.home() / 'OneDrive/Pictures/Screenshots 1'))
parser.add_argument('--topic', choices=['intro', 'packages', 'settings', 'active'], default='intro')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/store' / args.topic
name = f'WinApps-{args.topic}'
out.mkdir(parents=True, exist_ok=True)
sources = out / 'screenshots'
sources.mkdir(exist_ok=True)
fontdir = Path('C:/Windows/Fonts')
def font(size, bold=False):
    return ImageFont.truetype(str(fontdir / ('segoeuib.ttf' if bold else 'segoeui.ttf')), size)
def shot(date, time):
    return f'Screenshot {date} {time}.png'
library = shot('2026-10-08', '205727')
overview = shot('2026-10-08', '170839')
package = shot('2026-10-07', '163435')
manifest = shot('2026-10-07', '231726')
settings = shot('2026-10-07', '222641')
preview = shot('2026-10-07', '232317')
settings_crop = (440, 60, 1896, 860)
scenes = {
    'intro': [
        (3, 'WinApps', 'Know the applications on your PC', None, None),
        (6, 'Filter your application library', 'Search by name or browse alphabetically.', library, (0, 68, 416, 1016)),
        (6, 'Select an app. See its footprint.', 'RAM, CPU, processes, and threads in one live overview.', overview, None),
        (6, 'Understand its type and metadata', 'Inspect version, publisher, source, app type, and package details.', package, None),
        (6, 'Know where it lives', 'Find installation folders, frameworks, and app data.', package, (440, 480, 1896, 1007)),
        (3, 'Your apps, revealed.', 'WinApps · Explore. Inspect. Understand.', None, None),
    ],
    'packages': [
        (3, 'Inside an MSIX package', 'WinApps · Package inspection', None, None),
        (8, 'Understand the package', 'Inspect app type, trust, framework clues, and resource packages.', package, None),
        (8, 'Read the manifest in depth', 'Explore declarations, extensions, capabilities, and original details.', manifest, None),
        (7, 'Find the installation and app data', 'See where the app lives and open its folders.', package, (440, 480, 1896, 1007)),
        (3, 'Explore the details', 'WinApps · Package information and files', None, None),
    ],
    'settings': [
        (3, 'Settings and app data', 'WinApps · Inspect and edit', None, None),
        (8, 'Review supported settings', 'Strings, numbers, and Boolean values retain their stored types.', settings, settings_crop),
        (6, 'Edit, then Save', 'Save each supported value explicitly. Reload to review it again.', settings, settings_crop),
        (7, 'Look inside configuration files', 'Preview formatted JSON and find the file location.', preview, None),
        (3, 'Understand your app data', 'WinApps · Settings and file previews', None, None),
    ],
    'active': [
        (3, 'Your active applications', 'WinApps · Windows and resource usage', None, None),
        (7, 'See what is open', 'Explore active app cards and return to an app window.', library, (440, 60, 1896, 745)),
        (7, 'Open an app’s own overview', 'See RAM, CPU, processes, and threads in its live view.', overview, None),
        (7, 'Launch or terminate', 'Launch a selected app. Confirm before stopping matching processes.', overview, (440, 68, 1896, 435)),
        (3, 'Keep your apps in view', 'WinApps · Active apps and controls', None, None),
    ],
}[args.topic]
segments = []
frames = []

def run(command):
    subprocess.run([args.ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', *command], check=True)
for index, (duration, title, subtitle, source, crop) in enumerate(scenes):
    canvas = Image.new('RGB', (1920, 1080), '#101823')
    draw = ImageDraw.Draw(canvas)
    for y in range(1080):
        draw.line((0, y, 1920, y), fill=(16 + y//120, 24 + y//160, 35 + y//90))
    draw.rounded_rectangle((80, 54, 164, 62), radius=4, fill='#c4a7e7')
    draw.text((185, 39), 'WinApps', font=font(27, True), fill='#d9c5ed')
    if source:
        original = Path(args.screenshots) / source
        screenshot = Image.open(original).convert('RGB')
        if crop:
            screenshot = screenshot.crop(crop)
        screenshot.save(sources / f'source-{index+1:02}.png')
        screenshot = ImageOps.contain(screenshot, (1760, 760), Image.Resampling.LANCZOS)
        x, y = (1920-screenshot.width)//2, 108+(760-screenshot.height)//2
        if screenshot.width < 500:
            x = 260
            draw.text((800, 275), 'Search your library', font=font(55, True), fill='white')
            draw.text((800, 385), 'Browse A–Z', font=font(55, True), fill='white')
            draw.text((800, 495), 'Select an application', font=font(55, True), fill='white')
        draw.rounded_rectangle((x-3,y-3,x+screenshot.width+3,y+screenshot.height+3), radius=12, fill='#6b5b80')
        canvas.paste(screenshot, (x,y))
        draw.text((80, 904), title, font=font(47, True), fill='white')
        draw.text((82, 971), subtitle, font=font(29), fill='#c7d5e7')
    else:
        logo = Image.open(root / 'Assets/StoreLogo.scale-200.png').convert('RGBA')
        logo = ImageOps.contain(logo, (180,180), Image.Resampling.LANCZOS)
        canvas.paste(logo, (92,235), logo)
        draw.text((90, 456), title, font=font(82, True), fill='white')
        draw.text((94, 577), subtitle, font=font(36), fill='#c7d5e7')
        draw.text((94, 672), 'Discover \u00b7 Inspect \u00b7 Understand', font=font(29), fill='#d9c5ed')
    image = out / f'scene-{index+1:02}.png'
    canvas.save(image)
    frames.append(canvas.copy())
    segment = out / f'segment-{index+1:02}.mp4'
    run(['-loop','1','-framerate','30','-i',str(image),'-f','lavfi','-i','anullsrc=channel_layout=stereo:sample_rate=48000',
         '-t',str(duration),'-vf',f'fade=t=in:st=0:d=0.25,fade=t=out:st={duration-0.25}:d=0.25',
         '-c:v','libx264','-preset','fast','-profile:v','high','-pix_fmt','yuv420p','-r','30',
         '-b:v','50M','-minrate','50M','-maxrate','50M','-bufsize','100M','-x264-params','nal-hrd=cbr:force-cfr=1',
         '-g','15','-bf','2','-flags','+cgop','-sc_threshold','0',
         '-c:a','aac','-b:a','384k','-ar','48000','-ac','2','-shortest',str(segment)])
    segments.append(segment)
    print(f'Rendered scene {index+1}/{len(scenes)}', flush=True)
concat = out / 'segments.txt'
concat.write_text(''.join(f"file '{p.as_posix()}'\n" for p in segments), encoding='utf-8')
master = out / f'{name}-store.mp4'
run(['-f','concat','-safe','0','-i',str(concat),'-c','copy','-movflags','+faststart',str(master)])
run(['-i',str(master),'-c:v','libx264','-preset','fast','-crf','20','-pix_fmt','yuv420p',
     '-c:a','aac','-b:a','192k','-movflags','+faststart',str(out/f'{name}.mp4')])
frames[2 if args.topic == 'intro' else 1].save(out/f'{name}-thumbnail.png')
sheet = Image.new('RGB', (1440, math.ceil(len(frames)/3)*270), '#101823')
for index, frame in enumerate(frames):
    sheet.paste(frame.resize((480,270)), ((index%3)*480,(index//3)*270))
sheet.save(out/f'{name}-contact-sheet.png')
print(f'Finished: {out}', flush=True)
