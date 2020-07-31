
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeHookup : MonoBehaviour
{ 
    public ComputeShader compute;
    public ComputeShader decay;

    public ComputeBuffer initialGalaxyPositions;
    public ComputeBuffer particles_x;
    public ComputeBuffer particles_y;
    public ComputeBuffer particles_theta;
    public ComputeBuffer particles_weights;

    public RenderTexture result;
    public RenderTexture deposit_in;
    public RenderTexture deposit_out;
    public RenderTexture tex_deposit;
    public RenderTexture tex_trace;
    public Material mat;


    public float sense_spread; // default = 15-30 degrees
    public float sense_distance; // in world-space units; default = about 1/100 of the world 'cube' size
    public float turn_angle; // default = 15 degrees
    public float move_distance; // in world-space units; default = about 1/5--1/3 of sense_distance
    public float agent_deposit; // 0 for data-driven fitting, >0 for self-reinforcing behavior
    public int world_width; //
    public int world_height; // grid dimensions - note that the particle positions are also stored in the grid coordinates, but continuous
    public int world_depth; //
    public float move_sense_coef;
    public float normalization_factor;

    public int pixelWidth;
    public int pixelHeight;
    public const float PI = 3.1415926535897931f;
    public int swap;

    //public Camera camera;
    // Start is called before the first frame update
    void Start()
    {
        //Debug.Log("TESTING SAVING ISSUE");

        // kernel is the compute shader (initial spark)
        // result will have the thing to blur in it
        int computeKernel = compute.FindKernel("CSMain");
        
        // dealing with the aspect ratio
        pixelHeight = 512;
        pixelWidth = 512;//(int)(Camera.main.aspect * pixelHeight);

        result = initializeRenderTexture();

        // random seeding of arrays
        float[] xParticlePositions = new float[512 * 512];
        float[] yParticlePositions = new float[512 * 512];
        float[] thetaParticles = new float[512 * 512];
        float[] weightsParticles = new float[512 * 512];
        int index = 0;
        for (int i = 0; i < 512; i++) {
            for (int j = 0; j < 512; j++) {
                xParticlePositions[index] = Random.Range(0.0f, 512.0f);// i / (512.0f);
                yParticlePositions[index] = Random.Range(0.0f, 512.0f);// j / (512.0f);
                thetaParticles[index] = Random.Range(0.0f, 2.0f * PI);
                weightsParticles[index] = 1.0f; // particle
                
                if (Random.Range(0.0f, 1.0f) < 0.0005f) {
                    //thetaParticles[i] = -2.0f; // make it a deposit so it goes in the deposit texture
                    weightsParticles[index] = 2.0f;
                    //Debug.Log("x: " + xParticlePositions[index] + " y: " + yParticlePositions[index]);
                } else if (index > 512 * 512 / 3) {
                    weightsParticles[index] = 0.0f; // particle
                }
                index++;
            }
        }
        
        // x particle positions
        particles_x = initializeComputeBuffer(xParticlePositions, "particles_x", computeKernel);

        // y particle positions
        particles_y = initializeComputeBuffer(yParticlePositions, "particles_y", computeKernel);

        // particles theta
        particles_theta = initializeComputeBuffer(thetaParticles, "particles_theta", computeKernel);

        // particle weights
        particles_weights = initializeComputeBuffer(weightsParticles, "particle_weights", computeKernel);

        // deposit texture for propegate shader
        tex_deposit = initializeRenderTexture();
        deposit_in = initializeRenderTexture();
        compute.SetTexture(computeKernel, "tex_deposit", deposit_in);

        // trace texture for the propegate shader
        tex_trace = initializeRenderTexture();
        compute.SetTexture(computeKernel, "tex_trace", tex_trace);

        // other variables
        int worldWidth = 512;
        int worldHeight = 512;
        compute.SetFloat("half_sense_spread", 25.0f); // 15 to 30 degrees default
        compute.SetFloat("sense_distance", worldHeight / 100.0f); // in world-space units; default = about 1/100 of the world 'cube' size
        compute.SetFloat("turn_angle", 15.0f); // 15.0 is default
        compute.SetFloat("move_distance", 0.001f);//worldHeight / 100.0f / 4.0f); //  in world-space units; default = about 1/5--1/3 of sense_distance
        compute.SetFloat("agent_deposit", 15.0f); // 15.0 is default
        compute.SetInt("world_width", worldWidth); 
        compute.SetInt("world_height", worldHeight); 
        compute.SetFloat("move_sense_coef", 1.0f); // ?
        compute.SetFloat("normalization_factor", 2.0f); // ?
        compute.SetFloat("pixelWidth", pixelWidth);
        compute.SetFloat("pixelHeight", pixelHeight);
        compute.SetTexture(computeKernel, "Result", result);
        
        // dispatch the texture
        compute.Dispatch(computeKernel, 512 / 8, 512 / 8, 1);
        //mat.mainTexture = result;

        //decay kernel
        //int decayKernel = decay.FindKernel("CSMain");

        // deposit texture for propegate shader
        //deposit_in = initializeRenderTexture();
        //decay.SetTexture(computeKernel, "deposit_in", deposit_in);

        // trace texture for the propegate shader
        //deposit_out = initializeRenderTexture();
        //compute.SetTexture(computeKernel, "deposit_out", deposit_out);


        // deposit_in
        //deposit_in = new RenderTexture(512, 512, 32);
        //deposit_in.enableRandomWrite = true;
        //deposit_in.Create();

        // deposit_out is gonna have the blurred triangles
        deposit_out = initializeRenderTexture();

        // tex_trace is keeping track of the trace
       // tex_trace = new RenderTexture(512, 512, 32);
      //  tex_trace.enableRandomWrite = true;
      //  tex_trace.Create();

        // send squares texture in as deposit_in
        //decay.SetTexture(decayKernel, "deposit_in", tex_deposit);
        //decay.SetTexture(decayKernel, "deposit_out", deposit_out);
       // decay.SetTexture(decayKernel, "tex_trace", tex_trace);
        //decay.Dispatch(decayKernel, 512 / 8, 512 / 8, 1);

        float h = Camera.main.pixelWidth;
        // Debug.Log(h);

        // mat.mainTexture = deposit_out;
        swap = 0;

    }

    ComputeBuffer initializeComputeBuffer(float[] arr, string shaderBufferName, int computeKernel) {
        ComputeBuffer computeBuffer = new ComputeBuffer(arr.Length, sizeof(float));
        computeBuffer.SetData(arr);
        compute.SetBuffer(computeKernel, shaderBufferName, computeBuffer);
        return computeBuffer;
    }

    RenderTexture initializeRenderTexture() {
        RenderTexture renderTexture = new RenderTexture(pixelWidth, pixelHeight, 32);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        return renderTexture;
    }

    // Update is called once per frame
    void Update() {
        int decayKernel = decay.FindKernel("CSMain");
        int computeKernel = compute.FindKernel("CSMain");

        if (swap == 0) {
            decay.SetTexture(decayKernel, "deposit_in", deposit_in);
            decay.SetTexture(decayKernel, "deposit_out", deposit_out);
            compute.SetTexture(computeKernel, "tex_deposit", deposit_out);
            swap = 1;
        } else {
            decay.SetTexture(decayKernel, "deposit_in", deposit_out);
            decay.SetTexture(decayKernel, "deposit_out", deposit_in);
            compute.SetTexture(computeKernel, "tex_deposit", deposit_in);
            swap = 0;
        }
        //mat.mainTexture = result;
        decay.Dispatch(decayKernel, 512 / 8, 512 / 8, 1);
        compute.Dispatch(computeKernel, 512 / 8, 512 / 8, 1);
        //mat.mainTexture = result;
        if (swap == 0)
        {
            mat.mainTexture = deposit_in;
        } else
        {
            mat.mainTexture = deposit_out;
        }
        //mat.mainTexture = result;
    }
}
