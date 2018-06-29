using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class OutlineObject : MonoBehaviour {

	public bool _transparent,_occlusion;
	[HideInInspector]
	public new Renderer renderer;
	[HideInInspector]
	public Material[] sharedMaterials;

	void OnEnable() 
	{
		renderer = GetComponent<Renderer>();
		this.sharedMaterials = renderer.sharedMaterials;
		OutlineManager.Instance.objs.Add(this);

	}

	void OnDisable()
	{
		OutlineManager.Instance.objs.Remove(this);
	}

}
