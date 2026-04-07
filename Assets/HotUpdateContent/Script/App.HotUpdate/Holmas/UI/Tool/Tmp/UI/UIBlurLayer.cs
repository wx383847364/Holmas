using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zeus.Framework.UI
{
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(UnityEngine.UI.RawImage))]
    public class UIBlurLayer : MonoBehaviour
    {

        [Tooltip("模糊效果使用的Shader名称")]
        public string shaderName = "UI/UIBlurLayer";

        [Range(0, 6), Tooltip("[降采样次数]向下采样的次数。此值越大,则采样间隔越大,需要处理的像素点越少,运行速度越快。")]
        public int downSampleNum = 2;

        [Range(0.0f, 20.0f), Tooltip("[模糊扩散度]进行高斯模糊时，相邻像素点的间隔。此值越大相邻像素间隔越远，图像越模糊。但过大的值会导致失真。")]
        public float blurSpreadSize = 5.0f;

        [Range(0, 8), Tooltip("[迭代次数]此值越大,则模糊操作的迭代次数越多，模糊效果越好，但消耗越大。")]
        public int blurIterations = 1;

        [Tooltip("模糊之后叠加的颜色")]
        public Color blurColor = new Color(140f / 255f, 140f / 255f, 140f / 255f, 1f);

        [SerializeField, Tooltip("直接指定Shader引用，避免运行时查找失败导致的空指针。可选。")]
        private Shader _shaderOverride;

        private UnityEngine.UI.RawImage _rawImage;
        private Camera _camera;
        private RenderTexture _mainTexture;

        private RenderTextureFormat _rtFormat = RenderTextureFormat.RGB111110Float;

        private Material _material;
        Material material
        {
            get
            {
                if (_material == null)
                {
                    Shader targetShader = _shaderOverride != null ? _shaderOverride : Shader.Find(shaderName);
                    if (targetShader == null)
                    {
                        Debug.LogErrorFormat(this, "UIBlurLayer找不到Shader: {0}，请检查图形设置或手动指定。", shaderName);
                        enabled = false;
                        return null;
                    }
                    _material = new Material(targetShader);
                    _material.hideFlags = HideFlags.HideAndDontSave;
                }
                return _material;
            }
        }

        private void Cleanup()
        {
            if (_material) Object.DestroyImmediate(_material);
            _material = null;
            if (_mainTexture) RenderTexture.ReleaseTemporary(_mainTexture);
            _mainTexture = null;
            _camera.enabled = false;
            _rawImage.enabled = false;
        }

        void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_rawImage == null) _rawImage = GetComponent<UnityEngine.UI.RawImage>();
            _camera.enabled = false;
            _rawImage.enabled = false;
            if (!SystemInfo.SupportsRenderTextureFormat(_rtFormat))
            {
                _rtFormat = RenderTextureFormat.DefaultHDR;
            }
            if (!SystemInfo.supportsImageEffects)
            {
                enabled = false;
                _rawImage.enabled = true;
                Debug.LogWarning("Target device not supports UIBlurLayer !!!");
                Debug.LogWarning("SystemInfo.supportsImageEffects: " + SystemInfo.supportsImageEffects);
                return;
            }
        }

        void Start()
        {
            enabled = true;
            _camera.enabled = true;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            Material mat = material;
            if (mat == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            float widthMod = 1.0f / (1.0f * (1 << downSampleNum));
            mat.SetFloat("_DownSampleValue", blurSpreadSize * widthMod);

            int renderWidth = src.width >> downSampleNum;
            int renderHeight = src.height >> downSampleNum;

            _mainTexture = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, _rtFormat);
            _mainTexture.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, _mainTexture, mat, 0);

            for (int i = 0; i < blurIterations; i++)
            {
                float iterationOffs = (i * 1.0f);
                mat.SetFloat("_DownSampleValue", blurSpreadSize * widthMod + iterationOffs);

                RenderTexture tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, _rtFormat);
                Graphics.Blit(_mainTexture, tempBuffer, mat, 1);
                RenderTexture.ReleaseTemporary(_mainTexture);
                _mainTexture = tempBuffer;

                tempBuffer = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, _rtFormat);
                Graphics.Blit(_mainTexture, tempBuffer, mat, 2);
                RenderTexture.ReleaseTemporary(_mainTexture);
                _mainTexture = tempBuffer;
            }

            _rawImage.texture = _mainTexture;
            _rawImage.color = blurColor;
            _rawImage.enabled = true;
            _camera.enabled = false;
            enabled = false;

            Graphics.Blit(_mainTexture, dest);
        }

        void OnDestroy()
        {
            Cleanup();
        }
    }
}
