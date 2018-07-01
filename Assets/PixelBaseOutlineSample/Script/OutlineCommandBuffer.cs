using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;


[ExecuteInEditMode,ImageEffectAllowedInSceneView]
public class OutlineCommandBuffer : MonoBehaviour 
{
    Camera cam;

    [Tooltip("Trun ON obscured outline cull,but this mode will be more expensive.")]
    public bool occlusionCullModeOn;

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

    [SerializeField,Range(0, 4),Tooltip("Reduce blured texture size,more value will be cheaper.")]
    private int downScale = 0;
    public int DownScale
    {
        get { return downScale; }
        set
        {
            downScale = value;
        }
    }
    [SerializeField, Range(1, 10),Tooltip("Blur interation count,more count will be more blurry、smoothly,performance also more expensive.")]
    private int interation = 1;

    [SerializeField,Range(0, 20)]
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
    [SerializeField,HideInInspector]
    Material postMat,flatColor,blur;
    
    [SerializeField]
    CommandBuffer buffer;

    public CameraEvent bufferEvent = CameraEvent.AfterForwardAlpha;

#if UNITY_EDITOR
    void OnValidate()
    {
        // Init();
        OutlineColor = outlineColor;
        DownScale = downScale;
        ColorIntensity = colorIntensity;
    }
    
#endif

    void OnEnable() 
    {
        Init();
        buffer = new CommandBuffer() { name = "Outline" };
        cam.AddCommandBuffer(bufferEvent,buffer);

    }

    void OnDisable()  
    {  
        cam.RemoveCommandBuffer(bufferEvent,buffer);
    }  

    void OnPreRender() {
         DrawBuffer();
    }

    int offsetID, maskMapID, intensityID,outlineColorID,blur1ID,blur2ID,depthMaskID,maskID;
    void Init()
    {
        cam = GetComponent<Camera>();

        postMat = new Material(Shader.Find("Hidden/OutlineCommandBuffer"));
        flatColor = new Material(Shader.Find("Hidden/FlatColor"));
        blur = new Material(Shader.Find("Hidden/KawaseBlurPostProcess"));

        //translate string to ID , better speed.
        offsetID = Shader.PropertyToID("_Offset");
        maskMapID = Shader.PropertyToID("_MaskTex");
        intensityID = Shader.PropertyToID("_Intensity");
        outlineColorID = Shader.PropertyToID("_OutlineColor");
        blur1ID = Shader.PropertyToID("blur1");
        blur2ID = Shader.PropertyToID("blur2");
        depthMaskID = Shader.PropertyToID("depthMask");
        maskID = Shader.PropertyToID("mask");
    }

    public void DrawBuffer()
    {
        if (buffer == null) return;

        buffer.Clear(); //before new draw,must be clear buffer first.

        //setup setting;
        var scale = -(1 << DownScale);

		buffer.GetTemporaryRT (blur1ID, scale, scale, 0, FilterMode.Bilinear,RenderTextureFormat.R8);
		buffer.GetTemporaryRT (blur2ID, scale, scale, 0, FilterMode.Bilinear,RenderTextureFormat.R8);
        buffer.GetTemporaryRT (depthMaskID, -1, -1, 0, FilterMode.Bilinear,RenderTextureFormat.R8); // must be fit screen size

        buffer.SetRenderTarget(depthMaskID);  
        buffer.ClearRenderTarget(true, true, Color.black);//clear rt

        int lastMask = depthMaskID;
        if (occlusionCullModeOn)
        {
            buffer.GetTemporaryRT (maskID, -1, -1, 0, FilterMode.Bilinear,RenderTextureFormat.R8);
            buffer.SetRenderTarget(maskID);  
            buffer.ClearRenderTarget(true, true, Color.black);//clear rt
            lastMask = maskID;
        }

        var targets = OutlineManager.Instance.objs;
        var count = targets.Count;
        for (int i = 0; i < count; i++)
        {
            var mats = targets[i].sharedMaterials;
            var length = mats.Length;
            if (targets[i]._transparent) //draw transparent objects
            {
                for (int index = 0; index < length; index++) 
                    DrawSubmesh(targets[i],index,mats[index]);
            } 
            else //draw opaque objects
            {
                for (int index = 0; index < length; index++) 
                    DrawSubmesh(targets[i],index,flatColor);
            }
        }

        // //copy depth map to mask map
        buffer.SetGlobalTexture(maskMapID, lastMask);
        buffer.Blit(depthMaskID,blur1ID,flatColor,0); //copy
        buffer.Blit(KawaseBlur(blur1ID, blur2ID),BuiltinRenderTextureType.CameraTarget,postMat,0);//clip mask

        //Relase tempRT
        buffer.ReleaseTemporaryRT(depthMaskID);
        buffer.ReleaseTemporaryRT(blur1ID);
        buffer.ReleaseTemporaryRT(blur2ID);
        buffer.ReleaseTemporaryRT(maskID);
    }


    void DrawSubmesh(OutlineObject obj,int subMeshIndex,Material mat)
    {
        #if UNITY_2018
            subMeshIndex += obj.subMeshIndex;
        #endif

        int pass = obj._transparent ? 0 : obj._occlusion ? 0 : 1;
        // draw depth mask
        buffer.SetRenderTarget(depthMaskID,BuiltinRenderTextureType.ResolvedDepth);//grab depth               
        buffer.DrawRenderer (obj.renderer, mat, subMeshIndex,pass);
        if (occlusionCullModeOn)
        {
            //draw non depth mask
            buffer.SetRenderTarget(maskID,BuiltinRenderTextureType.ResolvedDepth);//grab depth   
            buffer.DrawRenderer (obj.renderer, mat, subMeshIndex, obj._transparent ? 0 : obj._occlusionCull ? 1:0);
        }
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


public class OutlineManager  
{
    static private OutlineManager instance;
    static public OutlineManager Instance
    {
        get
        {
            if (instance == null)
                instance = new OutlineManager();
            return instance;
        }
        private set
        {
            instance = value;
        }
        
    }
    public List<OutlineObject> objs = new List<OutlineObject>();
}