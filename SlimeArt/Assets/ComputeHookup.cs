
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HSVPicker;
#if UNITY_EDITOR
    using UnityEditor;
#endif
using System.IO;
using System.Text;

public class ComputeHookup : MonoBehaviour
{
    public ComputeShader propagate;
    public ComputeShader decay;
    public ComputeShader blank_canvas_shader;

    public ComputeBuffer blank_canvas;
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

    public float move_sense_coef;
    public float normalization_factor;
    public float deposit_strength;
    public float brush_size;

    public int pixelWidth;
    public int pixelHeight;
    public const float PI = 3.1415926535897931f;
    public int swap; // for swaping textures every iteration

    //Sliders
    private Slider moveDistanceSlider;
   // private Slider scaleSlider;
    private Slider depositStrengthSlider;
   // private Slider agentDepositStrengthSlider;
    private Slider brushSizeSlider;
    private Slider brushDensitySlider;
    //private Slider lifetimeSlider;
    private Slider traceDecaySlider;
    //private Slider senseDistanceSlider;

    // text for labels
    private TextMeshProUGUI moveDistanceSliderText;
  //  private TextMeshProUGUI scaleSliderText;
    private TextMeshProUGUI depositStrengthSliderText;
   // private TextMeshProUGUI agentDepositStrengthSliderText;
    private TextMeshProUGUI brushSizeSliderText;
    private TextMeshProUGUI brushDensitySliderText;
    //private TextMeshProUGUI lifetimeSliderText;
    private TextMeshProUGUI traceDecaySliderText;
    //private TextMeshProUGUI senseDistanceSliderText;

    private ColorPicker colorPicker;
    private TextMeshProUGUI depositSettingsTitle;
    private TextMeshProUGUI particleSettingsTitle;
    private TextMeshProUGUI globalSettingsTitle;
    private Button particleBrushButton;
    private Button depositBrushButton;
    private Button pauseButton;
    private Button playButton;
    private Button leaderButton;
    private Button followerButton;
    private TMP_Dropdown viewDropdown;

    private float OBSERVE_MODE = 2.0f;
    private float PARTICLE_VIEW = 2.0f;
    private float DEPOSIT_VIEW = 1.0f;
    private float TRACE_VIEW = 0.0f;

    public const float LOW_QUALITY_GRAPHICS = 0.0f;
    public const float MED_QUALITY_GRAPHICS = 1.0f;
    public const float HIGH_QUALITY_GRAPHICS = 2.0f;

    private float PARTICLE = 1.0f;
    private float DEPOSIT_EMITTER = 2.0f;

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

    public float SAVED_QUALITY;

    //values to adjust scale and speed sliders
    private const int SCALE = 0;
    private const int SPEED = 1;
    private const int SAVE_DATA = 0;
    private const int DO_NOT_SAVE_DATA = 1;

    private Vector3 previousMousePosition;
    private int playingOrPausing; // 0 if playing 1 if paused
    private float leadingOrFollowing; // 0.001 if leading, 0 if following
    private bool drawing = false;
    private Dictionary<string, List<UIClickData>> userClickData
        = new Dictionary<string, List<UIClickData>>();


    /*
     * on start!
     */
    void Start()
    {
        SAVED_QUALITY = GraphicsQualityMenu.CHOSEN_QUALITY_LEVEL;

        // set label where csv click data will be saved when app quits
        //TextMeshProUGUI label = GameObject.Find("FileLocation").GetComponent<TextMeshProUGUI>();
        //label.SetText(Application.persistentDataPath);

        propagateKernel = propagate.FindKernel("CSMain");
        GameObject uiBox = GameObject.Find("CubeUI");
        GameObject drawingCanvas = GameObject.Find("DrawingCanvas");
        pixelWidth = (int)(drawingCanvas.transform.lossyScale.x / (drawingCanvas.transform.lossyScale.x + uiBox.transform.lossyScale.x) * Screen.width);
        pixelHeight = Screen.height;

        if (SAVED_QUALITY == LOW_QUALITY_GRAPHICS)
        {
            MAX_SPACE = 100000;
        }

        if (SAVED_QUALITY == MED_QUALITY_GRAPHICS)
        {
            MAX_SPACE = 900000;
        }

        if (SAVED_QUALITY == HIGH_QUALITY_GRAPHICS)
        {
            MAX_SPACE = 2000000;
        }

        MAX_NUM_PARTICLES = MAX_SPACE / 4; // buffers have to have info for four
        COMPUTE_GRID_HEIGHT = 256;
        COMPUTE_GRID_WIDTH = MAX_SPACE / COMPUTE_GRID_HEIGHT;
        mat.mainTextureOffset = new Vector2(0.0f, 0.0f);

        calculateGroupTheoryIncrement();
        setupBuffers(); // sets up the buffers with their info.
        setupUI();

        blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
        blank_canvas_shader.Dispatch(blank_canvas_shader.FindKernel("CSMain"), COMPUTE_GRID_WIDTH, COMPUTE_GRID_HEIGHT, 1);

        previousMousePosition = new Vector3(0.0f, 0.0f, 0.0f);
    }

