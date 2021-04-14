
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HSVPicker;

public class ComputeHookup : MonoBehaviour
{ 
    public ComputeShader propagate;
    public ComputeShader decay;
    public ComputeShader blank_canvas_shader;

    public ComputeBuffer initialGalaxyPositions;
    public ComputeBuffer particles_x;
    public ComputeBuffer particles_y;
    public ComputeBuffer particles_theta;
    public ComputeBuffer data_types;
    public ComputeBuffer blank_canvas;
    public ComputeBuffer particle_id_buffer;
    public ComputeBuffer move_distance_buffer;
    public ComputeBuffer sense_distance_buffer;
    public ComputeBuffer particle_deposit_strength_buffer;
    public ComputeBuffer lifetime_buffer;
    public ComputeBuffer particle_red_channel_buffer;
    public ComputeBuffer particle_green_channel_buffer;
    public ComputeBuffer particle_blue_channel_buffer;
    public ComputeBuffer attracted_to_buffer;
    public ComputeBuffer repelled_by_buffer;

    public ComputeBuffer x_y_theta_dataType_buffer;
    public ComputeBuffer moveDist_SenseDist_particleDepositStrength_lifetime_buffer;
    public ComputeBuffer red_green_blue_alpha_buffer;
    public ComputeBuffer turn_sense_angles_buffer;

    public RenderTexture particle_render_texture;
    public RenderTexture deposit_in;
    public RenderTexture deposit_out;
    public RenderTexture tex_trace_in;
    public RenderTexture tex_trace_out;
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
    private Slider scaleSlider;
    private Slider depositStrengthSlider;
    private Slider agentDepositStrengthSlider;
    private Slider brushSizeSlider;
    private Slider brushDensitySlider;
    private Slider lifetimeSlider;
    

    private TextMeshProUGUI moveDistanceSliderText;
    private TextMeshProUGUI scaleSliderText;
    private TextMeshProUGUI depositStrengthSliderText;
    private TextMeshProUGUI agentDepositStrengthSliderText;
    private TextMeshProUGUI brushSizeSliderText;
    private TextMeshProUGUI brushDensitySliderText;
    private TextMeshProUGUI lifetimeSliderText;
    private TextMeshProUGUI particleRedChannelSliderText;
    private TextMeshProUGUI particleGreenChannelSliderText;
    private TextMeshProUGUI particleBlueChannelSliderText;
    private TextMeshProUGUI particleAlphaChannelSliderText;
    private ColorPicker colorPicker;

    //test ones
    private TextMeshProUGUI moveDistanceSliderTestText;
    private TextMeshProUGUI senseDistanceSliderText;
    private TextMeshProUGUI turnAngleSliderText;
    private TextMeshProUGUI senseAngleSliderText;
    private Slider moveDistanceSliderTest;
    private Slider senseDistanceSlider;
    private Slider turnAngleSlider;
    private Slider senseAngleSlider;

    private TMP_Dropdown modeDropdown;
    private TMP_Dropdown viewDropdown;
    private float OBSERVE_MODE = 2.0f;
    private float DRAW_DEPOSIT_MODE = 3.0f;
    private float DRAW_DEPOSIT_EMITTERS_MODE = 1.0f;
    private float DRAW_PARTICLES_MODE = 0.0f;

    private float PARTICLE_VIEW = 1.0f;
    private float DEPOSIT_VIEW = 2.0f;
    private float TRACE_VIEW = 0.0f;

    public float LOW_QUALITY_GRAPHICS = 0.0f;
    public float MED_QUALITY_GRAPHICS = 1.0f;
    public float HIGH_QUALITY_GRAPHICS = 2.0f;
    public float ULTRA_QUALITY_GRAPHICS = 3.0f;

    private float PARTICLE = 1.0f;
    private float DEPOSIT_EMITTER = 2.0f;
    private float DEPOSIT = 3.0f;
    private float NO_DATA = 0.0f;

    private int available_data_index = 0;
    private int MAX_SPACE;
    private int MAX_NUM_PARTICLES;
    private int COMPUTE_GRID_WIDTH;
    private int COMPUTE_GRID_HEIGHT;

