/// <summary>
/// Level 1 is Cemetery_Gameplay. Level 2 is Level_2. Level 3 is Level3.
/// </summary>
public static class GraveyardLevels
{
    public const int Count = 5;

    public static int LevelNumber(string sceneName)
    {
        if (sceneName == "Cemetery_Gameplay")
            return 1;

        if (sceneName == "Level3")
            return 3;

        const string prefix = "Level_";
        if (!string.IsNullOrEmpty(sceneName)
            && sceneName.StartsWith(prefix)
            && int.TryParse(sceneName.Substring(prefix.Length), out int number)
            && number >= 2)
            return number;

        return 0;
    }

    public static bool IsPlayableScene(string sceneName) => LevelNumber(sceneName) > 0;

    public static bool IsBossLevel(string sceneName) => LevelNumber(sceneName) == Count;

    public static string SceneForLevel(int levelNumber)
    {
        if (levelNumber == 1)
            return "Cemetery_Gameplay";

        if (levelNumber == 3)
            return "Level3";

        if (levelNumber >= 2)
            return "Level_" + levelNumber;

        return null;
    }
}
