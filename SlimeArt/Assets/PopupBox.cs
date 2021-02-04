using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupBox : MonoBehaviour
{
    public GameObject popupBox;
    public Animator animator;
    public TMP_Text popupText;

    public void PopUp(string text)
    {
        popupBox.SetActive(true);
        //popupBox.text = text;
        animator.SetTrigger("pop");
    }

}
