#!/bin/bash
set -Eeuo pipefail

MODELS_DIR="/app/ComfyUI/models"

CHECKPOINT_PATH="$MODELS_DIR/checkpoints/v1-5-pruned-emaonly.safetensors"
LCM_LORA_PATH="$MODELS_DIR/loras/pytorch_lora_weights.safetensors"
SEAMLESS_LORA_PATH="$MODELS_DIR/loras/seamless_texture.safetensors"

CHECKPOINT_URL="https://huggingface.co/stablediffusiontutorials/stable-diffusion-v1.5/resolve/main/v1-5-pruned-emaonly.safetensors"
LCM_LORA_URL="https://huggingface.co/latent-consistency/lcm-lora-sdv1-5/resolve/main/pytorch_lora_weights.safetensors"
SEAMLESS_LORA_URL="https://huggingface.co/gokaygokay/Flux-Seamless-Texture-LoRA/resolve/main/seamless_texture.safetensors"

mkdir -p "$MODELS_DIR/checkpoints"
mkdir -p "$MODELS_DIR/loras"

download_file() {
    local name="$1"
    local url="$2"
    local target="$3"
    local temporary="${target}.part"

    if [ -s "$target" ]; then
        echo "[OK] $name уже загружена."
        return 0
    fi

    echo "[Download] Загружаем $name..."

    curl \
        --fail \
        --location \
        --show-error \
        --retry 20 \
        --retry-delay 5 \
        --retry-all-errors \
        --connect-timeout 30 \
        --continue-at - \
        --output "$temporary" \
        "$url"

    if [ ! -s "$temporary" ]; then
        echo "[Error] Загруженный файл $name пуст."
        exit 1
    fi

    mv "$temporary" "$target"

    echo "[OK] $name успешно загружена."
}

echo "=== [PBR Pipeline] Проверка моделей ==="

download_file \
    "Stable Diffusion 1.5" \
    "$CHECKPOINT_URL" \
    "$CHECKPOINT_PATH"

download_file \
    "LCM-LoRA" \
    "$LCM_LORA_URL" \
    "$LCM_LORA_PATH"

download_file \
    "Seamless Texture LoRA" \
    "$SEAMLESS_LORA_URL" \
    "$SEAMLESS_LORA_PATH"

echo "=== [PBR Pipeline] Определение режима памяти ==="

COMFY_ARGS="${COMFY_ARGS:-}"

if [ -z "$COMFY_ARGS" ]; then
    if command -v nvidia-smi >/dev/null 2>&1 \
        && nvidia-smi >/dev/null 2>&1; then

        VRAM_MB=$(nvidia-smi \
            --query-gpu=memory.total \
            --format=csv,noheader,nounits \
            | head -n 1 \
            | tr -d ' ')

        echo "[ComfyUI] Обнаружено ${VRAM_MB} MB VRAM."

        if [ "$VRAM_MB" -lt 6000 ]; then
            COMFY_ARGS="--novram --disable-pinned-memory"
        elif [ "$VRAM_MB" -lt 9000 ]; then
            COMFY_ARGS="--lowvram"
        elif [ "$VRAM_MB" -lt 16000 ]; then
            COMFY_ARGS="--normalvram"
        else
            COMFY_ARGS=""
        fi
    else
        echo "[ComfyUI] NVIDIA GPU не обнаружена."
        COMFY_ARGS="--cpu --disable-pinned-memory"
    fi
fi

COMFY_EXTRA_ARGS=()

if [ -n "$COMFY_ARGS" ]; then
    read -r -a COMFY_EXTRA_ARGS <<< "$COMFY_ARGS"
fi

echo "[ComfyUI] Параметры запуска: ${COMFY_ARGS:-автоматически}"
echo "=== [PBR Pipeline] Запуск ComfyUI ==="

cd /app/ComfyUI

exec python main.py \
    --listen 0.0.0.0 \
    --port 8188 \
    "${COMFY_EXTRA_ARGS[@]}"