    private const int LINEAR_SPACE_MANAGEMENT = 0;
    private const int GROUP_THEORY_SPACE_MANAGEMENT = 1;
    private const int STOCHASTIC_SPACE_MANAGEMENT = 2;

    //private readonly System.Random random = new System.Random();
    private int group_theory_increment;
    private int group_theory_index = 0;
    private int propagateKernel;

    //public bool QUALITY_CHOSEN = false;
    public float SAVED_QUALITY;

    //values to adjust scale and speed sliders
    private const int SCALE = 0;
    private const int SPEED = 1;

    //public Camera camera;
    // Start is called before the first frame update
    void Start() {
        Debug.Log("Chosen Quality Level canvas " + GraphicsQualityMenu.CHOSEN_QUALITY_LEVEL);
        SAVED_QUALITY = GraphicsQualityMenu.CHOSEN_QUALITY_LEVEL;
        //GraphicsQualityMenu.QUALITY_MENU_GAME_OBJECT.SetActive(false);
        // kernel is the propagate shader (initial spark)
        propagateKernel = propagate.FindKernel("CSMain");
        pixelHeight = Screen.height;
        pixelWidth = Screen.width;

        if (SAVED_QUALITY == 0.0f) {
            MAX_SPACE = 100000;
        }

        if (SAVED_QUALITY == 1.0f) {
            MAX_SPACE = 1000000;
        }

        if (SAVED_QUALITY == 2.0f)
        {
            MAX_SPACE = 5000000;
        }

        //MAX_SPACE = 100000;//pixelHeight * pixelWidth * 5;
        MAX_NUM_PARTICLES = MAX_SPACE / 4;

        Debug.Log("MAX_SPACE " + MAX_SPACE);

        COMPUTE_GRID_HEIGHT = 256;
        COMPUTE_GRID_WIDTH = MAX_SPACE / COMPUTE_GRID_HEIGHT;
        mat.mainTextureOffset = new Vector2(0.0f, 0.0f);

        calculateGroupTheoryIncrement();
        setupBuffers(); // sets up the buffers with their info.
        setupUI();

        blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
        blank_canvas_shader.Dispatch(blank_canvas_shader.FindKernel("CSMain"), COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

    }

    void setUpCanvas() {
        // kernel is the propagate shader (initial spark)
        propagateKernel = propagate.FindKernel("CSMain");
        pixelHeight = Screen.height;
        pixelWidth = Screen.width;

        TMP_Dropdown modeDropdown = GameObject.Find("GraphicsQualityDropdown").GetComponent<TMP_Dropdown>();

        MAX_SPACE = 100000;//pixelHeight * pixelWidth * 5;
        MAX_NUM_PARTICLES = MAX_SPACE / 4;

        Debug.Log("MAX_SPACE " + MAX_SPACE);

        COMPUTE_GRID_HEIGHT = 256;
        COMPUTE_GRID_WIDTH = MAX_SPACE / COMPUTE_GRID_HEIGHT;
        mat.mainTextureOffset = new Vector2(0.0f, 0.0f);

        calculateGroupTheoryIncrement();
        setupBuffers(); // sets up the buffers with their info.
        setupUI();

        blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
        blank_canvas_shader.Dispatch(blank_canvas_shader.FindKernel("CSMain"), COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

        // dispatch the texture
        propagate.Dispatch(propagateKernel, COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);
        swap = 0; // alternating copying
    }

    void setupBuffers() {
        // random seeding of arrays
        float[] xParticlePositions = new float[MAX_SPACE];
        float[] yParticlePositions = new float[MAX_SPACE];
        float[] thetaParticles = new float[MAX_SPACE];
        float[] dataTypes = new float[MAX_SPACE];
        float[] blankCanvas = new float[MAX_SPACE];
        float[] moveDistanceBuffer = new float[MAX_SPACE];
        string[] particleIdBuffer = new string[MAX_SPACE];
        float[] senseDistanceBuffer = new float[MAX_SPACE];
        float[] particleDepositStrengthBuffer = new float[MAX_SPACE];
        float[] lifetimeBuffer = new float[MAX_SPACE];
        float[] particleRedChannelBuffer = new float[MAX_SPACE];
        float[] particleGreenChannelBuffer = new float[MAX_SPACE];
        float[] particleBlueChannelBuffer = new float[MAX_SPACE];
        string[] attractedToBuffer = new string[MAX_SPACE];
        string[] repelledByBuffer = new string[MAX_SPACE];

        float[] x_y_theta_dataType_array = new float[MAX_SPACE];
        float[] moveDist_SenseDist_particleDepositStrength_lifetime_array = new float[MAX_SPACE];
        float[] red_green_blue_alpha_array = new float[MAX_SPACE];
        float[] turn_sense_angles_array = new float[MAX_SPACE];
        //attractedTo
        //repelledBy

        // x particle positions
        //particles_x = initializeComputeBuffer(xParticlePositions, "particles_x", propagateKernel);

        // y particle positions
        //particles_y = initializeComputeBuffer(yParticlePositions, "particles_y", propagateKernel);

        // particles theta
        //particles_theta = initializeComputeBuffer(thetaParticles, "particles_theta", propagateKernel);

        // data types, like if it is deposit emitter, particle, deposit, or no data
        //data_types = initializeComputeBuffer(dataTypes, "data_types", propagateKernel);

        //x,y,theta,data
        x_y_theta_dataType_buffer = initializeComputeBuffer(x_y_theta_dataType_array, "x_y_theta_dataType", propagateKernel);

        //moveDist,senseDist,particleDepositStrength,lifetime
        moveDist_SenseDist_particleDepositStrength_lifetime_buffer = initializeComputeBuffer(moveDist_SenseDist_particleDepositStrength_lifetime_array, 
            "moveDist_SenseDist_particleDepositStrength_lifetime", propagateKernel);

        //red,green,blue,alpha
        red_green_blue_alpha_buffer = initializeComputeBuffer(red_green_blue_alpha_array, "red_green_blue_alpha", propagateKernel);

        turn_sense_angles_buffer = initializeComputeBuffer(turn_sense_angles_array, "turn_sense_angles", propagateKernel);

        blank_canvas = initializeComputeBuffer(blankCanvas, "blank_canvas", blank_canvas_shader.FindKernel("CSMain"));

        // deposit texture for propagate shader
        //tex_deposit = initializeRenderTexture();
        deposit_in = initializeRenderTexture();
        deposit_out = initializeRenderTexture();
        particle_render_texture = initializeRenderTexture();

        // trace texture for the propagate shader
        tex_trace_in = initializeRenderTexture();
        tex_trace_out = initializeRenderTexture();
    }

    void setupUI() {
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
        scaleSlider = GameObject.Find("ScaleSlider").GetComponent<Slider>();
        depositStrengthSlider = GameObject.Find("DepositStrengthSlider").GetComponent<Slider>();
        agentDepositStrengthSlider = GameObject.Find("AgentDepositStrengthSlider").GetComponent<Slider>();
        brushSizeSlider = GameObject.Find("BrushSizeSlider").GetComponent<Slider>();
        //brushDensitySlider = GameObject.Find("BrushDensitySlider").GetComponent<Slider>();
        //lifetimeSlider = GameObject.Find("ParticleLifetimeSlider").GetComponent<Slider>();
        colorPicker = GameObject.Find("Picker").GetComponent<ColorPicker>();
        moveDistanceSliderTest = GameObject.Find("MoveDistanceSliderTest").GetComponent<Slider>();
        turnAngleSlider = GameObject.Find("TurnAngleSlider").GetComponent<Slider>();
        senseAngleSlider = GameObject.Find("SenseAngleSlider").GetComponent<Slider>();
        senseDistanceSlider = GameObject.Find("SenseDistanceSlider").GetComponent<Slider>();

        move_distance = moveDistanceSlider.value;
        sense_distance = scaleSlider.value;
        //deposit_strength = depositStrengthSlider.value;
        //agent_deposit = agentDepositStrengthSlider.value;
        brush_size = brushSizeSlider.value;

        moveDistanceSliderText = GameObject.Find("MoveDistanceSliderText").GetComponent<TextMeshProUGUI>();
        moveDistanceSlider.onValueChanged.AddListener(delegate { updateSliderLabel(moveDistanceSliderText, "speed: ", moveDistanceSlider.value); });
        updateSliderLabel(moveDistanceSliderText, "speed: ", moveDistanceSlider.value);
        
        scaleSliderText = GameObject.Find("ScaleSliderText").GetComponent<TextMeshProUGUI>();
        scaleSlider.onValueChanged.AddListener(delegate { updateSliderLabel(scaleSliderText, "scale: ", scaleSlider.value); });
        updateSliderLabel(scaleSliderText, "scale: ", scaleSlider.value);

        depositStrengthSliderText = GameObject.Find("DepositStrengthSliderText").GetComponent<TextMeshProUGUI>();
        depositStrengthSlider.onValueChanged.AddListener(delegate { updateSliderLabel(depositStrengthSliderText, "deposit strength: ", depositStrengthSlider.value); });
        updateSliderLabel(depositStrengthSliderText, "deposit strength: ", depositStrengthSlider.value);

        //agentDepositStrengthSliderText = GameObject.Find("AgentDepositStrengthSliderText").GetComponent<TextMeshProUGUI>();
        //agentDepositStrengthSlider.onValueChanged.AddListener(delegate { updateSliderLabel(agentDepositStrengthSliderText, "agent deposit strength: ", agentDepositStrengthSlider.value); });
        //updateSliderLabel(agentDepositStrengthSliderText, "agent deposit strength: ", agentDepositStrengthSlider.value);

        brushSizeSliderText = GameObject.Find("BrushSizeSliderText").GetComponent<TextMeshProUGUI>();
        brushSizeSlider.onValueChanged.AddListener(delegate { updateSliderLabel(brushSizeSliderText, "brush size: ", brushSizeSlider.value); });
        updateSliderLabel(brushSizeSliderText, "brush size: ", brushSizeSlider.value);

        //brushDensitySliderText = GameObject.Find("BrushDensitySliderText").GetComponent<TextMeshProUGUI>();
        //brushDensitySlider.onValueChanged.AddListener(delegate { updateSliderLabel(brushDensitySliderText, "brush density: ", brushDensitySlider.value); });
        //updateSliderLabel(brushDensitySliderText, "brush density: ", brushDensitySlider.value);

        //lifetimeSliderText = GameObject.Find("ParticleLifetimeSliderText").GetComponent<TextMeshProUGUI>();
        //lifetimeSlider.onValueChanged.AddListener(delegate { updateSliderLabel(lifetimeSliderText, "Particle Lifetime: ", lifetimeSlider.value); });
        //updateSliderLabel(lifetimeSliderText, "Particle Lifetime: ", lifetimeSlider.value);


        //TEST SLIDERS TRYING TO FIND GOOD VALUES
        turnAngleSliderText = GameObject.Find("TurnAngleSliderText").GetComponent<TextMeshProUGUI>();
        turnAngleSlider.onValueChanged.AddListener(delegate { updateSliderLabel(turnAngleSliderText, "test turn angle: ", turnAngleSlider.value); });
        updateSliderLabel(turnAngleSliderText, "test turn angle: ", turnAngleSlider.value);

        senseAngleSliderText = GameObject.Find("SenseAngleSliderText").GetComponent<TextMeshProUGUI>();
        senseAngleSlider.onValueChanged.AddListener(delegate { updateSliderLabel(senseAngleSliderText, "test sense angle: ", senseAngleSlider.value); });
        updateSliderLabel(senseAngleSliderText, "test sense angle: ", senseAngleSlider.value);

        moveDistanceSliderTestText = GameObject.Find("MoveDistanceSliderTestText").GetComponent<TextMeshProUGUI>();
        moveDistanceSliderTest.onValueChanged.AddListener(delegate { updateSliderLabel(moveDistanceSliderTestText, "test move distance: ", moveDistanceSliderTest.value); });
        updateSliderLabel(moveDistanceSliderTestText, "test move distance: ", moveDistanceSliderTest.value);

        senseDistanceSliderText = GameObject.Find("SenseDistanceSliderText").GetComponent<TextMeshProUGUI>();
        senseDistanceSlider.onValueChanged.AddListener(delegate { updateSliderLabel(senseDistanceSliderText, "test sense distance: ", senseDistanceSlider.value); });
        updateSliderLabel(senseDistanceSliderText, "test sense distance: ", senseDistanceSlider.value);

        modeDropdown = GameObject.Find("ModeDropdown").GetComponent<TMP_Dropdown>();
        //modeDropdown.onValueChanged.AddListener(delegate { changeMode(modeDropdown.value);  });

        viewDropdown = GameObject.Find("ViewDropdown").GetComponent<TMP_Dropdown>();
        //viewDropdown.onValueChanged.AddListener(delegate { changeMode(viewDropdown.value); });

        //Button playButton = GameObject.Find("PlayButton").GetComponent<Button>();
        Button clearCanvasButton = GameObject.Find("ClearCanvasButton").GetComponent<Button>();
        clearCanvasButton.onClick.AddListener(delegate { Debug.Log("clear"); setupBuffers(); /*updatepropagateShaderVariables(deposit_in);*/ });
    }

    

    void calculateGroupTheoryIncrement() {
        group_theory_increment = 3;
        while (MAX_SPACE % group_theory_increment == 0) {
            group_theory_increment++;
        }
        //Debug.Log("group_theory_increment " + group_theory_increment);
    }

    public void updateSliderLabel(TextMeshProUGUI label, string labelText, float value) {
        label.SetText(labelText + value.ToString());
    }

    ComputeBuffer initializeComputeBuffer(float[] arr, string shaderBufferName, int propagateKernel) {
        ComputeBuffer computeBuffer = new ComputeBuffer(arr.Length, sizeof(float));
        computeBuffer.SetData(arr);
        propagate.SetBuffer(propagateKernel, shaderBufferName, computeBuffer);
        return computeBuffer;
    }

    RenderTexture initializeRenderTexture() {
        RenderTexture renderTexture = new RenderTexture(pixelWidth, pixelHeight, 32);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        return renderTexture;
    }

    void updatepropagateShaderVariables(RenderTexture depositTexture, RenderTexture traceTexture) {
        sense_distance = scaleSlider.value;
        move_distance = moveDistanceSlider.value;
        deposit_strength = depositStrengthSlider.value;
        //agent_deposit = agentDepositStrengthSlider.value;

        int propagateKernel = propagate.FindKernel("CSMain");

        propagate.SetTexture(propagateKernel, "tex_deposit", depositTexture);
        propagate.SetTexture(propagateKernel, "tex_trace", traceTexture);
        propagate.SetFloat("half_sense_spread", scaleSlider.value * (90.0f-20.0f) + 20.0f); // 15 to 30 degrees default
                                                                                            //  propagate.SetFloat("sense_distance", sense_distance); // in world-space units; default = about 1/100 of the world 'cube' size
        propagate.SetFloat("turn_angle", (1.0f - scaleSlider.value) * (45.0f - 10.0f) + 10.0f) ; // 15.0 is default
      //  propagate.SetFloat("move_distance", move_distance);//worldHeight / 100.0f / 4.0f); //  in world-space units; default = about 1/5--1/3 of sense_distance
       // propagate.SetFloat("agent_deposit", agent_deposit); // 15.0 is default
        propagate.SetFloat("world_width", (float)world_width);
        propagate.SetFloat("world_height", (float)world_height);
        propagate.SetFloat("move_sense_coef", move_sense_coef); // ?
        propagate.SetFloat("normalization_factor", normalization_factor); // ?
        propagate.SetFloat("pixelWidth", pixelWidth);
        propagate.SetFloat("pixelHeight", pixelHeight);
        propagate.SetFloat("deposit_strength", deposit_strength);
        propagate.SetTexture(propagateKernel, "particle_render_texture", particle_render_texture);
        propagate.SetFloat("COMPUTE_GRID_WIDTH", (float)COMPUTE_GRID_WIDTH);
        propagate.SetFloat("COMPUTE_GRID_HEIGHT", (float)COMPUTE_GRID_HEIGHT);
    }

    int getNextAvailableIndex() {
        int spaceManagement = STOCHASTIC_SPACE_MANAGEMENT;
        //GROUP_THEORY_SPACE_MANAGEMENT; UNAVAILABLE RN....
        //LINEAR_SPACE_MANAGEMENT;
        switch(spaceManagement) {
            case LINEAR_SPACE_MANAGEMENT:
                available_data_index += 4;
                if (available_data_index >= MAX_SPACE) {
                    Debug.Log("LINEAR MAX SPACE REACHED");
                    available_data_index = 0;
                }
                break;
            case GROUP_THEORY_SPACE_MANAGEMENT:
                group_theory_index += group_theory_increment;
                if (group_theory_index >= MAX_NUM_PARTICLES) {
                    group_theory_index = group_theory_index % MAX_NUM_PARTICLES;
                    available_data_index = group_theory_index * 4;
                }
                break;
            case STOCHASTIC_SPACE_MANAGEMENT:
            default:
                available_data_index = Random.Range(0, MAX_NUM_PARTICLES) * 4;
                break;
        }
        return available_data_index;
    }



    void draw(float x, float y) {
        // TODODODODODODOD GET THE OFFSET RIGHT, somehow the width of the UI cube
        float centerX = pixelWidth - x;// - pixelWidth*4/19;// + (mat.mainTextureOffset.x * pixelWidth * mat.mainTextureScale.x);
        float centerY = pixelHeight - y;
        
        if (modeDropdown.value != OBSERVE_MODE)  {

            float[] x_y_theta_dataType_array = new float[MAX_SPACE];
            float[] moveDist_SenseDist_particleDepositStrength_lifetime_array = new float[MAX_SPACE];
            float[] red_green_blue_alpha_array = new float[MAX_SPACE];
            float[] turn_sense_angles_array = new float[MAX_SPACE];
            x_y_theta_dataType_buffer.GetData(x_y_theta_dataType_array);
            moveDist_SenseDist_particleDepositStrength_lifetime_buffer.GetData(moveDist_SenseDist_particleDepositStrength_lifetime_array);
            red_green_blue_alpha_buffer.GetData(red_green_blue_alpha_array);
            turn_sense_angles_buffer.GetData(turn_sense_angles_array);

            float newX, newY;
            brush_size = (brushSizeSlider.value + 1)/2;
            for (int dx = (int)-brush_size; dx < (int)brush_size; dx++) {
                for(int dy = (int)-brush_size; dy < (int)brush_size; dy++) {
                    newX = centerX + dx;
                    newY = centerY + dy;

                    if ((newX-centerX)*(newX-centerX) 
                        + (newY-centerY)*(newY-centerY) < brush_size*brush_size) {
                        int nextAvailableIndex = getNextAvailableIndex();
                        if(nextAvailableIndex + 3 >= MAX_SPACE) { continue; } // make sure we don't go out of bounds
                        x_y_theta_dataType_array[nextAvailableIndex] = centerX + dx; //X
                        x_y_theta_dataType_array[nextAvailableIndex + 1] = centerY + dy; //Y
                        x_y_theta_dataType_array[nextAvailableIndex + 2] = Random.Range(-PI, PI); //random Theta

                        moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex] = moveDistanceSliderTest.value;//moveDistanceSlider.value;
                        moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex + 1] = senseDistanceSlider.value;//moveDistanceSlider.value * 2.0f;//sense distance
                        moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex + 2] = 5.0f;//agentDepositStrengthSlider.value;
                        moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex + 3] = 1.0f;//lifetimeSlider.value;
                       
                        red_green_blue_alpha_array[nextAvailableIndex] = colorPicker.CurrentColor.r;//particleRedChannelSlider.value;
                        red_green_blue_alpha_array[nextAvailableIndex + 1] = colorPicker.CurrentColor.g;//particleGreenChannelSlider.value;
                        red_green_blue_alpha_array[nextAvailableIndex + 2] = colorPicker.CurrentColor.b;//particleBlueChannelSlider.value;
                        red_green_blue_alpha_array[nextAvailableIndex + 3] = colorPicker.CurrentColor.a;//particleAlphaChannelSlider.value;

                        turn_sense_angles_array[nextAvailableIndex] = turnAngleSlider.value;
                        turn_sense_angles_array[nextAvailableIndex + 1] = senseAngleSlider.value;
                        turn_sense_angles_array[nextAvailableIndex + 2] = 0.0f;
                        turn_sense_angles_array[nextAvailableIndex + 3] = 0.0f;

                        //Debug.Log(colorPicker.CurrentColor.r);
                        // Debug.Log(colorPicker.CurrentColor.g);
                        // Debug.Log(colorPicker.CurrentColor.b);
                        // Debug.Log("--");
                        if (modeDropdown.value == DRAW_DEPOSIT_MODE) {
                            // draw temporary deposit that dissolves
                            x_y_theta_dataType_array[nextAvailableIndex + 3] = DEPOSIT;
                        } else if (modeDropdown.value == DRAW_DEPOSIT_EMITTERS_MODE) {
                            // draw deposit emitters that continuously emit deposit
                            x_y_theta_dataType_array[nextAvailableIndex + 3] = DEPOSIT_EMITTER;
                        } else if (modeDropdown.value == DRAW_PARTICLES_MODE) {
                            // draw particles
                            x_y_theta_dataType_array[nextAvailableIndex + 3] = PARTICLE;
                        }
                    }
                    
                }
            }
 