    /*
     * sets up the buffers for the shaders
     */
    void setupBuffers()
    {
        // random seeding of arrays
        float[] blankCanvas = new float[MAX_SPACE];
        float[] x_y_theta_dataType_array = new float[MAX_SPACE];
        float[] moveDist_SenseDist_particleDepositStrength_lifetime_array = new float[MAX_SPACE];
        float[] red_green_blue_alpha_array = new float[MAX_SPACE];
        float[] turn_sense_angles_array = new float[MAX_SPACE];

        //x,y,theta,data
        //x_y_theta_dataType_buffer.Release();
        x_y_theta_dataType_buffer = initializeComputeBuffer(x_y_theta_dataType_array, "x_y_theta_dataType", propagateKernel);

        //moveDist,senseDist,particleDepositStrength,lifetime
        //moveDist_SenseDist_particleDepositStrength_lifetime_buffer.Release();
        moveDist_SenseDist_particleDepositStrength_lifetime_buffer = initializeComputeBuffer(moveDist_SenseDist_particleDepositStrength_lifetime_array,
            "moveDist_SenseDist_particleDepositStrength_lifetime", propagateKernel);

        //red,green,blue,alpha
        //red_green_blue_alpha_buffer.Release();
        red_green_blue_alpha_buffer = initializeComputeBuffer(red_green_blue_alpha_array, "red_green_blue_alpha", propagateKernel);

        //turn_sense_angles_buffer.Release();
        turn_sense_angles_buffer = initializeComputeBuffer(turn_sense_angles_array, "turn_sense_angles", propagateKernel);

        //blank_canvas.Release();
        blank_canvas = initializeComputeBuffer(blankCanvas, "blank_canvas", blank_canvas_shader.FindKernel("CSMain"));

        // deposit texture for propagate shader
        deposit_in = initializeRenderTexture();
        deposit_out = initializeRenderTexture();
        particle_render_texture = initializeRenderTexture();

        // trace texture for the propagate shader
        tex_trace_in = initializeRenderTexture();
        tex_trace_out = initializeRenderTexture();
    }


    /*
     * sets up all of the ui components and on click functions
     */
    void setupUI()
    {
        // other variables
        move_sense_coef = 1.0f;
        normalization_factor = 2.0f;

        setupUIClickData();
        initializeSliders();
        initializeTitlesAndText();
        setupDropdownsButtonsAndTheColorPicker();
    }

    /*
     * set up the ui click data dictionary
     */
    private void setupUIClickData()
    {
        userClickData.Add("ViewDropdown", new List<UIClickData>());
        userClickData.Add("Play", new List<UIClickData>());
        userClickData.Add("Pause", new List<UIClickData>());
        userClickData.Add("ClearCanvasButton", new List<UIClickData>());
        userClickData.Add("ParticleBrushButton", new List<UIClickData>());
        userClickData.Add("DepositBrushButton", new List<UIClickData>());
        userClickData.Add("Leader", new List<UIClickData>());
        userClickData.Add("Follower", new List<UIClickData>());
        userClickData.Add("BrushSizeSlider", new List<UIClickData>());
        userClickData.Add("BrushDensitySlider", new List<UIClickData>());
        userClickData.Add("MoveDistanceSlider", new List<UIClickData>());
        //userClickData.Add("ScaleSlider", new List<UIClickData>());
        userClickData.Add("DepositStrengthSlider", new List<UIClickData>());
       // userClickData.Add("AgentDepositStrengthSlider", new List<UIClickData>());
        userClickData.Add("Picker", new List<UIClickData>());
      //  userClickData.Add("SenseDistanceSlider", new List<UIClickData>());
        userClickData.Add("TraceDecaySlider", new List<UIClickData>());
        userClickData.Add("DrawMouseDown", new List<UIClickData>());
        userClickData.Add("DrawMouseUp", new List<UIClickData>());
    }

