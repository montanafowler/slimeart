using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HSVPicker;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
public class save : MonoBehaviour
{

    int currentScore = 0;
    string currentName = "Asd";
    float currentTimePlayed = 5.0f;

    void Start()
    {
      //  SaveFile();
      //  LoadFile();
    }

    public void SaveFile()
    {
        string destination = Application.persistentDataPath + "/save.dat";
        destination = Application.persistentDataPath + "/" + Random.Range(0,1000) + ".txt";
        FileStream file;
        Debug.Log("destination " + destination);

        if (File.Exists(destination)) file = File.OpenWrite(destination);
        else file = File.Create(destination);

        GameData data = new GameData(currentScore, currentName, currentTimePlayed);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(file, data);
        AddText(file, "testing add text method");
        file.Close();
    }

    private static void AddText(FileStream fs, string value)
    {
        byte[] info = new UTF8Encoding(true).GetBytes(value);
        fs.Write(info, 0, info.Length);
    }

    public void LoadFile()
    {
        /*string destination = Application.persistentDataPath + "/save.dat";
        FileStream file;

        if (File.Exists(destination)) file = File.OpenRead(destination);
        else
        {
            Debug.LogError("File not found");
            return;
        }

        BinaryFormatter bf = new BinaryFormatter();
        GameData data = (GameData)bf.Deserialize(file);
        file.Close();

        currentScore = data.score;
        currentName = data.name;
        currentTimePlayed = data.timePlayed;

        Debug.Log(data.name);
        Debug.Log(data.score);
        Debug.Log(data.timePlayed);*/
    }

}