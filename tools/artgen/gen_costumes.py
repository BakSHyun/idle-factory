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

# (prompt, lock): full = 갓+얼굴 고정 / face = 얼굴만 고정 (모자 변형 허용)
COSTUMES = {
    "costume_space": ("1girl, solo, chibi, korean grim reaper girl, large pointed black gat, purple hair covering one eye, wearing white and orange astronaut spacesuit, space suit with tubes and chest control panel, sci-fi, cute, full body, thick outline, flat color, simple dark purple background", "full"),
    "costume_cat":   ("1girl, solo, chibi, girl wearing cat ear hood pajama onesie, cat hoodie with ears, paw gloves, purple hair covering one eye, tail, cute, sleepy, full body, thick outline, flat color, simple dark purple background", "face"),
    "costume_crimson": ("1girl, solo, chibi, korean grim reaper girl, large pointed black gat, purple hair covering one eye, wearing bright crimson red hanbok, red dress, red robe, red clothes with gold flower embroidery, cute, full body, thick outline, flat color, simple dark purple background", "full"),
    "costume_snow":    ("1girl, solo, chibi, korean grim reaper girl, large pointed white gat, purple hair covering one eye, wearing pure white hanbok, white dress, white robe, white clothes, silver trim, snowflakes, cute, full body, thick outline, flat color, simple dark purple background", "full"),
    "costume_gold":    ("1girl, solo, chibi, korean grim reaper girl, large pointed black gat with gold trim, purple hair covering one eye, wearing golden yellow royal robe, gold dress, gold embroidered clothes, shining gold, majestic, cute, full body, thick outline, flat color, simple dark purple background", "full"),
    "costume_shadow":  ("1girl, solo, chibi, korean grim reaper girl, large pointed black gat, purple hair covering one eye, wearing pitch black hooded cloak, black mist aura, glowing purple runes on fabric, mysterious, cute, full body, thick outline, flat color, simple dark purple background", "full"),
}
for name, (prompt, lock) in COSTUMES.items():
    image = pipe(prompt=prompt + ", masterpiece, high score", negative_prompt=NEG,
                 image=base, strength=0.85, guidance_scale=6.0, num_inference_steps=30,
                 generator=torch.Generator("cpu").manual_seed(77)).images[0]
    # 모듈화: 원본 머리(얼굴+갓)를 목선에서 합성 — 얼굴은 절대 안 바뀐다
    import numpy as np
    v = np.array(image).astype(float)
    b = np.array(base).astype(float)
    H = v.shape[0]
    head_end = int(H * (0.52 if lock == 'full' else 0.30))   # 치비 비율: 머리+갓이 상단 ~52%
    band = 80                   # 목선 블렌드 밴드
    mask = np.zeros((H, 1, 1))
    mask[:head_end - band] = 1.0
    ramp = np.linspace(1, 0, band).reshape(-1, 1, 1)
    mask[head_end - band:head_end] = ramp
    out = v * (1 - mask) + b * mask
    Image.fromarray(out.astype("uint8")).save(f"../../art/costumes/{name}.png")
    print("saved", name, flush=True)