    /*
     * initialize the slidre variables
     */
    private void initializeSliders()
    {
        moveDistanceSlider = GameObject.Find("MoveDistanceSlider").GetComponent<Slider>();
      //  scaleSlider = GameObject.Find("ScaleSlider").GetComponent<Slider>();
        depositStrengthSlider = GameObject.Find("DepositStrengthSlider").GetComponent<Slider>();
       // agentDepositStrengthSlider = GameObject.Find("AgentDepositStrengthSlider").GetComponent<Slider>();
        brushSizeSlider = GameObject.Find("BrushSizeSlider").GetComponent<Slider>();
        brushDensitySlider = GameObject.Find("BrushDensitySlider").GetComponent<Slider>();
        //lifetimeSlider = GameObject.Find("ParticleLifetimeSlider").GetComponent<Slider>();
        colorPicker = GameObject.Find("Picker").GetComponent<ColorPicker>();
       // senseDistanceSlider = GameObject.Find("SenseDistanceSlider").GetComponent<Slider>();
        traceDecaySlider = GameObject.Find("TraceDecaySlider").GetComponent<Slider>();
    }

    /*
     * initialize titles and text
     */
    private void initializeTitlesAndText()
    {
        particleSettingsTitle = GameObject.Find("ParticleSettingsTitle").GetComponent<TextMeshProUGUI>();
        depositSettingsTitle = GameObject.Find("DepositSettingsTitle0").GetComponent<TextMeshProUGUI>();
        globalSettingsTitle = GameObject.Find("GlobalSettingsTitle").GetComponent<TextMeshProUGUI>();

        moveDistanceSliderText = GameObject.Find("MoveDistanceSliderText").GetComponent<TextMeshProUGUI>();
        moveDistanceSlider.onValueChanged.AddListener(delegate { updateSliderLabel(moveDistanceSliderText, "speed: ", moveDistanceSlider.value); userClickData["MoveDistanceSlider"].Add(new UIClickData(Time.time, "speed", moveDistanceSlider.value)); });
        updateSliderLabel(moveDistanceSliderText, "speed: ", moveDistanceSlider.value);

       // scaleSliderText = GameObject.Find("ScaleSliderText").GetComponent<TextMeshProUGUI>();
       // scaleSlider.onValueChanged.AddListener(delegate { updateSliderLabel(scaleSliderText, "field of view: ", scaleSlider.value); userClickData["ScaleSlider"].Add(new UIClickData(Time.time, "field of view", scaleSlider.value)); });
       // updateSliderLabel(scaleSliderText, "field of view: ", scaleSlider.value);

        depositStrengthSliderText = GameObject.Find("DepositStrengthSliderText").GetComponent<TextMeshProUGUI>();
        depositStrengthSlider.onValueChanged.AddListener(delegate {  updateSliderLabel(depositStrengthSliderText, "deposit strength: ", depositStrengthSlider.value * Screen.width); userClickData["DepositStrengthSlider"].Add(new UIClickData(Time.time, "deposit strength", depositStrengthSlider.value * Screen.width)); });
        updateSliderLabel(depositStrengthSliderText, "deposit strength: ", depositStrengthSlider.value * Screen.width);

      //  agentDepositStrengthSliderText = GameObject.Find("AgentDepositStrengthSliderText").GetComponent<TextMeshProUGUI>();
       // agentDepositStrengthSlider.onValueChanged.AddListener(delegate {  updateSliderLabel(agentDepositStrengthSliderText, "particle deposit strength: ", agentDepositStrengthSlider.value); userClickData["AgentDepositStrengthSlider"].Add(new UIClickData(Time.time, "particle deposit strength", agentDepositStrengthSlider.value)); });
       // updateSliderLabel(agentDepositStrengthSliderText, "particle deposit strength: ", agentDepositStrengthSlider.value);

        brushSizeSliderText = GameObject.Find("BrushSizeSliderText").GetComponent<TextMeshProUGUI>();
        brushSizeSlider.onValueChanged.AddListener(delegate {  updateSliderLabel(brushSizeSliderText, "brush size: ", brushSizeSlider.value); userClickData["BrushSizeSlider"].Add(new UIClickData(Time.time, "brush size", brushSizeSlider.value)); });
        updateSliderLabel(brushSizeSliderText, "brush size: ", brushSizeSlider.value);

        brushDensitySliderText = GameObject.Find("BrushDensitySliderText").GetComponent<TextMeshProUGUI>();
        brushDensitySlider.onValueChanged.AddListener(delegate {  updateSliderLabel(brushDensitySliderText, "brush density: ", 50 + brushDensitySlider.value); userClickData["BrushDensitySlider"].Add(new UIClickData(Time.time, "brush density", 50 + brushDensitySlider.value)); });
        updateSliderLabel(brushDensitySliderText, "brush density: ", 50 + brushDensitySlider.value);

        traceDecaySliderText = GameObject.Find("TraceDecaySliderText").GetComponent<TextMeshProUGUI>();
        traceDecaySlider.onValueChanged.AddListener(delegate {  updateSliderLabel(traceDecaySliderText, "trace decay: ", traceDecaySlider.value); userClickData["TraceDecaySlider"].Add(new UIClickData(Time.time, "trace decay", traceDecaySlider.value)); });
        updateSliderLabel(traceDecaySliderText, "trace decay: ", traceDecaySlider.value);

        //lifetimeSliderText = GameObject.Find("ParticleLifetimeSliderText").GetComponent<TextMeshProUGUI>();
        //lifetimeSlider.onValueChanged.AddListener(delegate { updateSliderLabel(lifetimeSliderText, "Particle Lifetime: ", lifetimeSlider.value); });
        //updateSliderLabel(lifetimeSliderText, "Particle Lifetime: ", lifetimeSlider.value);

       // senseDistanceSliderText = GameObject.Find("SenseDistanceSliderText").GetComponent<TextMeshProUGUI>();
       // senseDistanceSlider.onValueChanged.AddListener(delegate {  updateSliderLabel(senseDistanceSliderText, "visibility distance: ", senseDistanceSlider.value); userClickData["SenseDistanceSlider"].Add(new UIClickData(Time.time, "visibility distance", senseDistanceSlider.value)); });
       // updateSliderLabel(senseDistanceSliderText, "visibility distance: ", senseDistanceSlider.value);

    }

