using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>날씨 시스템 — 3D Directional Light + RenderSettings.fog 연동</summary>
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    [SerializeField] private float minWeatherDuration = 120f, maxWeatherDuration = 400f, transitionDuration = 8f;
    [SerializeField] private WeatherWeight[] islandWeatherWeights, cityWeatherWeights;

    [SerializeField] private ParticleSystem rainParticles, snowParticles, fogParticles, heavyRainParticles;

    // 3D Directional Light (Light2D 아님)
    [SerializeField] private Light directionalLight;
    [SerializeField] private float clearLightIntensity=1.0f, cloudyLightIntensity=0.7f, rainLightIntensity=0.5f, fogLightIntensity=0.4f, snowLightIntensity=0.75f;

    private WeatherType _current = WeatherType.Clear, _next = WeatherType.Clear;
    private float _weatherTimer;
    private bool  _isTransitioning;
    private WeatherEffectData _currentEffect = new();

    public static event Action<WeatherType>        OnWeatherChanged;
    public static event Action<WeatherType, float> OnWeatherTransitioning;
    public static event Action<WeatherEffectData>  OnWeatherEffectChanged;

    public WeatherType       CurrentWeather => _current;
    public bool              IsRaining      => _current is WeatherType.Rain or WeatherType.HeavyRain;
    public bool              IsSnowing      => _current == WeatherType.Snow;
    public bool              IsFoggy        => _current == WeatherType.Fog;
    public WeatherEffectData CurrentEffect  => _currentEffect;

    private void Awake() { if (Instance != null && Instance != this) { Destroy(this); return; } Instance = this; SetDefaultWeights(); }

    private void Start() { _weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration); ApplyImmediate(_current); StartCoroutine(WeatherLoop()); }

    private void Update() { _weatherTimer -= Time.deltaTime; }

    private IEnumerator WeatherLoop()
    {
        while (true)
        {
            yield return new WaitUntil(() => _weatherTimer <= 0 && !_isTransitioning);
            yield return Transition();
        }
    }

    private IEnumerator Transition()
    {
        _isTransitioning = true;
        _next = PickNext();
        float elapsed = 0f;
        var from = WeatherEffectDatabase.Get(_current);
        var to   = WeatherEffectDatabase.Get(_next);
        SetParticles(_next, true);
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;
            _currentEffect = WeatherEffectData.Lerp(from, to, t);
            OnWeatherTransitioning?.Invoke(_next, t);
            Apply3DLight(_currentEffect);
            ApplyFog(_currentEffect);
            yield return null;
        }
        SetParticles(_current, false);
        _current = _next;
        _isTransitioning = false;
        _currentEffect = WeatherEffectDatabase.Get(_current);
        Apply3DLight(_currentEffect); ApplyFog(_currentEffect);
        _weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration);
        OnWeatherChanged?.Invoke(_current); OnWeatherEffectChanged?.Invoke(_currentEffect);
        NotifySystems();
    }

    public void ForceWeather(WeatherType type) { StopAllCoroutines(); _isTransitioning = false; SetParticles(_current, false); _current = type; ApplyImmediate(type); _weatherTimer = UnityEngine.Random.Range(minWeatherDuration, maxWeatherDuration); StartCoroutine(WeatherLoop()); }

    private void ApplyImmediate(WeatherType type)
    {
        SetParticles(type, true);
        _currentEffect = WeatherEffectDatabase.Get(type);
        Apply3DLight(_currentEffect); ApplyFog(_currentEffect);
        OnWeatherChanged?.Invoke(type); OnWeatherEffectChanged?.Invoke(_currentEffect);
        NotifySystems();
    }

    // 3D Directional Light 적용
    private void Apply3DLight(WeatherEffectData e)
    {
        if (directionalLight == null) return;
        directionalLight.intensity = e.lightIntensity;
        directionalLight.color     = Color.Lerp(Color.white, e.lightTint, 0.4f);
    }

    // 3D Fog (RenderSettings)
    private void ApplyFog(WeatherEffectData e)
    {
        RenderSettings.fog        = e.enableFog;
        RenderSettings.fogColor   = e.fogColor;
        RenderSettings.fogDensity = e.fogDensity;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
    }

    private void SetParticles(WeatherType t, bool on)
    {
        rainParticles?     .gameObject.SetActive(on && t == WeatherType.Rain);
        heavyRainParticles?.gameObject.SetActive(on && t == WeatherType.HeavyRain);
        snowParticles?     .gameObject.SetActive(on && t == WeatherType.Snow);
        fogParticles?      .gameObject.SetActive(on && t == WeatherType.Fog);
    }

    private void NotifySystems()
    {
        FindFirstObjectByType<SurvivalStats>()?.SetEnvironmentTemperature(_currentEffect.ambientTemperature);
        string msg = _current switch { WeatherType.HeavyRain=>"⛈️ 폭우!", WeatherType.Snow=>"❄️ 눈!", WeatherType.Fog=>"🌫️ 안개!", WeatherType.Rain=>"🌧️ 비!", WeatherType.Clear=>"☀️ 맑음!", _=>"" };
        if (!string.IsNullOrEmpty(msg)) UIManager.Instance?.ShowNotification(msg);
    }

    private WeatherType PickNext()
    {
        bool isIsland = WorldGenerator.Instance?.CurrentWorldType == WorldType.Island;
        var weights = isIsland ? islandWeatherWeights : cityWeatherWeights;
        float total = 0; foreach (var w in weights) total += (w.type == _current) ? w.weight * 0.3f : w.weight;
        float roll = UnityEngine.Random.Range(0f, total);
        foreach (var w in weights) { float wt = (w.type == _current) ? w.weight * 0.3f : w.weight; roll -= wt; if (roll <= 0) return w.type; }
        return WeatherType.Clear;
    }

    private void SetDefaultWeights()
    {
        if (islandWeatherWeights == null || islandWeatherWeights.Length == 0)
            islandWeatherWeights = new[] { new WeatherWeight{type=WeatherType.Clear,weight=40}, new WeatherWeight{type=WeatherType.Cloudy,weight=25}, new WeatherWeight{type=WeatherType.Rain,weight=20}, new WeatherWeight{type=WeatherType.HeavyRain,weight=8}, new WeatherWeight{type=WeatherType.Fog,weight=5}, new WeatherWeight{type=WeatherType.Snow,weight=2} };
        if (cityWeatherWeights == null || cityWeatherWeights.Length == 0)
            cityWeatherWeights = new[] { new WeatherWeight{type=WeatherType.Cloudy,weight=30}, new WeatherWeight{type=WeatherType.Rain,weight=25}, new WeatherWeight{type=WeatherType.Fog,weight=20}, new WeatherWeight{type=WeatherType.HeavyRain,weight=15}, new WeatherWeight{type=WeatherType.Clear,weight=8}, new WeatherWeight{type=WeatherType.Snow,weight=2} };
    }

    public float GetVisionRange()   => _currentEffect.visionRange;
    public float GetMoveSpeedMult() => _currentEffect.moveSpeedMultiplier;
}

