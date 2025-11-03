using UnityEngine;
using PassthroughCameraSamples;

public interface IFrameProviderView {
    RenderTexture SourceRT { get; }
    Texture2D CpuReadable { get; }
    float Scale { get; }
    int PadX { get; }
    int PadY { get; }
}

public class FrameProvider : MonoBehaviour, IFrameProviderView
{
    [Header("Source")]
    [SerializeField] public WebCamTextureManager webCamTextureManager;
    [SerializeField] public Shader webCamCopyShader;

    [Header("Inference")]
    [SerializeField] public int modelSize = 640;


    // backing fields
    public RenderTexture sourceRT { get; private set; }
    public Texture2D cpuReadable { get; private set; }
    public float scale { get; private set; }
    public int padX { get; private set; }
    public int padY { get; private set; }

    // shader bits
    Material copyMat;
    RenderTexture scratchRT;
    static readonly int PropFlipY = Shader.PropertyToID("_FlipY");
    static readonly int PropFlipX = Shader.PropertyToID("_FlipX");
    static readonly int PropRotate = Shader.PropertyToID("_Rotate90");

    int currentW, currentH;

    void Awake()
    {
        if (!webCamTextureManager)
        {
            Debug.LogError("FrameProvider: assign WebCamTextureManager", this);
            enabled = false; return;
        }

        if (webCamCopyShader == null)
        {
            copyMat = new Material(Shader.Find("Hidden/WebCamCopy"));
        }
        else
        {
            copyMat = new Material(webCamCopyShader);
        }
        
        scratchRT = new RenderTexture(modelSize, modelSize, 0, RenderTextureFormat.ARGB32);
        scratchRT.Create();
        cpuReadable = new Texture2D(modelSize, modelSize, TextureFormat.RGB24, false);
    }

    void OnDestroy()
    {
        if (sourceRT) sourceRT.Release();
        if (scratchRT) scratchRT.Release();
        if (copyMat) Destroy(copyMat);
    }

    void EnsureSourceRT(int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        if (sourceRT != null && (currentW == w && currentH == h)) return;

        if (sourceRT) sourceRT.Release();
        sourceRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
        { useDynamicScale = false, antiAliasing = 1 };
        sourceRT.Create();
        currentW = w; currentH = h;
        Debug.Log($"FrameProvider: created sourceRT {w}x{h}");
    }

    void Update()
    {
        var wt = webCamTextureManager.WebCamTexture;
        if (wt == null || !wt.isPlaying || wt.width < 16 || wt.height < 16) return;

        // Create/resize sourceRT based on the *actual* camera size
        EnsureSourceRT(wt.width, wt.height);

        int rotate90Steps = 0;
#if UNITY_ANDROID
        rotate90Steps = Mathf.RoundToInt(wt.videoRotationAngle / 90f) % 4;
        copyMat.SetFloat(PropFlipY, wt.videoVerticallyMirrored ? 1f : 0f);
        copyMat.SetFloat(PropFlipX, 0f); // set to 1f if you find the feed mirrored
#else
        copyMat.SetFloat(PropFlipY, 0f);
        copyMat.SetFloat(PropFlipX, 0f);
#endif
        copyMat.SetFloat(PropRotate, rotate90Steps);

        Graphics.Blit(wt, sourceRT, copyMat);
    }

    public void BlitForModel()
    {
        if (!sourceRT) return;

        float srcW = sourceRT.width, srcH = sourceRT.height;
        float s = Mathf.Min(modelSize / srcW, modelSize / srcH);
        int newW = Mathf.RoundToInt(srcW * s);
        int newH = Mathf.RoundToInt(srcH * s);
        padX = (modelSize - newW) / 2;
        padY = (modelSize - newH) / 2;
        scale = s;

        var prev = RenderTexture.active;
        RenderTexture.active = scratchRT;
        GL.Clear(false, true, Color.black);
        Graphics.Blit(sourceRT, scratchRT);
        cpuReadable.ReadPixels(new Rect(0, 0, modelSize, modelSize), 0, 0, false);
        cpuReadable.Apply(false);
        RenderTexture.active = prev;
    }

    // IFrameProviderView implementation
    public RenderTexture SourceRT => sourceRT;
    public Texture2D CpuReadable => cpuReadable;
    public float Scale => scale;
    public int PadX => padX;
    public int PadY => padY;
}