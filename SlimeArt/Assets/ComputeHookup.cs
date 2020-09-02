
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComputeHookup : MonoBehaviour
{ 
    public ComputeShader propegate;
    public ComputeShader decay;

    public ComputeBuffer initialGalaxyPositions;
    public ComputeBuffer particles_x;
    public ComputeBuffer particles_y;
    public ComputeBuffer particles_theta;
    public ComputeBuffer data_types;

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

    public int pixelWidth;
    public int pixelHeight;
    public const float PI = 3.1415926535897931f;
    public int swap;

    public Vector2[] linePositions;

    private Slider moveDistanceSlider;
    private Slider senseDistanceSlider;
    private Slider depositStrengthSlider;
    private Slider agentDepositStrengthSlider;

    private TextMeshProUGUI moveDistanceSliderText;
    private TextMeshProUGUI senseDistanceSliderText;
    private TextMeshProUGUI depositStrengthSliderText;
    private TextMeshProUGUI agentDepositStrengthSliderText;

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


    //public Camera camera;
    // Start is called before the first frame update
    void Start() {
        // kernel is the propegate shader (initial spark)
        int propegateKernel = propegate.FindKernel("CSMain");
        
        pixelHeight = Screen.height;
        pixelWidth = Screen.width;//(int)(Camera.main.aspect * pixelHeight);

        // random seeding of arrays
        float[] xParticlePositions = new float[pixelWidth * pixelHeight];
        float[] yParticlePositions = new float[pixelWidth * pixelHeight];
        float[] thetaParticles = new float[pixelWidth * pixelHeight];
        float[] dataTypes = new float[pixelWidth * pixelHeight];
        int index = 0;

        float increment5 = pixelHeight / 5.0f;
        float increment4 = pixelHeight / 4.0f;
        float[] xGalaxyCoordinates = { increment5, increment5 * 2, increment5 * 3, increment5 * 4,
                                       increment5, increment5 * 2, increment5 * 3, increment5 * 4,
                                       increment5, increment5 * 2, increment5 * 3, increment5 * 4};
        float[] yGalaxyCoordinates = { increment4, increment4, increment4, increment4, 
                                        increment4 * 2, increment4 * 2,increment4 * 2,increment4 * 2,
                                        increment4 * 3 , increment4 * 3, increment4 * 3, increment4 * 3 };

        bool firstNoData = true;

        for (int i = 0; i < pixelWidth; i++) {
            for (int j = 0; j < pixelHeight; j++) {
                xParticlePositions[index] = Random.Range(0.0f, (float)pixelWidth);// i / (512.0f);
                yParticlePositions[index] = Random.Range(0.0f, (float)pixelHeight);// j / (512.0f);
                thetaParticles[index] = Random.Range(0.0f, 2.0f * PI);
                dataTypes[index] = PARTICLE; // particle
                
                if (index < 12) {
                    xParticlePositions[index] = xGalaxyCoordinates[index];
                    yParticlePositions[index] = yGalaxyCoordinates[index];
                    dataTypes[index] = DEPOSIT_EMITTER;
                } else if (index > pixelWidth * pixelHeight * 3 / 4) {
                    if (firstNoData) {
                        firstNoData = false;
                        available_data_index = index;
                    }
                    dataTypes[index] = NO_DATA; // particle
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

        // data types, like if it is deposit emitter, particle, deposit, or no data
        data_types = initializeComputeBuffer(dataTypes, "data_types", propegateKernel);

        // deposit texture for propegate shader
        //tex_deposit = initializeRenderTexture();
        setupVariables();

        // dispatch the texture
        propegate.Dispatch(propegateKernel, pixelWidth / 8, pixelHeight / 8, 1);

        deposit_out = initializeRenderTexture();

        swap = 0;

    }

    void setupVariables() {
        deposit_in = initializeRenderTexture();
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

        move_distance = moveDistanceSlider.value;
        sense_distance = senseDistanceSlider.value;
        deposit_strength = depositStrengthSlider.value;
        agent_deposit = agentDepositStrengthSlider.value;

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

        modeDropdown = GameObject.Find("ModeDropdown").GetComponent<TMP_Dropdown>();
        //modeDropdown.onValueChanged.AddListener(delegate { changeMode(modeDropdown.value);  });

        viewDropdown = GameObject.Find("ViewDropdown").GetComponent<TMP_Dropdown>();
        viewDropdown.onValueChanged.AddListener(delegate { changeMode(viewDropdown.value); });

        Button playButton = GameObject.Find("PlayButton").GetComponent<Button>();
        //Debug.Log(playButton);
        updatePropegateShaderVariables(deposit_in);
    }

    private void changeMode(float value) {
        if (value == PARTICLE_VIEW) {
            mat.mainTexture = particle_render_texture;
        } else if (value == DEPOSIT_VIEW) {

        }
        Debug.Log("change mode " + value);
        Debug.Log("PARTICLE_VIEW " + PARTICLE_VIEW);
        Debug.Log("Desposit view " + DEPOSIT_VIEW);
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
        propegate.SetInt("world_width", world_width);
        propegate.SetInt("world_height", world_height);
        propegate.SetFloat("move_sense_coef", move_sense_coef); // ?
        propegate.SetFloat("normalization_factor", normalization_factor); // ?
        propegate.SetFloat("pixelWidth", pixelWidth);
        propegate.SetFloat("pixelHeight", pixelHeight);
        propegate.SetFloat("deposit_strength", deposit_strength);
        propegate.SetTexture(propegateKernel, "particle_render_texture", particle_render_texture);
    }

    void draw(float x, float y) {
        if (modeDropdown.value != OBSERVE_MODE && available_data_index < pixelHeight * pixelWidth) {
            //Debug.Log("draw in " + )
            float[] particlesX = new float[pixelWidth * pixelHeight]; 
            float[] particlesY = new float[pixelWidth * pixelHeight]; 
            //float[] particlesTheta = new float[pixelWidth * pixelHeight];
            float[] dataTypes = new float[pixelWidth * pixelHeight]; 
            particles_x.GetData(particlesX);
            particles_y.GetData(particlesY);
           //particles_theta.GetData(particlesTheta);
            data_types.GetData(dataTypes);

            particlesX[available_data_index] = x;
            particlesY[available_data_index] = y;
            Debug.Log("particlesX[available_data_index]" + particlesX[available_data_index]);
            Debug.Log("particlesY[available_data_index]" + particlesY[available_data_index]);


            if (modeDropdown.value == DRAW_DEPOSIT_MODE)  {
                // draw temporary deposit that dissolves
                dataTypes[available_data_index] = DEPOSIT;
            } else if (modeDropdown.value == DRAW_DEPOSIT_EMITTERS_MODE) {
                // draw deposit emitters that continuously emit deposit
                dataTypes[available_data_index] = DEPOSIT_EMITTER;
            } else if (modeDropdown.value == DRAW_PARTICLES_MODE) {
                // draw particles
                dataTypes[available_data_index] = PARTICLE;
            }
            available_data_index++;
            Debug.Log("dataTypes" + particlesX[available_data_index]);
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