// WeatherEffectApplier — 3D FOV 조절
public class WeatherEffectApplier : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float  defaultFOV = 60f;
    [SerializeField] private AudioSource weatherAudio;
    [SerializeField] private AudioClip rainSound, snowSound;
    private SurvivalStats _stats;

    private void OnEnable()  => WeatherSystem.OnWeatherEffectChanged += OnEffect;
    private void OnDisable() => WeatherSystem.OnWeatherEffectChanged -= OnEffect;
    private void Start()     { _stats = GetComponent<SurvivalStats>(); mainCamera ??= Camera.main; }

    private void OnEffect(WeatherEffectData e)
    {
        // 3D 카메라 — FOV로 시야 제한
        if (mainCamera && !mainCamera.orthographic)
        {
            float target = Mathf.Clamp(defaultFOV * e.visionRange, 30f, defaultFOV);
            mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, target, Time.deltaTime * 2f);
        }
        var ws = WeatherSystem.Instance;
        AudioClip clip = ws.IsRaining ? rainSound : ws.IsSnowing ? snowSound : null;
        if (weatherAudio) { if (clip && weatherAudio.clip != clip) { weatherAudio.clip=clip; weatherAudio.volume=e.audioVolume; weatherAudio.Play(); } else if (!clip && weatherAudio.isPlaying) weatherAudio.Stop(); }
        _stats?.SetEnvironmentTemperature(e.ambientTemperature);
    }
}

