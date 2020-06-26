
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeHookup : MonoBehaviour
{
    public ComputeShader compute;
    public ComputeShader decay;
    public ComputeBuffer initialGalaxyPositions;
    public RenderTexture result;
    public RenderTexture deposit_in;
    public RenderTexture deposit_out;
    public RenderTexture tex_trace;
    public Material mat;
    //public Camera camera;
    // Start is called before the first frame update
    void Start()
    {

        //hello world

        // kernel is the compute shader (initial spark)
        // result will have the thing to blur in it
        int computeKernel = compute.FindKernel("CSMain");
        int pixelHeight = 512;
        int pixelWidth = (int)(Camera.main.aspect * pixelHeight);
        Debug.Log(pixelWidth);

        result = new RenderTexture(pixelWidth, pixelHeight, 32);
        result.enableRandomWrite = true;
        result.Create();

        float[] testArray = { 10.0f, 10.0f, 0.0f };
        initialGalaxyPositions = new ComputeBuffer(testArray.Length, sizeof(float));
        initialGalaxyPositions.SetData(testArray);
        compute.SetBuffer(computeKernel, "buffer", initialGalaxyPositions);
        compute.SetFloat("pixelWidth", pixelWidth);
        compute.SetFloat("pixelHeight", pixelHeight);
        compute.SetTexture(computeKernel, "Result", result);
        compute.Dispatch(computeKernel, 512 / 8, 512 / 8, 1);
        //mat.mainTexture = result;

        //decay kernel
        int decayKernel = decay.FindKernel("CSMain");

        // deposit_in
        //deposit_in = new RenderTexture(512, 512, 32);
        //deposit_in.enableRandomWrite = true;
        //deposit_in.Create();

        // deposit_out is gonna have the blurred triangles
        deposit_out = new RenderTexture(512, 512, 32);
        deposit_out.enableRandomWrite = true;
        deposit_out.Create();

        // tex_trace is keeping track of the trace
        // tex_trace = new RenderTexture(512, 512, 32);
        //  tex_trace.enableRandomWrite = true;
        //  tex_trace.Create();

        // send squares texture in as deposit_in
        decay.SetTexture(decayKernel, "deposit_in", result);
        decay.SetTexture(decayKernel, "deposit_out", deposit_out);
        // decay.SetTexture(decayKernel, "tex_trace", tex_trace);
        decay.Dispatch(decayKernel, 512 / 8, 512 / 8, 1);

        float h = Camera.main.pixelWidth;
        // Debug.Log(h);

        mat.mainTexture = deposit_out;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