    /*
     * set up the buttons, dropdowns, and the color picker
     */
    private void setupDropdownsButtonsAndTheColorPicker()
    {
        viewDropdown = GameObject.Find("ViewDropdown").GetComponent<TMP_Dropdown>();
        viewDropdown.onValueChanged.AddListener(delegate {  userClickData["ViewDropdown"].Add(new UIClickData(Time.time, "view", viewDropdown.value)); });

        // brush buttons setup
        particleBrushButton = GameObject.Find("ParticleBrushButton").GetComponent<Button>();
        depositBrushButton = GameObject.Find("DepositBrushButton").GetComponent<Button>();
        particleBrushButton.onClick.AddListener(delegate {  brushSwitch(true); userClickData["ParticleBrushButton"].Add(new UIClickData(Time.time, "particle brush button click", 1.0f)); });
        depositBrushButton.onClick.AddListener(delegate { brushSwitch(false); userClickData["DepositBrushButton"].Add(new UIClickData(Time.time, "deposit brush button click", 1.0f)); });
        brushSwitch(true); // set particle brush to be selected first

        // play pause setup
        playButton = GameObject.Find("Play").GetComponent<Button>();
        pauseButton = GameObject.Find("Pause").GetComponent<Button>();
        playButton.onClick.AddListener(delegate {  pausePlaySwitch(true); userClickData["Play"].Add(new UIClickData(Time.time, "play button click", 1.0f)); });
        pauseButton.onClick.AddListener(delegate {  pausePlaySwitch(false); userClickData["Pause"].Add(new UIClickData(Time.time, "pause button click", 1.0f)); });
        pausePlaySwitch(true);

        // lead follow setup
        leaderButton = GameObject.Find("LeaderButton").GetComponent<Button>();
        followerButton = GameObject.Find("FollowerButton").GetComponent<Button>();
        leaderButton.onClick.AddListener(delegate { leaderFollowerSwitch(true); userClickData["Leader"].Add(new UIClickData(Time.time, "leader button click", 1.0f)); });
        followerButton.onClick.AddListener(delegate { leaderFollowerSwitch(false); userClickData["Follower"].Add(new UIClickData(Time.time, "follower button click", 1.0f)); });
        leaderFollowerSwitch(true);

        // color picker on change
        colorPicker.onValueChanged.AddListener(color => {
            userClickData["Picker"].Add(new UIClickData(Time.time, "red", color.r, "green", color.g, "blue", color.b));
        });

        //clear canvas button;
        Button clearCanvasButton = GameObject.Find("ClearCanvasButton").GetComponent<Button>();
        clearCanvasButton.onClick.AddListener(delegate {  setupBuffers(); });

    }

