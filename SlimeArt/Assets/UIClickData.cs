using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIClickData : MonoBehaviour
{
    // Start is called before the first frame update
    public float time = 0.0f;
    public string value1Name = "";
    public float value1 = -1.0f;
    public string value2Name = "";
    public float value2 = -1.0f;
    public string value3Name = "";
    public float value3 = -1.0f;

    public UIClickData(float time0, string value_1_name, float value_1)
    {
        time = time0;
        value1Name = value_1_name;
        value1 = value_1;
    }

    public UIClickData(float time0, string value_1_name, float value_1, string value_2_name, float value_2)
    {
        time = time0;
        value1Name = value_1_name;
        value1 = value_1;
        value2Name = value_2_name;
        value2 = value_2;
    }

    public UIClickData(float time0, string value_1_name, float value_1, string value_2_name, float value_2, string value_3_name, float value_3)
    {
        time = time0;
        value1Name = value_1_name;
        value1 = value_1;
        value2Name = value_2_name;
        value2 = value_2;
        value3Name = value_3_name;
        value3 = value_3;
    }
}
