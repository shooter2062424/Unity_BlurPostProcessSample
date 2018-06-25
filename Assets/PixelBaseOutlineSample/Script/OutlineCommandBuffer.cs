using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class OutlineCommandBuffer : MonoBehaviour 
{
    static private OutlineCommandBuffer instance;
    static public OutlineCommandBuffer Instance
    {
        get
        {
            if (!instance)
            {
                instance = FindObjectOfType(typeof(OutlineCommandBuffer)) as OutlineCommandBuffer;

                if (!instance)
                    instance = Camera.main.gameObject.AddComponent<OutlineCommandBuffer>();
                instance.Init();
            }
            return instance;
        }
        private set
        {
            if (value == null)
                Destroy(instance);
            instance = value;
        }
    }
    
    public bool pixelBase,occluder,alphaDepth;
    Camera postProcessCam, maskCam;

    [SerializeField]
    private Color outlineColor = new Color(1,.2f,0,1);
    public Color OutlineColor
    {
        get { return outlineColor; }
        set
        {
            outlineColor = value;
            postMat.SetColor(outlineColorID, value);
        }
    }

    [SerializeField,Range(1, 16)]
    private int resolutionReduce = 2;
    public int ResolutionReduce
    {
        get { return resolutionReduce; }
        set
        {
            resolutionReduce = value;
            GetTempRenderTexture();
        }
    }
    [SerializeField, Range(1, 10)]
    private int interation = 1;

    [SerializeField,Range(0, 10)]
    private float offset = 1, colorIntensity = 3;
    public float ColorIntensity
    {
        get { return colorIntensity; }
        set
        {
            colorIntensity = value;
            postMat.SetFloat(intensityID, value);
        }
    }


    //if need ingore some layer,just edit this list.
    public string[] ignoreLayerName = new string[] {
        "Outline"
        , "Water"
        , "TransparentFX"
        ,"UI"
    };
    int[] ignoreLayerIndex;
    int offsetID, maskMapID, intensityID,outlineColorID;
    bool isRuntime;

    [SerializeField, Header("Debug")]
    private RenderTexture maskTexture;
    [SerializeField]
    private RenderTexture tempRT1, tempRT2;
    [SerializeField]
    private Material postMat,flatColor,grabDepth,blur;
    [SerializeField]
    private RawImage mask, temp1, temp2;

    [SerializeField]
    private Renderer opaque,transparent;

    CommandBuffer buffer;

    public CameraEvent bufferEvent = CameraEvent.AfterForwardAlpha;


    void OnValidate()
    {
        if (!isRuntime) return;
        OutlineColor = outlineColor;
        ResolutionReduce = resolutionReduce;
        ColorIntensity = colorIntensity;
        DrawBuffer();
        AttachToRawImage();
    }
    void Start()
    {
        Init();
    }

    void Init()
    {
        if (Instance != this) Destroy(this);
        if (isRuntime) return;
        isRuntime = true;

        transparent.enabled = false;

        buffer = new CommandBuffer();
        Camera.main.AddCommandBuffer(bufferEvent,buffer);

        postMat = new Material(Shader.Find("Hide/OutlinePostprocess"));
        flatColor = new Material(Shader.Find("Hide/FlatColor"));
        blur = new Material(Shader.Find("Hide/KawaseBlurPostProcess"));

        //translate string to ID , better speed.
        offsetID = Shader.PropertyToID("_Offset");
        maskMapID = Shader.PropertyToID("_MaskTex");
        intensityID = Shader.PropertyToID("_Intensity");
        outlineColorID = Shader.PropertyToID("_OutlineColor");

        maskTexture = RenderTexture.GetTemporary(Screen.width, Screen.height, 16, RenderTextureFormat.R8);
        OnValidate();


        buffer.name = "Outline";
        DrawBuffer();
    }

    RenderTexture KawaseBlur(RenderTexture from, RenderTexture to)
    {
        bool swich = true;
        for (int i = 0; i < interation; i++)
        {
            // ClearBuffer(swich ? to : from);
            buffer.SetGlobalFloat(offsetID, i+offset);
            buffer.Blit(swich ? from : to, swich ? to : from, blur);
            swich = !swich;
        }
        return swich ? from : to;
    }


    void DrawBuffer()
    {
        buffer.Clear(); //must be clear first.
        buffer.SetRenderTarget(maskTexture);  
        buffer.ClearRenderTarget(true, true, Color.black);//clear rt

        buffer.SetRenderTarget(maskTexture,BuiltinRenderTextureType.ResolvedDepth);//grab depth
        //draw objects
        buffer.DrawRenderer(opaque,flatColor,0,2);
        for (int i = 0; i < transparent.GetComponent<MeshFilter>().mesh.subMeshCount; i++)
        {
            buffer.DrawRenderer(transparent,flatColor,i,4); 
        }

        //draw rt
        // buffer.SetGlobalTexture(maskMapID,maskTexture);
        postMat.SetTexture(maskMapID, maskTexture); // will not changed.
        buffer.Blit(maskTexture,tempRT1);
        buffer.Blit(KawaseBlur(tempRT1, tempRT2),BuiltinRenderTextureType.CameraTarget,postMat,1);//clip mask
    }

    void OnDisable()  
    {  
        buffer.Clear();  
        Camera.main.RemoveCommandBuffer(bufferEvent, buffer);  
    }  

    void AttachToRawImage()
    {
        try
        {
            mask.texture = maskTexture;
            temp1.texture = tempRT1;
            temp2.texture = tempRT2;
        }
        catch { }
    }

    void OnDestroy()
    {
        RenderTexture.ReleaseTemporary(tempRT1);
        RenderTexture.ReleaseTemporary(tempRT2);
    }

    void GetTempRenderTexture()
    {
        OnDestroy();
        tempRT1 = RenderTexture.GetTemporary(maskTexture.width / resolutionReduce, maskTexture.height / resolutionReduce, 0, RenderTextureFormat.R8);
        tempRT2 = RenderTexture.GetTemporary(maskTexture.width / resolutionReduce, maskTexture.height / resolutionReduce, 0, RenderTextureFormat.R8);
    }

    void ClearBuffer(RenderTexture rt)
    {
        buffer.SetRenderTarget(maskTexture);  
        buffer.ClearRenderTarget(true, true, Color.black);//clear rt
    }

}