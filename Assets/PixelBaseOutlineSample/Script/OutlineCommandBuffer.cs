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
                    instance = cam.gameObject.AddComponent<OutlineCommandBuffer>();
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
    static Camera cam;

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

    [SerializeField,Range(0, 4)]
    private int downScale = 0;
    public int DownScale
    {
        get { return downScale; }
        set
        {
            downScale = value;
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

    int offsetID, maskMapID, intensityID,outlineColorID,blur1ID,blur2ID,depthMaskID;
    bool isRuntime;

    // [SerializeField]
    Material postMat,flatColor,blur;
    [SerializeField]

    Renderer opaque,transparent;

    CommandBuffer buffer;

    public CameraEvent bufferEvent = CameraEvent.AfterForwardAlpha;

    void OnValidate()
    {
        if (!isRuntime) return;
        OutlineColor = outlineColor;
        DownScale = downScale;
        ColorIntensity = colorIntensity;
        DrawBuffer();
    }
    void Awake()
    {
        Init();
    }

    void Init()
    {
        if (Instance != this) Destroy(this);
        if (isRuntime) return;
        isRuntime = true;

        cam = GetComponent<Camera>();

        postMat = new Material(Shader.Find("Hide/OutlineCommandBuffer"));
        flatColor = new Material(Shader.Find("Hide/FlatColor"));
        blur = new Material(Shader.Find("Hide/KawaseBlurPostProcess"));

        //translate string to ID , better speed.
        offsetID = Shader.PropertyToID("_Offset");
        maskMapID = Shader.PropertyToID("_MaskTex");
        intensityID = Shader.PropertyToID("_Intensity");
        outlineColorID = Shader.PropertyToID("_OutlineColor");
        blur1ID = Shader.PropertyToID("blur1");
        blur2ID = Shader.PropertyToID("blur2");
        depthMaskID = Shader.PropertyToID("depthMask");

        buffer = new CommandBuffer();
        buffer.name = "Outline";
        OnValidate();
        // OnEnable();
    }
    
    void DrawBuffer()
    {
        buffer.Clear(); //before new draw,must be clear buffer first.
        
        var scale = -(1 << DownScale);

		buffer.GetTemporaryRT (blur1ID, scale, scale, 0, FilterMode.Bilinear);
		buffer.GetTemporaryRT (blur2ID, scale, scale, 0, FilterMode.Bilinear);
        buffer.GetTemporaryRT (depthMaskID, -1, -1, 0, FilterMode.Bilinear); // must be fit screen size
        
        buffer.SetRenderTarget(depthMaskID);  
        buffer.ClearRenderTarget(true, true, Color.black);//clear rt
        buffer.SetRenderTarget(depthMaskID,BuiltinRenderTextureType.ResolvedDepth);//grab depth
        
        //draw opaque objects
        buffer.DrawRenderer(opaque,flatColor,0,0);

        //draw transparent objects
        var mesh = transparent.GetComponent<MeshFilter>().sharedMesh;
        Debug.Log(transparent.GetComponent<MeshRenderer>().subMeshStartIndex);
        for (int i = 0; i < transparent.materials.Length; i++)
        {
            buffer.DrawRenderer(transparent,transparent.materials[i],transparent.GetComponent<MeshRenderer>().subMeshStartIndex+i,0); 
        }

        //copy depth map to mask map
        buffer.SetGlobalTexture(maskMapID,depthMaskID);

        //draw blur
        buffer.Blit(depthMaskID,blur1ID,flatColor,0); //copy
        buffer.Blit(KawaseBlur(blur1ID, blur2ID),BuiltinRenderTextureType.CameraTarget,postMat,0);//clip mask

        //Relase tempRT
        buffer.ReleaseTemporaryRT(depthMaskID);
        buffer.ReleaseTemporaryRT(blur1ID);
        buffer.ReleaseTemporaryRT(blur2ID);
    }


    void OnEnable() 
    {
        cam.AddCommandBuffer(bufferEvent,buffer);
        DrawBuffer();
    }

    void OnDisable()  
    {  
        buffer.Clear();  
        cam.RemoveCommandBuffer(bufferEvent, buffer); 
    }  

    int KawaseBlur(int from, int to)
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

}