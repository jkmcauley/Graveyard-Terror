using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UHFPS.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires pause and death panels for the current level.
/// Resume stays on the existing pause button. Restart reloads that level from the start.
/// </summary>
public class GraveyardLevelFlow : MonoBehaviour
{
    const string RestartButtonName = "RestartLevel";
    const string CarryFileName = "graveyard-player-carry.json";
    const string ProgressFileName = "graveyard-level-progress.json";
    const string PistolGuid = "896bd5e3b27745d884a827b90ce02fb7";
    const string OssabiteGuid = "59f0dbf8d803402380b5342aff68f188";
    const string PistolAmmoGuid = "a89400ede61f4e26aae37610a18b01c9";
    const string FlamegunGuid = "b7c1e4a09d2f4c5e8a6b3d1f0e9c8a72";
    const int FlamegunPlayerItem = 10;

    Coroutine wireRoutine;
    bool suppressCapture;
    int watchingLevel;
    bool levelMarked;
    static bool announceLock;
    static bool restarting;

    static string CarryPath => Path.Combine(Application.persistentDataPath, CarryFileName);
    static string ProgressPath => Path.Combine(Application.persistentDataPath, ProgressFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject(nameof(GraveyardLevelFlow));
        DontDestroyOnLoad(go);
        var flow = go.AddComponent<GraveyardLevelFlow>();
        SceneManager.sceneLoaded += flow.OnSceneLoaded;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WatchEditorStop()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayMode;
        UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayMode;
    }

    static void OnEditorPlayMode(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.ExitingPlayMode)
            return;

        var flow = FindAnyObjectByType<GraveyardLevelFlow>();
        if (flow != null)
            flow.CaptureCarry();
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WireFirstScene()
    {
        var flow = FindAnyObjectByType<GraveyardLevelFlow>();
        if (flow != null)
            flow.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (wireRoutine != null)
            StopCoroutine(wireRoutine);

        int levelNumber = GraveyardLevels.LevelNumber(scene.name);
        if (levelNumber > CompletedThrough() + 1)
        {
            string required = GraveyardLevels.SceneForLevel(CompletedThrough() + 1);
            if (!string.IsNullOrEmpty(required)
                && required != scene.name
                && Application.CanStreamedLevelBeLoaded(required))
            {
                announceLock = true;
                SceneManager.LoadScene(required);
                return;
            }
        }

        watchingLevel = levelNumber;
        levelMarked = IsLevelComplete(levelNumber);

        if (GraveyardLevels.IsPlayableScene(scene.name))
        {
            int level = levelNumber;
            bool freshRestart = restarting;
            restarting = false;
            bool carryIn = freshRestart
                || SaveGameManager.GameLoadType == SaveGameManager.LoadType.Normal
                || SaveGameManager.GameLoadType == SaveGameManager.LoadType.LoadPlayer;
            if (carryIn && level > 1)
                TryApplyCarry(level, freshRestart || level >= 3);

            if (SaveGameManager.GameLoadType == SaveGameManager.LoadType.Normal)
            {
                SaveGameManager.ClearLoadType();
                SaveGameManager.LoadFolderName = string.Empty;
            }

            SaveGameManager.LoadSceneName = scene.name;
            suppressCapture = false;
        }

        wireRoutine = StartCoroutine(WireAfterUi(scene.name));
    }

    void Update()
    {
        if (announceLock && GameManager.HasReference && GameManager.Instance.HintMessageGroup != null)
        {
            announceLock = false;
            GameManager.Instance.ShowHintMessage("Finish this level before the next one.", 4f);
        }

        if (watchingLevel <= 0 || levelMarked || !EnemyCounterHud.LevelCleared)
            return;

        levelMarked = true;
        MarkComplete(watchingLevel);
        if (GameManager.HasReference && GameManager.Instance.HintMessageGroup != null)
            GameManager.Instance.ShowHintMessage("Level complete.", 4f);
    }

    IEnumerator WireAfterUi(string sceneName)
    {
        yield return null;

        int level = GraveyardLevels.LevelNumber(sceneName);
        if (level == 0)
            yield break;

        if (level >= 2)
            GiveLevelTwoKit();

        string label = "Restart Level " + level;
        const string tooltip = "Start this level over from the beginning.";

        var deadRestart = GameObject.Find("DeadPanel")?.transform.Find("Panel/Buttons/Restart");
        if (deadRestart != null)
        {
            var deadButton = deadRestart.GetComponent<Button>();
            if (deadButton != null)
            {
                SilencePersistentCalls(deadButton);
                deadButton.onClick.RemoveListener(RestartCurrentLevel);
                deadButton.onClick.AddListener(RestartCurrentLevel);
            }

            SetButtonCopy(deadRestart, label, tooltip);
        }

        var pauseButtons = GameObject.Find("PauseButtons");
        if (pauseButtons == null)
            yield break;

        var restart = pauseButtons.transform.Find(RestartButtonName);
        if (restart == null)
        {
            var resume = pauseButtons.transform.Find("Resume");
            if (resume == null)
                yield break;

            var clone = Instantiate(resume.gameObject, pauseButtons.transform);
            clone.name = RestartButtonName;
            clone.transform.SetSiblingIndex(resume.GetSiblingIndex() + 1);

            var button = clone.GetComponent<Button>();
            SilencePersistentCalls(button);
            button.onClick.AddListener(RestartCurrentLevel);
            restart = clone.transform;
        }

        SetButtonCopy(restart, label, tooltip);
    }

