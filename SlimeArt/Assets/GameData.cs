[System.Serializable]
public class GameData
{
    public int score;
    public string name;
    public float timePlayed;

    public GameData(int scoreInt, string nameStr, float timePlayedF)
    {
        score = scoreInt;
        name = nameStr;
        timePlayed = timePlayedF;
    }
}