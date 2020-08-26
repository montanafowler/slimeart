
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ComputeHookup : MonoBehaviour
{ 
    public ComputeShader propegate;
    public ComputeShader decay;

    public ComputeBuffer initialGalaxyPositions;
    public ComputeBuffer particles_x;
    public ComputeBuffer particles_y;
    public ComputeBuffer particles_theta;
    public ComputeBuffer particles_weights;

    public RenderTexture result;
    public RenderTexture deposit_in;
    public RenderTexture deposit_out;
    public RenderTexture tex_trace;
    public Material mat;

    public float half_sense_spread; // default = 15-30 degrees
    public float sense_distance; // in world-space units; default = about 1/100 of the world 'cube' size
    public float sense_distance_divisor; // 100.0f
    public float turn_angle; // default = 15 degrees
    public float move_distance; // in world-space units; default = about 1/5--1/3 of sense_distance
    public float agent_deposit; // 0 for data-driven fitting, >0 for self-reinforcing behavior
    public int world_width; //
    public int world_height; // grid dimensions - note that the particle positions are also stored in the grid coordinates, but continuous
    public int world_depth; //
    public float move_sense_coef;
    public float normalization_factor;
    public float deposit_strength;

    public int pixelWidth;
    public int pixelHeight;
    public const float PI = 3.1415926535897931f;
    public int swap;

    public Vector2[] linePositions;

    private Slider moveDistanceSlider;
    private Slider senseDistanceSlider;
    private Slider depositStrengthSlider;

    //public Camera camera;
    // Start is called before the first frame update
    void Start()
    {
        // kernel is the propegate shader (initial spark)
        int propegateKernel = propegate.FindKernel("CSMain");
        
        pixelHeight = 512;
        pixelWidth = 512;//(int)(Camera.main.aspect * pixelHeight);

        // random seeding of arrays
        float[] xParticlePositions = new float[pixelWidth * pixelHeight];
        float[] yParticlePositions = new float[pixelWidth * pixelHeight];
        float[] thetaParticles = new float[pixelWidth * pixelHeight];
        float[] weightsParticles = new float[pixelWidth * pixelHeight];
        int index = 0;

        float increment5 = 512.0f / 5.0f;
        float increment4 = 512.0f / 4.0f;
        float[] xGalaxyCoordinates = { increment5, increment5 * 2, increment5 * 3, increment5 * 4,
                                       increment5, increment5 * 2, increment5 * 3, increment5 * 4,
                                       increment5, increment5 * 2, increment5 * 3, increment5 * 4};
        float[] yGalaxyCoordinates = { increment4, increment4, increment4, increment4, 
                                        increment4 * 2, increment4 * 2,increment4 * 2,increment4 * 2,
                                        increment4 * 3 , increment4 * 3, increment4 * 3, increment4 * 3 };



        for (int i = 0; i < pixelWidth; i++) {
            for (int j = 0; j < pixelHeight; j++) {
                xParticlePositions[index] = Random.Range(0.0f, (float)pixelWidth);// i / (512.0f);
                yParticlePositions[index] = Random.Range(0.0f, (float)pixelHeight);// j / (512.0f);
                thetaParticles[index] = Random.Range(0.0f, 2.0f * PI);
                weightsParticles[index] = 1.0f; // particle
                
                if (index < 12) {
                    xParticlePositions[index] = xGalaxyCoordinates[index];
                    yParticlePositions[index] = yGalaxyCoordinates[index];
                    weightsParticles[index] = 2.0f;
                } else if (index > pixelWidth * pixelHeight * 3 / 4) {
                    weightsParticles[index] = 0.0f; // particle
                }
                index++;
            }
        }
        
        // x particle positions
        particles_x = initializeComputeBuffer(xParticlePositions, "particles_x", propegateKernel);

        // y particle positions
        particles_y = initializeComputeBuffer(yParticlePositions, "particles_y", propegateKernel);

        // particles theta
        particles_theta = initializeComputeBuffer(thetaParticles, "particles_theta", propegateKernel);

        // particle weights
        particles_weights = initializeComputeBuffer(weightsParticles, "particle_weights", propegateKernel);

        // deposit texture for propegate shader
        //tex_deposit = initializeRenderTexture();
        setupVariables();

        // dispatch the texture
        propegate.Dispatch(propegateKernel, 512 / 8, 512 / 8, 1);

        deposit_out = initializeRenderTexture();

        swap = 0;

    }

    void setupVariables() {
        deposit_in = initializeRenderTexture();
        result = initializeRenderTexture();

        // trace texture for the propegate shader
        tex_trace = initializeRenderTexture();

        // other variables
        world_width = 512;
        world_height = 512;
        half_sense_spread = Random.Range(15.0f, 30.0f);
        sense_distance_divisor = 100.0f;
        turn_angle = 15.0f;
        move_distance = 0.001f;
        agent_deposit = 0.0001f;
        move_sense_coef = 1.0f;
        normalization_factor = 2.0f;

        moveDistanceSlider = GameObject.Find("MoveDistanceSlider").GetComponent<Slider>();
        senseDistanceSlider = GameObject.Find("SenseDistanceSlider").GetComponent<Slider>();
        depositStrengthSlider = GameObject.Find("DepositStrengthSlider").GetComponent<Slider>();

        move_distance = moveDistanceSlider.value;
        sense_distance = senseDistanceSlider.value;
        deposit_strength = depositStrengthSlider.value;

        updatePropegateShaderVariables(deposit_in);
    }

    ComputeBuffer initializeComputeBuffer(float[] arr, string shaderBufferName, int propegateKernel) {
        ComputeBuffer computeBuffer = new ComputeBuffer(arr.Length, sizeof(float));
        computeBuffer.SetData(arr);
        propegate.SetBuffer(propegateKernel, shaderBufferName, computeBuffer);
        return computeBuffer;
    }

    RenderTexture initializeRenderTexture() {
        RenderTexture renderTexture = new RenderTexture(pixelWidth, pixelHeight, 32);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        return renderTexture;
    }

    void updatePropegateShaderVariables(RenderTexture depositTexture) {
        sense_distance = senseDistanceSlider.value;
        move_distance = moveDistanceSlider.value;
        deposit_strength = depositStrengthSlider.value;
        int propegateKernel = propegate.FindKernel("CSMain");

        propegate.SetTexture(propegateKernel, "tex_deposit", depositTexture);
        propegate.SetTexture(propegateKernel, "tex_trace", tex_trace);
        propegate.SetFloat("half_sense_spread", half_sense_spread); // 15 to 30 degrees default
        propegate.SetFloat("sense_distance", sense_distance); // in world-space units; default = about 1/100 of the world 'cube' size
        propegate.SetFloat("turn_angle", turn_angle); // 15.0 is default
        propegate.SetFloat("move_distance", move_distance);//worldHeight / 100.0f / 4.0f); //  in world-space units; default = about 1/5--1/3 of sense_distance
        propegate.SetFloat("agent_deposit", agent_deposit); // 15.0 is default
        propegate.SetInt("world_width", world_width);
        propegate.SetInt("world_height", world_height);
        propegate.SetFloat("move_sense_coef", move_sense_coef); // ?
        propegate.SetFloat("normalization_factor", normalization_factor); // ?
        propegate.SetFloat("pixelWidth", pixelWidth);
        propegate.SetFloat("pixelHeight", pixelHeight);
        propegate.SetFloat("deposit_strength", deposit_strength);
        propegate.SetTexture(propegateKernel, "Result", result);
    }

    // Update is called once per frame
    void Update() {
        int decayKernel = decay.FindKernel("CSMain");
        int propegateKernel = propegate.FindKernel("CSMain");

        if (swap == 0) {
            decay.SetTexture(decayKernel, "deposit_in", deposit_in);
            decay.SetTexture(decayKernel, "deposit_out", deposit_out);
            updatePropegateShaderVariables(deposit_out);
            swap = 1;
        } else {
            decay.SetTexture(decayKernel, "deposit_in", deposit_out);
            decay.SetTexture(decayKernel, "deposit_out", deposit_in);
            updatePropegateShaderVariables(deposit_in);
            swap = 0;
        }

        decay.Dispatch(decayKernel, pixelWidth / 8, pixelHeight / 8, 1);
        propegate.Dispatch(propegateKernel, pixelWidth / 8, pixelHeight / 8, 1);

        if (swap == 0) { 
           // mat.mainTexture = deposit_in;
        } else {
          //  mat.mainTexture = deposit_out;
        }

        mat.mainTexture = result;

        /*if(Input.GetMouseButtonDown(0))
        {
            Debug.Log("mouse button down");
            linePositions = new Vector2[1000];
            linePositions[0] = new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, 
                Camera.main.ScreenToWorldPoint(Input.mousePosition).y);
        }

        if(Input.GetMouseButton(0))
        {
            Vector2 tempMousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Vector2.Distance(tempMousePosition, linePositions[fingerPositions.Count - 1]) > .1f) {

            }
        }*/
    }

    
}
