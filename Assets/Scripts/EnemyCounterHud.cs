using System.Collections.Generic;
using UHFPS.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Upper-right count of every enemy in the level. The first number counts down as they die.
/// </summary>
public class EnemyCounterHud : MonoBehaviour
{
    Text _label;
    GameObject _canvas;
    int _total;
    readonly List<NPCHealth> _living = new();

    public static int Remaining { get; private set; }
    public static int SeenTotal { get; private set; }
    public static bool LevelCleared => SeenTotal > 0 && Remaining == 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject(nameof(EnemyCounterHud));
        DontDestroyOnLoad(go);
        go.AddComponent<EnemyCounterHud>();
    }

    static bool ShouldShow(string sceneName)
    {
        if (GraveyardLevels.IsPlayableScene(sceneName))
            return true;

        if (sceneName == "MainMenu" || sceneName == "LevelManager")
            return false;

        return GameObject.FindWithTag("Player") != null;
    }

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void BuildCanvas()
    {
        _canvas = new GameObject("EnemyCounterCanvas");

        SceneManager.MoveGameObjectToScene(_canvas, SceneManager.GetActiveScene());

        var canvas = _canvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.overrideSorting = true;

        var scaler = _canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var backObject = new GameObject("EnemyCountBack");
        backObject.transform.SetParent(_canvas.transform, false);
        var backRect = backObject.AddComponent<RectTransform>();
        backRect.anchorMin = new Vector2(1f, 1f);
        backRect.anchorMax = new Vector2(1f, 1f);
        backRect.pivot = new Vector2(1f, 1f);
        backRect.anchoredPosition = new Vector2(-16f, -16f);
        backRect.sizeDelta = new Vector2(280f, 52f);
        var back = backObject.AddComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.72f);
        back.raycastTarget = false;

        var textObject = new GameObject("EnemyCount");
        textObject.transform.SetParent(_canvas.transform, false);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-28f, -24f);
        rect.sizeDelta = new Vector2(250f, 40f);

        _label = textObject.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 28;
        _label.alignment = TextAnchor.MiddleRight;
        _label.color = Color.white;
        _label.horizontalOverflow = HorizontalWrapMode.Overflow;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
        _label.raycastTarget = false;

        var shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _total = 0;
        Remaining = 0;
        SeenTotal = 0;

        if (_canvas != null)
        {
            Destroy(_canvas);
            _canvas = null;
            _label = null;
        }

        if (ShouldShow(scene.name))
            BuildCanvas();
    }

    void Update()
    {
        bool show = ShouldShow(SceneManager.GetActiveScene().name);
        if (show && _canvas == null)
            BuildCanvas();

        if (_canvas == null)
            return;

        if (_canvas.activeSelf != show)
            _canvas.SetActive(show);

        if (!show)
            return;

        _living.Clear();
        int remaining = Unspawned() + CollectLiving(_living);
        if (remaining > _total)
            _total = remaining;

        Remaining = remaining;
        SeenTotal = _total;
        _label.text = LevelCleared ? "Level complete" : "Enemies " + remaining + "/" + _total;
    }

    public static int CountRemaining()
    {
        var living = new List<NPCHealth>();
        return Unspawned() + CollectLiving(living);
    }

    public static int Unspawned()
    {
        int remaining = 0;
        SpawnManager[] spawners = Object.FindObjectsByType<SpawnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SpawnManager spawner in spawners)
            remaining += spawner.RemainingToSpawn + spawner.AdditionalEnemies;

        return remaining;
    }

    /// <summary>
    /// Living enemies that block the current level. The same list every level uses for the counter.
    /// </summary>
    public static int CollectLiving(List<NPCHealth> living)
    {
        int extra = 0;
        var counted = new HashSet<int>();
        NPCHealth[] healths = Object.FindObjectsByType<NPCHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (NPCHealth health in healths)
        {
            if (!CountsInPool(health))
                continue;

            counted.Add(health.gameObject.GetInstanceID());
            living.Add(health);
        }

        FlyGirlGhostChaser[] chasers = Object.FindObjectsByType<FlyGirlGhostChaser>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (FlyGirlGhostChaser chaser in chasers)
        {
            if (!chaser.IsAlive)
            {
                counted.Add(chaser.BodyId);
                continue;
            }

            NPCHealth health = chaser.GetComponent<NPCHealth>();
            if (health != null && !health.IsDead)
                AddLiving(living, counted, health);
            else if (health == null)
                extra++;

            counted.Add(chaser.BodyId);
        }

        GhostAngryTrigger[] churches = Object.FindObjectsByType<GhostAngryTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GhostAngryTrigger church in churches)
        {
            extra += AddTriggerBodies(living, counted, church.PrimaryBody, church.SecondaryBody);
            church.CollectGhosts(counted);
        }

        ZombieAngryTrigger[] zombieAmbushes = Object.FindObjectsByType<ZombieAngryTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ZombieAngryTrigger ambush in zombieAmbushes)
        {
            extra += AddTriggerBodies(living, counted, ambush.PrimaryBody, ambush.SecondaryBody);
            ambush.CollectZombies(counted);
        }

        foreach (NPCHealth health in healths)
        {
            if (health == null || health.IsDead || counted.Contains(health.gameObject.GetInstanceID()))
                continue;

            if (!health.gameObject.activeInHierarchy || IsBoss(health))
                continue;

            if (health.GetComponentInParent<EnemyPool>(true) != null)
                continue;

            NPCStateMachine machine = health.GetComponent<NPCStateMachine>();
            if (machine != null && machine.NPCType != NPCStateMachine.NPCTypeEnum.Enemy)
                continue;

            counted.Add(health.gameObject.GetInstanceID());
            living.Add(health);
        }

        return living.Count + extra;
    }

    static bool CountsInPool(NPCHealth health)
    {
        if (health == null || health.IsDead)
            return false;

        if (health.GetComponentInParent<EnemyPool>(true) == null)
            return false;

        return health.gameObject.activeInHierarchy && !IsBoss(health);
    }

    static void AddLiving(List<NPCHealth> living, HashSet<int> counted, NPCHealth health)
    {
        if (health == null || !counted.Add(health.gameObject.GetInstanceID()))
            return;

        living.Add(health);
    }

    static int AddTriggerBodies(List<NPCHealth> living, HashSet<int> counted, GameObject first, GameObject second)
    {
        int extra = AddTriggerBody(living, counted, first);
        if (second != first)
            extra += AddTriggerBody(living, counted, second);

        return extra;
    }

    static int AddTriggerBody(List<NPCHealth> living, HashSet<int> counted, GameObject body)
    {
        if (body == null)
            return 0;

        NPCHealth health = body.GetComponent<NPCHealth>();
        if (health != null && health.IsDead)
            return 0;

        if (health != null)
        {
            AddLiving(living, counted, health);
            return 0;
        }

        return 1;
    }

    static bool IsBoss(Component body)
    {
        Transform point = body.transform;
        while (point != null)
        {
            if (point.name == "BossEnemy")
                return true;

            point = point.parent;
        }

        return false;
    }
}