    public void RestartCurrentLevel()
    {
        suppressCapture = true;
        restarting = true;
        if (GameManager.HasReference)
            GameManager.Instance.RestartGame();
    }

    /// <summary>
    /// Save pockets and load the next level. Chests in the next level stay as placed.
    /// </summary>
    public void ContinueToNextLevel()
    {
        string scene = SceneManager.GetActiveScene().name;
        int level = GraveyardLevels.LevelNumber(scene);
        string next = GraveyardLevels.SceneForLevel(level + 1);
        if (string.IsNullOrEmpty(next) || !Application.CanStreamedLevelBeLoaded(next))
            return;

        if (!IsLevelComplete(level))
        {
            if (GameManager.HasReference && GameManager.Instance.HintMessageGroup != null)
                GameManager.Instance.ShowHintMessage("Kill every enemy to finish this level.", 4f);
            return;
        }

        if (!CaptureCarry())
            return;

        if (GameManager.HasReference)
            GameManager.Instance.LoadNextLevel(next);
    }

    void OnApplicationQuit()
    {
        CaptureCarry();
    }

    bool CaptureCarry()
    {
        if (suppressCapture || !SaveGameManager.HasReference)
            return false;

        int level = GraveyardLevels.LevelNumber(SceneManager.GetActiveScene().name);
        if (level <= 0)
            return false;

        string json = SaveGameManager.TakePlayerJson();
        if (string.IsNullOrEmpty(json))
            return false;

        var player = JObject.Parse(json);
        JToken health = player.SelectToken("localData.health");
        if (health != null && health.Value<float>() <= 0f)
            return false;

        if (player.SelectToken("globalData.objectives") is JObject objectives)
            objectives.RemoveAll();

        var file = new JObject
        {
            ["fromLevel"] = level,
            ["player"] = player
        };

        File.WriteAllText(CarryPath, file.ToString(Formatting.None));
        SaveGameManager.UsePlayerJson(player.ToString(Formatting.None));
        return true;
    }

    public static bool IsLevelComplete(int level)
    {
        return level > 0 && CompletedThrough() >= level;
    }

    static int CompletedThrough()
    {
        if (!File.Exists(ProgressPath))
            return 0;

        try
        {
            JObject file = JObject.Parse(File.ReadAllText(ProgressPath));
            return file["completedThrough"] != null ? file["completedThrough"].Value<int>() : 0;
        }
        catch (System.Exception)
        {
            return 0;
        }
    }

    static void MarkComplete(int level)
    {
        int done = Mathf.Max(CompletedThrough(), level);
        var file = new JObject { ["completedThrough"] = done };
        File.WriteAllText(ProgressPath, file.ToString(Formatting.None));
    }

    static void GiveLevelTwoKit()
    {
        Inventory inventory = Inventory.Instance;
        if (inventory == null)
            return;

        var data = new ItemCustomData();
        if (!inventory.ContainsItem(PistolGuid))
            inventory.AddItem(PistolGuid, 7, 1, data);
        if (!inventory.ContainsItem(OssabiteGuid))
            inventory.AddItem(OssabiteGuid, 1, 2, data);
        if (!inventory.ContainsItem(PistolAmmoGuid))
            inventory.AddItem(PistolAmmoGuid, 21, data);
    }

    static void TryApplyCarry(int level, bool fullHealth)
    {
        if (level <= 1 || !File.Exists(CarryPath))
            return;

        JObject file;
        try
        {
            file = JObject.Parse(File.ReadAllText(CarryPath));
        }
        catch (System.Exception)
        {
            return;
        }

        int fromLevel = file["fromLevel"] != null ? file["fromLevel"].Value<int>() : 0;
        if (fromLevel <= 0 || fromLevel > level)
            return;

        if (file["player"] is not JObject player)
            return;

        if (player["localData"] is JObject local)
        {
            if (local["crouched"] != null)
                local["crouched"] = false;
            if (fullHealth)
                local["health"] = 100;
        }

        if (level == 4)
            StripFlamegun(player);

        SaveGameManager.UsePlayerJson(player.ToString(Formatting.None));
    }

    static void StripFlamegun(JObject player)
    {
        if (player["localData"] is JObject local
            && local["selectedItem"] != null
            && local["selectedItem"].Value<int>() == FlamegunPlayerItem)
            local["selectedItem"] = -1;

        if (player["globalData"]?["inventory"] is not JObject inventory)
            return;

        if (inventory["items"] is JObject items)
        {
            var remove = new List<string>();
            foreach (var prop in items.Properties())
            {
                if (prop.Value["item"]?.ToString() == FlamegunGuid)
                    remove.Add(prop.Name);
            }

            foreach (string name in remove)
                items.Remove(name);
        }

        if (inventory["shortcuts"] is JObject shortcuts)
        {
            foreach (var prop in shortcuts.Properties())
            {
                if (prop.Value?.ToString() == FlamegunGuid)
                    prop.Value = "{}";
            }
        }
    }

    static void SetButtonCopy(Transform button, string label, string tooltip)
    {
        var gloc = button.GetComponentInChildren<GLocText>(true);
        if (gloc != null && gloc.GlocKey != null)
        {
            gloc.GlocKey.GlocText = "*" + label;
            gloc.GlocKey.NormalText = label;
        }

        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = label;

        var hover = button.GetComponent<MenuHoverTooltip>();
        if (hover != null && hover.TooltipMessage != null)
        {
            hover.TooltipMessage.GlocText = "*" + tooltip;
            hover.TooltipMessage.NormalText = tooltip;
        }
    }

    static void SilencePersistentCalls(Button button)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
    }
}
