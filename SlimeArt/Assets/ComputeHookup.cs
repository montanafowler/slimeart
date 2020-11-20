﻿
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComputeHookup : MonoBehaviour
{ 
    public ComputeShader propegate;
    public ComputeShader decay;
    public ComputeShader blank_canvas_shader;

    public ComputeBuffer initialGalaxyPositions;
    public ComputeBuffer particles_x;
    public ComputeBuffer particles_y;
    public ComputeBuffer particles_theta;
    public ComputeBuffer data_types;
    public ComputeBuffer blank_canvas;

    public RenderTexture particle_render_texture;
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
    public float brush_size;

    public int pixelWidth;
    public int pixelHeight;
    public const float PI = 3.1415926535897931f;
    public int swap;

    public Vector2[] linePositions;

    private Slider moveDistanceSlider;
    private Slider senseDistanceSlider;
    private Slider depositStrengthSlider;
    private Slider agentDepositStrengthSlider;
    private Slider brushSizeSlider;

    private TextMeshProUGUI moveDistanceSliderText;
    private TextMeshProUGUI senseDistanceSliderText;
    private TextMeshProUGUI depositStrengthSliderText;
    private TextMeshProUGUI agentDepositStrengthSliderText;
    private TextMeshProUGUI brushSizeSliderText;

    private TMP_Dropdown modeDropdown;
    private TMP_Dropdown viewDropdown;
    private float OBSERVE_MODE = 0.0f;
    private float DRAW_DEPOSIT_MODE = 1.0f;
    private float DRAW_DEPOSIT_EMITTERS_MODE = 2.0f;
    private float DRAW_PARTICLES_MODE = 3.0f;
    private float PARTICLE_VIEW = 0.0f;
    private float DEPOSIT_VIEW = 1.0f;

    private float PARTICLE = 1.0f;
    private float DEPOSIT_EMITTER = 2.0f;
    private float DEPOSIT = 3.0f;
    private float NO_DATA = 0.0f;

    private int available_data_index = 0;
    private int MAX_SPACE;
    private int COMPUTE_GRID_WIDTH;
    private int COMPUTE_GRID_HEIGHT;


    //public Camera camera;
    // Start is called before the first frame update
    void Start() {
        // kernel is the propegate shader (initial spark)
        int propegateKernel = propegate.FindKernel("CSMain");

        pixelHeight = Screen.height;
        pixelWidth = Screen.width;//(int)(Camera.main.aspect * pixelHeight);

        //Debug.Log("pixelHeight " + pixelHeight);
        //Debug.Log("pixelWidth " + pixelWidth);
        MAX_SPACE = pixelHeight * pixelWidth * 5;   
        Debug.Log("MAX_SPACE " + MAX_SPACE);
        
        COMPUTE_GRID_HEIGHT = 256;
        COMPUTE_GRID_WIDTH = MAX_SPACE / COMPUTE_GRID_HEIGHT;

        //mat.mainTextureScale = new Vector2(0.9f, 1.0f);
        //mat.mainTextureOffset = new Vector2(-0.1f, 0.0f);

        // random seeding of arrays
        float[] xParticlePositions = new float[MAX_SPACE];
        float[] yParticlePositions = new float[MAX_SPACE];
        float[] thetaParticles = new float[MAX_SPACE];
        float[] dataTypes = new float[MAX_SPACE];
        float[] blankCanvas = new float[MAX_SPACE];
        int index = 0;

        bool firstNoData = true;

        for (int i = 0; i < pixelWidth; i++) {
            for (int j = 0; j < pixelHeight; j++) {
                xParticlePositions[index] = Random.Range(0.0f, (float)pixelWidth);// i / (512.0f);
                yParticlePositions[index] = Random.Range(0.0f, (float)pixelHeight);// j / (512.0f);
                thetaParticles[index] = Random.Range(0.0f, 2.0f * PI);
                dataTypes[index] = PARTICLE; // particle
                blankCanvas[index] = 0.0f;
                
                //if (index > pixelWidth * pixelHeight /* 3*/ / 4) {
                    if (firstNoData) {
                        firstNoData = false;
                        available_data_index = index;
                    }
                    dataTypes[index] = NO_DATA; // particle
                //}
                index++;
            }
        }
        
        // x particle positions
        particles_x = initializeComputeBuffer(xParticlePositions, "particles_x", propegateKernel);

        // y particle positions
        particles_y = initializeComputeBuffer(yParticlePositions, "particles_y", propegateKernel);

        // particles theta
        particles_theta = initializeComputeBuffer(thetaParticles, "particles_theta", propegateKernel);

        // data types, like if it is deposit emitter, particle, deposit, or no data
        data_types = initializeComputeBuffer(dataTypes, "data_types", propegateKernel);

        blank_canvas = initializeComputeBuffer(blankCanvas, "blank_canvas", blank_canvas_shader.FindKernel("CSMain"));
        
        // deposit texture for propegate shader
        //tex_deposit = initializeRenderTexture();
        setupVariables();

        blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
        blank_canvas_shader.Dispatch(blank_canvas_shader.FindKernel("CSMain"), COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);
        //mat.mainTexture = particle_render_texture;

        // dispatch the texture
        //propegate.Dispatch(propegateKernel, pixelWidth / 8, pixelHeight / 8, 1);
       // Change MAX SPACE TO BE NUM_OF_AGENTS

        propegate.Dispatch(propegateKernel, COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);


        swap = 0;

    }

    void setupVariables() {
        deposit_in = initializeRenderTexture();
        deposit_out = initializeRenderTexture();
        particle_render_texture = initializeRenderTexture();

        // trace texture for the propegate shader
        tex_trace = initializeRenderTexture();

        // other variables
        world_width = Screen.width;
        world_height = Screen.height;
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
        agentDepositStrengthSlider = GameObject.Find("AgentDepositStrengthSlider").GetComponent<Slider>();
        brushSizeSlider = GameObject.Find("BrushSizeSlider").GetComponent<Slider>();

        move_distance = moveDistanceSlider.value;
        sense_distance = senseDistanceSlider.value;
        deposit_strength = depositStrengthSlider.value;
        agent_deposit = agentDepositStrengthSlider.value;
        brush_size = brushSizeSlider.value;

        moveDistanceSliderText = GameObject.Find("MoveDistanceSliderText").GetComponent<TextMeshProUGUI>();
        moveDistanceSlider.onValueChanged.AddListener(delegate { updateSliderLabel(moveDistanceSliderText, "move distance: ", moveDistanceSlider.value); });
        updateSliderLabel(moveDistanceSliderText, "move distance: ", moveDistanceSlider.value);

        senseDistanceSliderText = GameObject.Find("SenseDistanceSliderText").GetComponent<TextMeshProUGUI>();
        senseDistanceSlider.onValueChanged.AddListener(delegate { updateSliderLabel(senseDistanceSliderText, "sense distance: ", senseDistanceSlider.value); });
        updateSliderLabel(senseDistanceSliderText, "sense distance: ", senseDistanceSlider.value);

        depositStrengthSliderText = GameObject.Find("DepositStrengthSliderText").GetComponent<TextMeshProUGUI>();
        depositStrengthSlider.onValueChanged.AddListener(delegate { updateSliderLabel(depositStrengthSliderText, "deposit strength: ", depositStrengthSlider.value); });
        updateSliderLabel(depositStrengthSliderText, "deposit strength: ", depositStrengthSlider.value);

        agentDepositStrengthSliderText = GameObject.Find("AgentDepositStrengthSliderText").GetComponent<TextMeshProUGUI>();
        agentDepositStrengthSlider.onValueChanged.AddListener(delegate { updateSliderLabel(agentDepositStrengthSliderText, "agent deposit strength: ", agentDepositStrengthSlider.value); });
        updateSliderLabel(agentDepositStrengthSliderText, "agent deposit strength: ", agentDepositStrengthSlider.value);

        brushSizeSliderText = GameObject.Find("BrushSizeSliderText").GetComponent<TextMeshProUGUI>();
        brushSizeSlider.onValueChanged.AddListener(delegate { updateSliderLabel(brushSizeSliderText, "brush size: ", brushSizeSlider.value); });
        updateSliderLabel(brushSizeSliderText, "brush size: ", agentDepositStrengthSlider.value);


        modeDropdown = GameObject.Find("ModeDropdown").GetComponent<TMP_Dropdown>();
        //modeDropdown.onValueChanged.AddListener(delegate { changeMode(modeDropdown.value);  });

        viewDropdown = GameObject.Find("ViewDropdown").GetComponent<TMP_Dropdown>();
        //viewDropdown.onValueChanged.AddListener(delegate { changeMode(viewDropdown.value); });

        Button playButton = GameObject.Find("PlayButton").GetComponent<Button>();
        //Debug.Log(playButton);
        updatePropegateShaderVariables(deposit_in);
    }

    

    public void updateSliderLabel(TextMeshProUGUI label, string labelText, float value) {
        label.SetText(labelText + value.ToString());
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
        agent_deposit = agentDepositStrengthSlider.value;

        int propegateKernel = propegate.FindKernel("CSMain");

        propegate.SetTexture(propegateKernel, "tex_deposit", depositTexture);
        propegate.SetTexture(propegateKernel, "tex_trace", tex_trace);
        propegate.SetFloat("half_sense_spread", half_sense_spread); // 15 to 30 degrees default
        propegate.SetFloat("sense_distance", sense_distance); // in world-space units; default = about 1/100 of the world 'cube' size
        propegate.SetFloat("turn_angle", turn_angle); // 15.0 is default
        propegate.SetFloat("move_distance", move_distance);//worldHeight / 100.0f / 4.0f); //  in world-space units; default = about 1/5--1/3 of sense_distance
        propegate.SetFloat("agent_deposit", agent_deposit); // 15.0 is default
        propegate.SetFloat("world_width", (float)world_width);
        propegate.SetFloat("world_height", (float)world_height);
        propegate.SetFloat("move_sense_coef", move_sense_coef); // ?
        propegate.SetFloat("normalization_factor", normalization_factor); // ?
        propegate.SetFloat("pixelWidth", pixelWidth);
        propegate.SetFloat("pixelHeight", pixelHeight);
        propegate.SetFloat("deposit_strength", deposit_strength);
        propegate.SetTexture(propegateKernel, "particle_render_texture", particle_render_texture);
        propegate.SetFloat("COMPUTE_GRID_WIDTH", (float)COMPUTE_GRID_WIDTH);
        propegate.SetFloat("COMPUTE_GRID_HEIGHT", (float)COMPUTE_GRID_HEIGHT);
    }

    int getNextAvailableIndex() {
        return 0;
    }

    void draw(float x, float y) {
        float centerX = pixelWidth - x -100;// + (mat.mainTextureOffset.x * pixelWidth * mat.mainTextureScale.x);
        float centerY = pixelHeight - y;
        if (modeDropdown.value != OBSERVE_MODE 
            && available_data_index < MAX_SPACE
            && available_data_index < MAX_SPACE
            //&& (centerX < (pixelWidth /*+ mat.mainTextureOffset.x * pixelWidth*/))
            )  {
            float[] particlesX = new float[MAX_SPACE]; 
            float[] particlesY = new float[MAX_SPACE]; 
            //float[] particlesTheta = new float[MAX_SPACE];
            float[] dataTypes = new float[MAX_SPACE]; 
            particles_x.GetData(particlesX);
            particles_y.GetData(particlesY);
            data_types.GetData(dataTypes);
            //particles_theta.GetData(particlesTheta);

            //float centerX = pixelWidth - x - (mat.mainTextureOffset.x * pixelWidth);
            //float centerY = pixelHeight - y;
            float newX, newY;
            brush_size = (brushSizeSlider.value + 1)/2;
            for (int dx = (int)-brush_size; dx < (int)brush_size; dx++) {
                for(int dy = (int)-brush_size; dy < (int)brush_size; dy++) {
                    newX = centerX + dx;
                    newY = centerY + dy;

                    if (available_data_index >= MAX_SPACE) {
                        Debug.Log("MAX SPACE REACHED");
                        break;
                    }

                    if ((newX-centerX)*(newX-centerX) 
                        + (newY-centerY)*(newY-centerY) < brush_size*brush_size) {
                        particlesX[available_data_index] = centerX + dx;
                        particlesY[available_data_index] = centerY + dy;
                        if (modeDropdown.value == DRAW_DEPOSIT_MODE)
                        {
                            // draw temporary deposit that dissolves
                            dataTypes[available_data_index] = DEPOSIT;
                        }
                        else if (modeDropdown.value == DRAW_DEPOSIT_EMITTERS_MODE)
                        {
                            // draw deposit emitters that continuously emit deposit
                            dataTypes[available_data_index] = DEPOSIT_EMITTER;
                        }
                        else if (modeDropdown.value == DRAW_PARTICLES_MODE)
                        {
                            // draw particles
                            dataTypes[available_data_index] = PARTICLE;
                        }
                        available_data_index++;
                    }
                    
                }
                if (available_data_index >= MAX_SPACE) {
                    Debug.Log("MAX SPACE REACHED");
                    break;
                }
            }
 
            //particlesX[available_data_index] = pixelWidth - x;
            //particlesY[available_data_index] = pixelHeight - y;

            
            int propegateKernel = propegate.FindKernel("CSMain");

            // x particle positions
            particles_x = initializeComputeBuffer(particlesX, "particles_x", propegateKernel);

            // y particle positions
            particles_y = initializeComputeBuffer(particlesY, "particles_y", propegateKernel);

            // data types, like if it is deposit emitter, particle, deposit, or no data
            data_types = initializeComputeBuffer(dataTypes, "data_types", propegateKernel);
        }
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown("escape")) {
            Debug.Log("quit");
            Application.Quit(); // Quits the game
        }

        int decayKernel = decay.FindKernel("CSMain");
        int propegateKernel = propegate.FindKernel("CSMain");
        int intPixWidth = (int)pixelWidth;
        int intPixHeight = (int)pixelHeight;
        decay.SetInt("pixelWidth", (int)pixelWidth);
        decay.SetInt("pixelHeight", (int)pixelHeight);

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
        //blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
        blank_canvas_shader.Dispatch(blank_canvas_shader.FindKernel("CSMain"), COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

        decay.Dispatch(decayKernel, COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);
        propegate.Dispatch(propegateKernel,COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

        if (viewDropdown.value == DEPOSIT_VIEW) {
            if (swap == 0) {
                mat.mainTexture = deposit_in;
            } else {
                mat.mainTexture = deposit_out;
            }
        } 
        
        if (viewDropdown.value == PARTICLE_VIEW) {
            mat.mainTexture = particle_render_texture;
        }

        if (Input.GetMouseButton(0)) {
            //Debug.Log("mouse button down " + Input.mousePosition);
            //Debug.Log("Screen Width : " + Screen.width);
            //Debug.Log("Screen Height : " + Screen.height);
            draw(Input.mousePosition.x, Input.mousePosition.y);   
        }
    }
}
