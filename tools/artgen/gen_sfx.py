#!/usr/bin/env python
"""절차 합성 효과음/BGM 생성 → Assets/Resources/Sfx, Bgm (라이선스 걱정 제로)."""
import os
import numpy as np
from scipy.io import wavfile

SR = 44100
OUT_SFX = "../../Assets/Resources/Sfx"
OUT_BGM = "../../Assets/Resources/Bgm"

def env(n, attack=0.005, release=0.15):
    t = np.arange(n) / SR
    total = n / SR
    e = np.minimum(t / max(attack, 1e-4), 1.0)
    rel = np.clip((total - t) / max(release, 1e-4), 0, 1)
    return e * rel

def save(name, x, out=OUT_SFX):
    os.makedirs(out, exist_ok=True)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.85
    wavfile.write(os.path.join(out, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("saved", name)

def tone(freq, dur, shape="sine", detune=0.0):
    t = np.arange(int(SR * dur)) / SR
    f = freq * (1 + detune * t)
    phase = 2 * np.pi * np.cumsum(f) / SR
    if shape == "square": return np.sign(np.sin(phase))
    if shape == "saw": return 2 * ((phase / (2 * np.pi)) % 1) - 1
    return np.sin(phase)

# 클릭: 짧은 블립
n = int(SR * 0.07)
save("click", tone(880, 0.07) * env(n, 0.002, 0.05))

# 타격: 노이즈 + 저음 펀치
n = int(SR * 0.16)
noise = np.random.randn(n) * env(n, 0.001, 0.09)
punch = tone(120, 0.16, detune=-0.5) * env(n, 0.001, 0.12)
save("hit", noise * 0.5 + punch)

# 피격 (아군): 낮고 둔탁
n = int(SR * 0.2)
save("hurt", (tone(90, 0.2, "square", detune=-0.3) * 0.7 + np.random.randn(n) * 0.25) * env(n, 0.001, 0.15))

# 코인: 밝은 이중 딩
a = tone(1318, 0.09) * env(int(SR * 0.09), 0.001, 0.07)
b = tone(1760, 0.22) * env(int(SR * 0.22), 0.001, 0.18)
save("coin", np.concatenate([a, b]))

# 소환: 상승 스윕 + 차임
sweep = tone(300, 0.5, "saw", detune=2.2) * env(int(SR * 0.5), 0.01, 0.2) * 0.4
def overlay(canvas, seg, start):
    end = min(len(canvas), start + len(seg))
    canvas[start:end] += seg[:end - start]

chime = np.zeros(int(SR * 1.2))
for i, f in enumerate([1046, 1318, 1568]):
    seg = tone(f, 0.5) * env(int(SR * 0.5), 0.002, 0.4)
    overlay(chime, seg * 0.5, int(SR * (0.35 + i * 0.09)))
base = np.zeros(len(chime))
overlay(base, sweep, 0)
save("summon", base + chime)

# 레벨업/승급: 아르페지오
notes = [523, 659, 784, 1046]
total = np.zeros(int(SR * 0.75))
for i, f in enumerate(notes):
    seg = tone(f, 0.3) * env(int(SR * 0.3), 0.002, 0.22)
    overlay(total, seg, int(SR * i * 0.11))
save("levelup", total)

# 보스 클리어: 웅장한 화음
n = int(SR * 1.2)
chord = sum(tone(f, 1.2) for f in [261, 329, 392, 523]) * env(n, 0.01, 0.9)
save("victory", chord)

# ── BGM: 다크 앰비언트 루프 (저승 무드, 24초 심리스) ──
dur = 24.0
n = int(SR * dur)
t = np.arange(n) / SR
bgm = np.zeros(n)
# 저음 드론 (Am 계열)
for f, amp in [(55, 0.30), (110, 0.18), (164.8, 0.10)]:
    lfo = 1 + 0.008 * np.sin(2 * np.pi * t / 12.0)
    bgm += amp * np.sin(2 * np.pi * f * lfo * t)
# 느린 패드 코드 진행 (Am → F → C → G, 6초씩)
chords = [[220, 261.6, 329.6], [174.6, 220, 261.6], [261.6, 329.6, 392], [196, 246.9, 293.7]]
for ci, chord in enumerate(chords):
    seg_n = n // 4
    seg_t = np.arange(seg_n) / SR
    fade = np.minimum(seg_t / 1.5, 1) * np.minimum((seg_n / SR - seg_t) / 1.5, 1)
    seg = sum(0.06 * np.sin(2 * np.pi * f * seg_t) for f in chord) * fade
    bgm[ci * seg_n:(ci + 1) * seg_n] += seg
# 드문 종소리 (도깨비불 감성)
rng = np.random.default_rng(42)
for _ in range(6):
    start = int(rng.uniform(0, dur - 3) * SR)
    f = rng.choice([880, 1046, 1174])
    bell_n = int(SR * 2.5)
    bell = 0.05 * np.sin(2 * np.pi * f * np.arange(bell_n) / SR) * np.exp(-np.arange(bell_n) / (SR * 0.8))
    bgm[start:start + bell_n] += bell
save("main_theme", bgm, OUT_BGM)
print("done")
