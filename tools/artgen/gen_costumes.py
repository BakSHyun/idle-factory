#!/usr/bin/env python
"""코스튬 생성 — img2img로 같은 포즈에 복장만 교체 (룩덕 파이프라인)."""
import torch
from diffusers import StableDiffusionXLImg2ImgPipeline
from PIL import Image

pipe = StableDiffusionXLImg2ImgPipeline.from_pretrained(
    "cagliostrolab/animagine-xl-4.0", torch_dtype=torch.float16, use_safetensors=True).to("mps")

base = Image.open("../../art/concepts/main_hires_c.png").convert("RGB").resize((1024, 1024))
NEG = ("lowres, bad anatomy, bad hands, text, watermark, worst quality, low quality, "
       "multiple views, character sheet, 2girls, realistic, photo")

COSTUMES = {
    "costume_crimson": "1girl, solo, chibi, korean grim reaper girl, large pointed black gat, purple hair covering one eye, wearing crimson red royal hanbok with flower embroidery, cute, full body, thick outline, flat color, simple dark purple background",
    "costume_snow":    "1girl, solo, chibi, korean grim reaper girl, large pointed white gat, purple hair covering one eye, wearing pure white mourning hanbok with silver trim, snowflakes, cute, full body, thick outline, flat color, simple dark purple background",
    "costume_gold":    "1girl, solo, chibi, korean grim reaper girl, large pointed black gat with gold trim, purple hair covering one eye, wearing black official robe with golden embroidery, majestic, cute, full body, thick outline, flat color, simple dark purple background",
    "costume_shadow":  "1girl, solo, chibi, korean grim reaper girl, large pointed black gat, purple hair covering one eye, wearing pitch black shadow cloak with dark mist, mysterious, cute, full body, thick outline, flat color, simple dark purple background",
}
for name, prompt in COSTUMES.items():
    image = pipe(prompt=prompt + ", masterpiece, high score", negative_prompt=NEG,
                 image=base, strength=0.6, guidance_scale=6.0, num_inference_steps=30,
                 generator=torch.Generator("cpu").manual_seed(6)).images[0]
    image.save(f"../../art/costumes/{name}.png")
    print("saved", name, flush=True)
