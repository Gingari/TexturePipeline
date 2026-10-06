import base64
import cv2
import numpy as np
from fastapi import FastAPI, UploadFile, File, Form
from fastapi.responses import JSONResponse

app = FastAPI(title="NormalMapOnline Python Engine")

def process_height_map(albedo_gray: np.ndarray, blur_radius: int = 3, gamma: float = 0.6) -> np.ndarray:

    background_light = cv2.GaussianBlur(albedo_gray, (101, 101), 0)
    flat_height = cv2.addWeighted(albedo_gray, 1.0, background_light, -1.0, 128)


    if blur_radius > 0:
        k = blur_radius if blur_radius % 2 != 0 else blur_radius + 1
        flat_height = cv2.GaussianBlur(flat_height, (k, k), 0)

    norm = flat_height.astype(np.float32) / 255.0
    height_gamma = np.power(norm, gamma) * 255.0

    return height_gamma.astype(np.uint8)


def process_normal_map(height_map: np.ndarray, strength: float = 4.0, invert_y: bool = True) -> np.ndarray:
    """
    Точный порт WebGL-шейдера NormalMapOnline (Sobel Filter + Normalization)
    """
    h_float = height_map.astype(np.float32) / 255.0

    dx = cv2.Sobel(h_float, cv2.CV_32F, 1, 0, ksize=3) * strength
    dy = cv2.Sobel(h_float, cv2.CV_32F, 0, 1, ksize=3) * strength
    dz = 1.0 / strength

    norm = np.sqrt(dx * dx + dy * dy + dz * dz)
    nx = dx / norm
    ny = dy / norm
    nz = dz / norm
    if invert_y:
        ny = -ny

    r = ((nx + 1.0) * 0.5 * 255.0).clip(0, 255).astype(np.uint8)
    g = ((ny + 1.0) * 0.5 * 255.0).clip(0, 255).astype(np.uint8)
    b = ((nz + 1.0) * 0.5 * 255.0).clip(0, 255).astype(np.uint8)

    return cv2.merge([b, g, r])


def process_ambient_occlusion(height_map: np.ndarray, blur_range: int = 15, strength: float = 2.0) -> np.ndarray:
    """
    Вычисление Ambient Occlusion по методу кривизны (Difference of Gaussians)
    """
    h_float = height_map.astype(np.float32) / 255.0

    k = blur_range if blur_range % 2 != 0 else blur_range + 1
    blurred = cv2.GaussianBlur(h_float, (k, k), 0)

    diff = blurred - h_float
    ao = 1.0 - (diff * strength)
    ao = np.clip(ao, 0.0, 1.0) * 255.0

    return ao.astype(np.uint8)


def process_roughness_map(albedo_gray: np.ndarray) -> np.ndarray:
    """
    Генерация шероховатости: матовый базис + микро-фактура
    """
    blur = cv2.GaussianBlur(albedo_gray, (15, 15), 0)
    high_pass = cv2.subtract(albedo_gray, blur)
    hp_norm = cv2.normalize(high_pass, None, 0, 60, cv2.NORM_MINMAX)

    base = np.full_like(albedo_gray, 140, dtype=np.uint8)
    return cv2.add(base, hp_norm)


def img_to_base64(img_array: np.ndarray) -> str:
    if len(img_array.shape) == 2:
        img_array = cv2.cvtColor(img_array, cv2.COLOR_GRAY2BGR)
    _, buffer = cv2.imencode(".png", img_array)
    return base64.b64encode(buffer).decode("utf-8")


@app.post("/generate-pbr")
async def generate_pbr(
    file: UploadFile = File(...),
    strength: float = Form(4.5), 
    blur_radius: int = Form(3)   
):
    contents = await file.read()
    nparr = np.frombuffer(contents, np.uint8)
    albedo = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

    if albedo is None:
        return JSONResponse(status_code=400, content={"error": "Не удалось декодировать Albedo"})

    gray = cv2.cvtColor(albedo, cv2.COLOR_BGR2GRAY)

    height = process_height_map(gray, blur_radius=blur_radius, gamma=0.65)
    normal = process_normal_map(height, strength=strength, invert_y=True)
    ao = process_ambient_occlusion(height, blur_range=21, strength=2.5)
    roughness = process_roughness_map(gray)

    return {
        "albedo": img_to_base64(albedo),
        "normal": img_to_base64(normal),
        "height": img_to_base64(height),
        "roughness": img_to_base64(roughness),
        "ao": img_to_base64(ao)
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=5001)