FROM pytorch/pytorch:2.5.1-cuda12.1-cudnn9-runtime

LABEL org.opencontainers.image.title="TexturePipeline ComfyUI"
LABEL org.opencontainers.image.description="ComfyUI backend for PBR texture generation"
LABEL org.opencontainers.image.source="https://github.com/Gingari/TexturePipeline"

ENV DEBIAN_FRONTEND=noninteractive \
    PYTHONUNBUFFERED=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

RUN printf '%s\n' \
        'Acquire::Retries "10";' \
        'Acquire::http::Timeout "120";' \
        'Acquire::https::Timeout "120";' \
        'Acquire::ForceIPv4 "true";' \
        > /etc/apt/apt.conf.d/80-retries

RUN for attempt in 1 2 3 4 5; do \
        echo "[APT] Попытка установки ${attempt}/5"; \
        apt-get clean; \
        rm -rf /var/lib/apt/lists/*; \
        if apt-get update \
            && apt-get install -y --no-install-recommends \
                ca-certificates \
                git \
                wget \
                curl \
                libgl1 \
                libglib2.0-0; then \
            break; \
        fi; \
        if [ "$attempt" = "5" ]; then \
            echo "[APT] Не удалось установить зависимости"; \
            exit 1; \
        fi; \
        echo "[APT] Повтор через 15 секунд"; \
        sleep 15; \
    done \
    && rm -rf /var/lib/apt/lists/*

ARG COMFYUI_VERSION=v0.30.0

RUN git clone \
        --depth 1 \
        --branch "${COMFYUI_VERSION}" \
        https://github.com/comfyanonymous/ComfyUI.git \
        /app/ComfyUI \
    && git -C /app/ComfyUI rev-parse HEAD

WORKDIR /app/ComfyUI


RUN python -m pip install \
        --no-cache-dir \
        --prefer-binary \
        -r requirements.txt \
    && python -m pip install \
        --no-cache-dir \
        --prefer-binary \
        timm

RUN python -c "import torch; print('PyTorch:', torch.__version__)" \
    && python -m pip show comfy-kitchen \
    && python -m pip check

COPY entrypoint.sh /app/entrypoint.sh

RUN sed -i 's/\r$//' /app/entrypoint.sh \
    && chmod +x /app/entrypoint.sh

EXPOSE 8188

ENTRYPOINT ["/app/entrypoint.sh"]