    /*
     * when the user switches buttons hide/show the appropriate UI
     */
    void brushSwitch(bool particleBrush)
    {
        // if the particle brush button was clicked
        if (particleBrush)
        {
            particleBrushButton.interactable = false; // grey out particle button
            depositBrushButton.interactable = true; // activate deposit button

            moveDistanceSlider.enabled = true;
            moveDistanceSlider.interactable = true;
            moveDistanceSlider.gameObject.SetActive(true);

          //  scaleSlider.enabled = true;
          //  scaleSlider.interactable = true;
          //  scaleSlider.gameObject.SetActive(true);

            traceDecaySlider.enabled = true;
            traceDecaySlider.interactable = true;
            traceDecaySlider.gameObject.SetActive(true);

          //  agentDepositStrengthSlider.enabled = true;
           // agentDepositStrengthSlider.interactable = true;
          //  agentDepositStrengthSlider.gameObject.SetActive(true);

            //senseDistanceSlider.enabled = true;
            //senseDistanceSlider.interactable = true;
           // senseDistanceSlider.gameObject.SetActive(true);

            colorPicker.gameObject.SetActive(true);

            particleSettingsTitle.gameObject.SetActive(true);
            globalSettingsTitle.gameObject.SetActive(true);
            depositStrengthSlider.enabled = false;
            depositStrengthSlider.interactable = false;
            depositStrengthSlider.gameObject.SetActive(false);
            depositSettingsTitle.gameObject.SetActive(false);

            if (viewDropdown.value == (int)DEPOSIT_VIEW)
            {
                viewDropdown.value = (int)TRACE_VIEW; // switch to trace view automatically
            }
        }
        else
        {
            // deposit brush button clicked
            particleSettingsTitle.GetComponent<TextMeshProUGUI>().gameObject.SetActive(false);
            depositSettingsTitle.GetComponent<TextMeshProUGUI>().gameObject.SetActive(true);
            globalSettingsTitle.gameObject.SetActive(false);
            colorPicker.gameObject.SetActive(false);
            particleBrushButton.interactable = true;
            depositBrushButton.interactable = false;
            moveDistanceSlider.enabled = false;
            moveDistanceSlider.interactable = false;
            moveDistanceSlider.gameObject.SetActive(false);
          //  scaleSlider.enabled = false;
          //  scaleSlider.interactable = false;
          //  scaleSlider.gameObject.SetActive(false);
            traceDecaySlider.enabled = false;
            traceDecaySlider.interactable = false;
            traceDecaySlider.gameObject.SetActive(false);
           // agentDepositStrengthSlider.enabled = false;
          //  agentDepositStrengthSlider.interactable = false;
           // agentDepositStrengthSlider.gameObject.SetActive(false);
           // senseDistanceSlider.enabled = false;
           // senseDistanceSlider.interactable = false;
          //  senseDistanceSlider.gameObject.SetActive(false);
            depositStrengthSlider.enabled = true;
            depositStrengthSlider.interactable = true;
            depositStrengthSlider.gameObject.SetActive(true);
            viewDropdown.value = (int)DEPOSIT_VIEW; // automatically switch to deposit view
        }
    }

    /*
     * switch between pause and play when button is clicked.
     */
    void pausePlaySwitch(bool play)
    {
        playButton.interactable = !play; //false if play button was pushed
        pauseButton.interactable = play; //true of play button was pushed
        if (play)
        {
            playingOrPausing = 0; //play
        }
        else
        {
            playingOrPausing = 1; //pause
        }
    }

    /*
     * switch between pause and play when button is clicked.
     */
    void leaderFollowerSwitch(bool leader)
    {
        leaderButton.interactable = !leader; //false if play button was pushed
        followerButton.interactable = leader; //true of play button was pushed
        if (leader)
        {
            leadingOrFollowing = 0.01f; //leading
        }
        else
        {
            leadingOrFollowing = 0.0f; //following
        }
    }

    /*
     * calculate group theory increment for teting group theory storage management
     */
    private void calculateGroupTheoryIncrement()
    {
        group_theory_increment = 3;
        while (MAX_SPACE % group_theory_increment == 0)
        {
            group_theory_increment++;
        }
    }

    /*
     * update the slider label
     */
    public void updateSliderLabel(TextMeshProUGUI label, string labelText, float value)
    {
        label.SetText(labelText + value.ToString());
    }

    /*
     * initialize a compute buffer and add it to the propagate shader
     */
    private ComputeBuffer initializeComputeBuffer(float[] arr, string shaderBufferName, int propagateKernel)
    {
        ComputeBuffer computeBuffer = new ComputeBuffer(arr.Length, sizeof(float));
        computeBuffer.SetData(arr);
        propagate.SetBuffer(propagateKernel, shaderBufferName, computeBuffer);
        return computeBuffer;
    }

    /*
     * initialize render texture 
     */
    private RenderTexture initializeRenderTexture()
    {
        RenderTexture renderTexture = new RenderTexture(pixelWidth, pixelHeight, 32);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        return renderTexture;
    }