            int propagateKernel = propagate.FindKernel("CSMain");

            //x,y,theta,data
            x_y_theta_dataType_buffer = initializeComputeBuffer(x_y_theta_dataType_array, "x_y_theta_dataType", propagateKernel);

            //moveDist,senseDist,particleDepositStrength,lifetime
            moveDist_SenseDist_particleDepositStrength_lifetime_buffer = initializeComputeBuffer(moveDist_SenseDist_particleDepositStrength_lifetime_array,
                "moveDist_SenseDist_particleDepositStrength_lifetime", propagateKernel);

            //red,green,blue,alpha
            red_green_blue_alpha_buffer = initializeComputeBuffer(red_green_blue_alpha_array, "red_green_blue_alpha", propagateKernel);

            //turn and sense angles
            turn_sense_angles_buffer = initializeComputeBuffer(turn_sense_angles_array, "turn_sense_angles", propagateKernel);

        }
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown("escape"))
        {
            Debug.Log("quit");
            Application.Quit(); // Quits the game
        }

        int decayKernel = decay.FindKernel("CSMain");
        int propagateKernel = propagate.FindKernel("CSMain");
        int intPixWidth = (int)pixelWidth;
        int intPixHeight = (int)pixelHeight;
        decay.SetInt("pixelWidth", (int)pixelWidth);
        decay.SetInt("pixelHeight", (int)pixelHeight);


        if (swap == 0)
        {
            decay.SetTexture(decayKernel, "deposit_in", deposit_in);
            decay.SetTexture(decayKernel, "deposit_out", deposit_out);
            decay.SetTexture(decayKernel, "tex_trace_in", tex_trace_in);
            decay.SetTexture(decayKernel, "tex_trace_out", tex_trace_out);
            updatepropagateShaderVariables(deposit_out, tex_trace_out);
            swap = 1;
        }
        else
        {
            decay.SetTexture(decayKernel, "deposit_in", deposit_out);
            decay.SetTexture(decayKernel, "deposit_out", deposit_in);
            decay.SetTexture(decayKernel, "tex_trace_in", tex_trace_out);
            decay.SetTexture(decayKernel, "tex_trace_out", tex_trace_in);
            updatepropagateShaderVariables(deposit_in, tex_trace_in);
            swap = 0;
        }
        //blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
        blank_canvas_shader.Dispatch(blank_canvas_shader.FindKernel("CSMain"), COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

        decay.Dispatch(decayKernel, COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);
        propagate.Dispatch(propagateKernel, COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

        if (viewDropdown.value == DEPOSIT_VIEW)
        {
            if (swap == 0)
            {
                mat.mainTexture = deposit_in;
            }
            else
            {
                mat.mainTexture = deposit_out;
            }
        }

        if (viewDropdown.value == PARTICLE_VIEW)
        {
            mat.mainTexture = particle_render_texture;
        }

        if (viewDropdown.value == TRACE_VIEW)
        {
            if (swap == 0)
            {
                mat.mainTexture = tex_trace_in;
            }
            else
            {
                mat.mainTexture = tex_trace_out;
            }
        }
        GameObject uiBox = GameObject.Find("CubeUI");
        if (Input.GetMouseButton(0))
            {
                draw(Input.mousePosition.x, Input.mousePosition.y);
            }
        }
    

        
}
