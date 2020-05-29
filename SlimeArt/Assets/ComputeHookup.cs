using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeHookup : MonoBehaviour
{

    public ComputeShader compute;
    public RenderTexture result;
    public Material mat;
    // Start is called before the first frame update
    void Start()
    {
        int kernel = compute.FindKernel("CSMain");
        result = new RenderTexture(512, 512, 24);
        result.enableRandomWrite = true;
        result.Create();

        compute.SetTexture(kernel, "Result", result);
        compute.Dispatch(kernel, 512 / 8, 512 / 8, 1);
        mat.mainTexture = result;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
