// PhotoCapture.cs —— 现场照片（方案 C）：提交时截取当前 AR 画面，降采样 + JPEG 压缩转 base64。
// 用途：跨设备定位的"人找点位"辅助——GPS 导航到附近后，管理端看图精确确认具体点位。
// 失败时返回空串（照片可选，不影响上报）。
using UnityEngine;

namespace ParkInspectionAR
{
    public static class PhotoCapture
    {
        // 截取当前屏幕（含 AR 相机画面），降采样到 maxWidth，JPEG 压缩，返回 base64；失败返回空串。
        public static string CaptureBase64(int maxWidth = 480, int quality = 55)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
            {
                return "";
            }
            try
            {
                Texture2D outTex = tex;
                if (tex.width > maxWidth)
                {
                    int w = maxWidth;
                    int h = Mathf.Max(1, Mathf.RoundToInt(tex.height * (maxWidth / (float)tex.width)));
                    outTex = Scale(tex, w, h);
                    Object.Destroy(tex);
                }
                var bytes = outTex.EncodeToJPG(quality);
                if (outTex != tex)
                {
                    Object.Destroy(outTex);
                }
                return System.Convert.ToBase64String(bytes);
            }
            catch (System.Exception)
            {
                return "";
            }
        }

        static Texture2D Scale(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0);
            rt.filterMode = FilterMode.Bilinear;
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(src, rt);
            var res = new Texture2D(w, h, TextureFormat.RGB24, false);
            res.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            res.Apply();
            RenderTexture.active = old;
            RenderTexture.ReleaseTemporary(rt);
            return res;
        }
    }
}