    /*
     * update the propagate shader variables
     */
    void updatepropagateShaderVariables(RenderTexture depositTexture, RenderTexture traceTexture)
    {
        deposit_strength = depositStrengthSlider.value * Screen.width;
        int propagateKernel = propagate.FindKernel("CSMain");
        propagate.SetTexture(propagateKernel, "tex_deposit", depositTexture);
        propagate.SetTexture(propagateKernel, "tex_trace", traceTexture);
        propagate.SetFloat("world_width", (float)pixelWidth);
        propagate.SetFloat("world_height", (float)pixelHeight);
        propagate.SetFloat("move_sense_coef", move_sense_coef); // ?
        propagate.SetFloat("normalization_factor", normalization_factor); // ?
        propagate.SetFloat("pixelWidth", pixelWidth);
        propagate.SetFloat("pixelHeight", pixelHeight);
        propagate.SetFloat("deposit_strength", deposit_strength);
        propagate.SetTexture(propagateKernel, "particle_render_texture", particle_render_texture);
        propagate.SetFloat("COMPUTE_GRID_WIDTH", (float)COMPUTE_GRID_WIDTH);
        propagate.SetFloat("COMPUTE_GRID_HEIGHT", (float)COMPUTE_GRID_HEIGHT);
        propagate.SetInt("playingOrPausing", playingOrPausing);
    }

    /*
     * get the next available index based on the selected space management system
     */
    int getNextAvailableIndex()
    {
        int spaceManagement = STOCHASTIC_SPACE_MANAGEMENT;
        switch (spaceManagement)
        {
            case LINEAR_SPACE_MANAGEMENT:
                available_data_index += 4;
                if (available_data_index >= MAX_SPACE)
                {
                    available_data_index = 0;
                }
                break;
            case GROUP_THEORY_SPACE_MANAGEMENT:
                group_theory_index += group_theory_increment;
                if (group_theory_index >= MAX_NUM_PARTICLES)
                {
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


    /*
     * draw the particles centered at x,y on the canvas with the saved settings
     */
    void draw(float x, float y)
    {
        float centerX = Screen.width - x;
        float centerY = pixelHeight - y;
        float[] x_y_theta_dataType_array = new float[MAX_SPACE];
        float[] moveDist_SenseDist_particleDepositStrength_lifetime_array = new float[MAX_SPACE];
        float[] red_green_blue_alpha_array = new float[MAX_SPACE];
        float[] turn_sense_angles_array = new float[MAX_SPACE];
        x_y_theta_dataType_buffer.GetData(x_y_theta_dataType_array);
        moveDist_SenseDist_particleDepositStrength_lifetime_buffer.GetData(moveDist_SenseDist_particleDepositStrength_lifetime_array);
        red_green_blue_alpha_buffer.GetData(red_green_blue_alpha_array);
        turn_sense_angles_buffer.GetData(turn_sense_angles_array);

        float newX, newY;
        brush_size = (brushSizeSlider.value + 1) / 2;
        int brush_density = (int)brushDensitySlider.value * -1;

        if (SAVED_QUALITY == 0.0f && brush_density < 5)
        {
            brush_density = 5; // for low quality, make it less dense to avoid running out of particles
        }

        // if the brush density is bigger than a third of the brush size, make it that
        if (brush_density > brush_size / 3)
        {
            brush_density = (int)brush_size / 3;
        }

        for (int dx = (int)-brush_size; dx < (int)brush_size; dx += brush_density)
        {
            for (int dy = (int)-brush_size; dy < (int)brush_size; dy += brush_density)
            {
                newX = centerX + dx;
                newY = centerY + dy;

                if ((newX - centerX) * (newX - centerX)
                    + (newY - centerY) * (newY - centerY) < brush_size * brush_size)
                {
                    int nextAvailableIndex = getNextAvailableIndex();
                    if (nextAvailableIndex + 3 >= MAX_SPACE) { continue; } // make sure we don't go out of bounds
                    x_y_theta_dataType_array[nextAvailableIndex] = centerX + dx; //X
                    x_y_theta_dataType_array[nextAvailableIndex + 1] = centerY + dy; //Y
                    x_y_theta_dataType_array[nextAvailableIndex + 2] = Random.Range(-PI, PI); //random Theta

                    moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex] = moveDistanceSlider.value;
                    moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex + 1] = 0.20f * pixelWidth / 3.0f;//sense distance
                    moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex + 2] = leadingOrFollowing;
                    moveDist_SenseDist_particleDepositStrength_lifetime_array[nextAvailableIndex + 3] = 1.0f;//lifetimeSlider.value;

                    red_green_blue_alpha_array[nextAvailableIndex] = colorPicker.CurrentColor.r;//particleRedChannelSlider.value;
                    red_green_blue_alpha_array[nextAvailableIndex + 1] = colorPicker.CurrentColor.g;//particleGreenChannelSlider.value;
                    red_green_blue_alpha_array[nextAvailableIndex + 2] = colorPicker.CurrentColor.b;//particleBlueChannelSlider.value;
                    red_green_blue_alpha_array[nextAvailableIndex + 3] = colorPicker.CurrentColor.a;//particleAlphaChannelSlider.value;

                    turn_sense_angles_array[nextAvailableIndex] = 1.0f;//scaleSlider.value; //turnAngleSlider.value;
                    turn_sense_angles_array[nextAvailableIndex + 1] = 2.0f;//scaleSlider.value * 2.0f; //senseAngleSlider.value;
                    turn_sense_angles_array[nextAvailableIndex + 2] = 0.0f;//traceDecaySlider.value;
                    turn_sense_angles_array[nextAvailableIndex + 3] = 0.0f;

                    if (depositBrushButton.interactable == false)
                    {
                        // draw deposit emitters that continuously emit deposit
                        x_y_theta_dataType_array[nextAvailableIndex + 3] = DEPOSIT_EMITTER;
                    }
                    else if (particleBrushButton.interactable == false)
                    {
                        // draw particles
                        x_y_theta_dataType_array[nextAvailableIndex + 3] = PARTICLE;
                    }
                }

            }
        }

        int propagateKernel = propagate.FindKernel("CSMain");

        //x,y,theta,data
        x_y_theta_dataType_buffer.Release();
        x_y_theta_dataType_buffer = initializeComputeBuffer(x_y_theta_dataType_array, "x_y_theta_dataType", propagateKernel);

        //moveDist,senseDist,particleDepositStrength,lifetime
        moveDist_SenseDist_particleDepositStrength_lifetime_buffer.Release();
        moveDist_SenseDist_particleDepositStrength_lifetime_buffer = initializeComputeBuffer(moveDist_SenseDist_particleDepositStrength_lifetime_array,
            "moveDist_SenseDist_particleDepositStrength_lifetime", propagateKernel);

        //red,green,blue,alpha
        red_green_blue_alpha_buffer.Release();
        red_green_blue_alpha_buffer = initializeComputeBuffer(red_green_blue_alpha_array, "red_green_blue_alpha", propagateKernel);

        //turn and sense angles
        turn_sense_angles_buffer.Release();
        turn_sense_angles_buffer = initializeComputeBuffer(turn_sense_angles_array, "turn_sense_angles", propagateKernel);


    }

