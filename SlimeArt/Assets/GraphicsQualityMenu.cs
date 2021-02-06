using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;


public class GraphicsQualityMenu : MonoBehaviour
{
    public static float CHOSEN_QUALITY_LEVEL;
    public static GameObject QUALITY_MENU_GAME_OBJECT;
    public void switchToCanvas()
    {
        Debug.Log("switchToCanvas()");
        TMP_Dropdown modeDropdown = GameObject.Find("QualityMenuDropdown").GetComponent<TMP_Dropdown>();
        CHOSEN_QUALITY_LEVEL = modeDropdown.value;
        QUALITY_MENU_GAME_OBJECT = GameObject.Find("QualityMenuGameObject");
        Debug.Log(QUALITY_MENU_GAME_OBJECT);
        Debug.Log("Chosen Quality Level menu " + CHOSEN_QUALITY_LEVEL);
        DontDestroyOnLoad(transform.gameObject);
        SceneManager.LoadScene("CanvasScene");
    }
}
