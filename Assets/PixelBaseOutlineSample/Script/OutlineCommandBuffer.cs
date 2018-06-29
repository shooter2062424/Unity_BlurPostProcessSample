using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;


[ExecuteInEditMode,ImageEffectAllowedInSceneView]
public class OutlineCommandBuffer : MonoBehaviour 
{
    Camera cam;

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
    
    [SerializeField]
    Material postMat,flatColor,blur;
    
    [SerializeField]
    CommandBuffer buffer;

    public CameraEvent bufferEvent = CameraEvent.AfterForwardAlpha;

#if UNITY_EDITOR
    void OnValidate()
    {
        OutlineColor = outlineColor;
        DownScale = downScale;
        ColorIntensity = colorIntensity;
    }
    
#endif

    void OnEnable() 
    {
        Init();

    }

    void OnDisable()  
    {  
        cam.RemoveCommandBuffer(bufferEvent,buffer);
    }  

    void OnPreRender() {
         DrawBuffer();
    }

    [SerializeField]
    int gridcount = 10;
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

        buffer = new CommandBuffer() { name = "Outline" };
        cam.AddCommandBuffer(bufferEvent,buffer);
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
        buffer.SetRenderTarget(depthMaskID,BuiltinRenderTextureType.ResolvedDepth);//grab depth
        
        var targets = OutlineManager.Instance.objs;
        var count = targets.Count;
        for (int i = 0; i < count; i++) 
        {
            var renderer = targets[i].renderer;
            // if (!context.CheckVisible (renderer)) continue;

            var mats = targets[i].sharedMaterials;
            var length = mats.Length;
            if (targets[i]._transparent) {
                //draw transparent objects
                for (int index = 0; index < length; index++) {
#if UNITY_2018
                    buffer.DrawRenderer (renderer, mats[index], renderer.subMeshStartIndex + index, 0);
#else
                    buffer.DrawRenderer (renderer, mats[index], index, 0);
#endif
                }

            } else {
                //draw opaque objects
                for (int index = 0; index < length; index++) 
                {
                    if (targets[i]._occlusion)
                    {
                    #if UNITY_2018
                        buffer.DrawRenderer (renderer, flatColor, renderer.subMeshStartIndex + index, 0);
                        #else
                        buffer.DrawRenderer (renderer, flatColor, index, 0);
                    #endif
                    }
                    else
                    {
                    #if UNITY_2018
                        buffer.DrawRenderer (renderer, flatColor, renderer.subMeshStartIndex + index, 1);
                        #else
                        buffer.DrawRenderer (renderer, flatColor, index, 1);
                    #endif

                    }
                }
            }
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