    /*
     * called every animation frame
     */
    void Update()
    {

        if (Input.GetKeyDown("escape"))
        {
            x_y_theta_dataType_buffer.Release();
            moveDist_SenseDist_particleDepositStrength_lifetime_buffer.Release();
            red_green_blue_alpha_buffer.Release();
            turn_sense_angles_buffer.Release();
            SaveDataFile();
            Application.Quit(); // Quits the game
        }

        int decayKernel = decay.FindKernel("CSMain");
        int propagateKernel = propagate.FindKernel("CSMain");
        int intPixWidth = (int)pixelWidth;
        int intPixHeight = (int)pixelHeight;
        decay.SetInt("pixelWidth", (int)pixelWidth);
        decay.SetInt("pixelHeight", (int)pixelHeight);
        decay.SetFloat("trace_decay_value", traceDecaySlider.value);

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
        blank_canvas_shader.SetTexture(blank_canvas_shader.FindKernel("CSMain"), "Result", particle_render_texture);
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
        GameObject drawingCanvas = GameObject.Find("DrawingCanvas");
        float pixelWidthDrawingCanvas = drawingCanvas.transform.lossyScale.x / (drawingCanvas.transform.lossyScale.x + uiBox.transform.lossyScale.x) * pixelWidth;

        // if the mouse is down and there is some distance between the previous position and the new one
        if (Input.GetMouseButton(0) && Vector3.Distance(previousMousePosition, Input.mousePosition) > 20.0f)
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            previousMousePosition = Input.mousePosition;
            if (Physics.Raycast(ray, out hit, 100.0f))
            {
                // if they drew on the drawing canvas
                if (hit.transform.name == "DrawingCanvas")
                {
                    drawing = true;
                    float drawingCanvasWidth = drawingCanvas.transform.lossyScale.x / (drawingCanvas.transform.lossyScale.x + uiBox.transform.lossyScale.x) * Screen.width;
                    float uiBoxWidth = uiBox.transform.lossyScale.x / (drawingCanvas.transform.lossyScale.x + uiBox.transform.lossyScale.x) * Screen.width;
                    float fraction = (Camera.main.WorldToScreenPoint(hit.point).x /*+ uiBoxWidth * 1.2f*/) / drawingCanvasWidth;// - 0.5f;
                    float newX = Camera.main.WorldToScreenPoint(hit.point).x + uiBoxWidth * (1.2f + -1.0f * (fraction - 0.5f) / 2.0f);
                    draw(newX, Camera.main.WorldToScreenPoint(hit.point).y);

                    if (Input.GetMouseButtonDown(0))
                    {
                        saveDrawingUIClickData(newX, hit);
                    }
                }
            }
        }