// WeatherEffectData — 3D Fog 필드 포함
[Serializable]
public class WeatherEffectData
{
    public WeatherType type;
    public float visionRange=1f, moveSpeedMultiplier=1f, ambientTemperature=20f, lightIntensity=1f, audioVolume=0f, enemySpawnMultiplier=1f;
    public Color lightTint = Color.white;
    public bool  enableFog  = false;
    public float fogDensity = 0.01f;
    public Color fogColor   = new Color(0.5f,0.5f,0.5f);

    public static WeatherEffectData Lerp(WeatherEffectData a, WeatherEffectData b, float t) => new()
    {
        visionRange=Mathf.Lerp(a.visionRange,b.visionRange,t), moveSpeedMultiplier=Mathf.Lerp(a.moveSpeedMultiplier,b.moveSpeedMultiplier,t),
        ambientTemperature=Mathf.Lerp(a.ambientTemperature,b.ambientTemperature,t), lightIntensity=Mathf.Lerp(a.lightIntensity,b.lightIntensity,t),
        lightTint=Color.Lerp(a.lightTint,b.lightTint,t), audioVolume=Mathf.Lerp(a.audioVolume,b.audioVolume,t),
        enableFog=t>0.5f?b.enableFog:a.enableFog, fogDensity=Mathf.Lerp(a.fogDensity,b.fogDensity,t), fogColor=Color.Lerp(a.fogColor,b.fogColor,t)
    };
}

public static class WeatherEffectDatabase
{
    public static WeatherEffectData Get(WeatherType t) => t switch
    {
        WeatherType.Clear     => new WeatherEffectData{type=t,visionRange=1.0f,moveSpeedMultiplier=1.0f,ambientTemperature=22f,lightIntensity=1.0f,lightTint=new Color(1f,0.98f,0.9f),audioVolume=0f,enableFog=false,fogDensity=0f},
        WeatherType.Cloudy    => new WeatherEffectData{type=t,visionRange=0.9f,moveSpeedMultiplier=1.0f,ambientTemperature=18f,lightIntensity=0.75f,lightTint=new Color(0.85f,0.87f,0.9f),audioVolume=0f,enableFog=false},
        WeatherType.Rain      => new WeatherEffectData{type=t,visionRange=0.7f,moveSpeedMultiplier=0.9f,ambientTemperature=14f,lightIntensity=0.55f,lightTint=new Color(0.7f,0.75f,0.85f),audioVolume=0.4f,enableFog=true,fogDensity=0.015f,fogColor=new Color(0.45f,0.52f,0.6f)},
        WeatherType.HeavyRain => new WeatherEffectData{type=t,visionRange=0.45f,moveSpeedMultiplier=0.75f,ambientTemperature=10f,lightIntensity=0.35f,lightTint=new Color(0.55f,0.6f,0.75f),audioVolume=0.8f,enableFog=true,fogDensity=0.04f,fogColor=new Color(0.3f,0.38f,0.5f)},
        WeatherType.Snow      => new WeatherEffectData{type=t,visionRange=0.6f,moveSpeedMultiplier=0.8f,ambientTemperature=2f,lightIntensity=0.8f,lightTint=new Color(0.88f,0.92f,1f),audioVolume=0.2f,enableFog=true,fogDensity=0.02f,fogColor=new Color(0.75f,0.8f,0.88f)},
        WeatherType.Fog       => new WeatherEffectData{type=t,visionRange=0.35f,moveSpeedMultiplier=0.85f,ambientTemperature=12f,lightIntensity=0.4f,lightTint=new Color(0.8f,0.82f,0.8f),audioVolume=0.1f,enableFog=true,fogDensity=0.06f,fogColor=new Color(0.55f,0.58f,0.55f)},
        _ => new WeatherEffectData()
    };
}

[System.Serializable] public struct WeatherWeight { public WeatherType type; public float weight; }
public enum WeatherType { Clear, Cloudy, Rain, HeavyRain, Snow, Fog }
public static class WeatherTypeExtensions
{ public static string ToKorean(this WeatherType t) => t switch { WeatherType.Clear=>"맑음", WeatherType.Cloudy=>"흐림", WeatherType.Rain=>"비", WeatherType.HeavyRain=>"폭우", WeatherType.Snow=>"눈", WeatherType.Fog=>"안개", _=>"?" }; }