        if (Input.GetMouseButtonUp(0) && drawing)
        {
            userClickData["DrawMouseUp"].Add(new UIClickData(Time.time, "mouse up", 0.0f));
            drawing = false;
        }
    }

    /*
     * save drawing the ui click data
     */
    private void saveDrawingUIClickData(float newX, RaycastHit hit)
    {
        userClickData["DrawMouseDown"].Add(new UIClickData(Time.time, "x", newX, "y", Camera.main.WorldToScreenPoint(hit.point).y));
        userClickData["ViewDropdown"].Add(new UIClickData(Time.time, "drawing view", viewDropdown.value));
        userClickData["Play"].Add(new UIClickData(Time.time, "drawing play=0pause=1", playingOrPausing));
        userClickData["Pause"].Add(new UIClickData(Time.time, "drawing play=0pause=1", playingOrPausing));
        userClickData["BrushSizeSlider"].Add(new UIClickData(Time.time, "drawing brush size", brushSizeSlider.value));
        userClickData["BrushDensitySlider"].Add(new UIClickData(Time.time, "drawing brush density", brushDensitySlider.value + 50));
        userClickData["MoveDistanceSlider"].Add(new UIClickData(Time.time, "drawing speed", moveDistanceSlider.value));
       // userClickData["ScaleSlider"].Add(new UIClickData(Time.time, "drawing turn angle", scaleSlider.value));
        userClickData["DepositStrengthSlider"].Add(new UIClickData(Time.time, "drawing deposit strength", depositStrengthSlider.value * Screen.width));
       // userClickData["AgentDepositStrengthSlider"].Add(new UIClickData(Time.time, "drawing particle deposit strength", agentDepositStrengthSlider.value));
        userClickData["Picker"].Add(new UIClickData(Time.time, "drawing red", colorPicker.CurrentColor.r, "drawing green", colorPicker.CurrentColor.g, "drawing blue", colorPicker.CurrentColor.b));
       // userClickData["SenseDistanceSlider"].Add(new UIClickData(Time.time, "drawing sense distance ", senseDistanceSlider.value));
        userClickData["TraceDecaySlider"].Add(new UIClickData(Time.time, "drawing trace decay ", traceDecaySlider.value));

    }

    /*
     * save the data file with all of the click data from the user
     */
    public void SaveDataFile()
    {
        string destination = Application.persistentDataPath + "/dataFile" + Random.Range(0, 10000) + ".csv";
        FileStream file;
        Debug.Log("destination " + destination);

        if (File.Exists(destination)) file = File.OpenWrite(destination);
        else file = File.Create(destination);
        string csvLineToAdd = "total time using application," + Time.time + ",MAX_SPACE," + MAX_SPACE + ",resolution width," + Screen.width + ",resolution height," + Screen.height + "\n";
        AddText(file, csvLineToAdd);
        AddText(file, "UI Component,Time,Value 1 Name,Value 1, Value 2 Name, Value 2, Value 3 Name, Value 3\n");
        float previousTime = -2.0f;

        foreach (KeyValuePair<string, List<UIClickData>> pair in userClickData)
        {
            previousTime = -2.0f;
            foreach (UIClickData uiClick in pair.Value)
            {
                if (uiClick.time < previousTime - 0.2f || uiClick.time > previousTime + 0.2f)
                {
                    csvLineToAdd = pair.Key + "," + uiClick.time + ","
                    + uiClick.value1Name + "," + uiClick.value1 + ","
                    + uiClick.value2Name + "," + uiClick.value2 + ","
                    + uiClick.value3Name + "," + uiClick.value3 + "\n";
                    AddText(file, csvLineToAdd);
                    previousTime = uiClick.time;
                }


            }
        }

        file.Close();
    }

    /*
     * on application quit, save the data file
     */
    void OnApplicationQuit()
    {
        SaveDataFile();
    }


    /*
     * adds text to the file
     */
    private static void AddText(FileStream fs, string value)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(value);
        fs.Write(info, 0, info.Length);
    }
}
