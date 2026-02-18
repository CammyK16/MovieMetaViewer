using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HandleSliderValueLabel : MonoBehaviour
{
    [SerializeField] public Canvas _settingsCanvas;
    
    private Slider _rottenTomatoesSlider;
    private Slider _metacriticSlider;
    private Slider _imdbSlider;

    private TextMeshProUGUI _rottenTomatoesLabel;
    private TextMeshProUGUI _metacriticLabel;
    private TextMeshProUGUI _imdbLabel;

    void Awake()
    {
        if (_settingsCanvas == null) Debug.LogError("HandleSliderValueLabel::Awake - Failed to get _settingsCanvas");
        if (_rottenTomatoesSlider == null) Debug.LogError("HandleSliderValueLabel::Awake - Failed to get _rottenTomatoesSlider");
        if (_metacriticSlider == null) Debug.LogError("HandleSliderValueLabel::Awake - Failed to get _metacriticSlider");
        if (_imdbSlider == null) Debug.LogError("HandleSliderValueLabel::Awake - Failed to get _imdbSlider");

        _rottenTomatoesLabel = _settingsCanvas.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "RottenTomatoesSliderValue");
        _metacriticLabel = _settingsCanvas.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "MetacriticSliderValue");
        _imdbLabel = _settingsCanvas.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name == "IMDbSliderValue");
    }

    void OnEnable()
    {
        _rottenTomatoesSlider?.onValueChanged.AddListener(OnRottenTomatoesChanged);
        _metacriticSlider?.onValueChanged.AddListener(OnMetacriticChanged);
        _imdbSlider?.onValueChanged.AddListener(OnIMDbChanged);

        UpdateAllLabels();
    }

    void OnDisable()
    {
        _rottenTomatoesSlider?.onValueChanged.RemoveListener(OnRottenTomatoesChanged);
        _metacriticSlider?.onValueChanged.RemoveListener(OnMetacriticChanged);
        _imdbSlider?.onValueChanged.RemoveListener(OnIMDbChanged);
    }

    private void OnRottenTomatoesChanged(float value)
    {
        if (_rottenTomatoesLabel != null) _rottenTomatoesLabel.text = $"{value:F0}%";
    }

    private void OnMetacriticChanged(float value)
    {
        if (_metacriticLabel != null) _metacriticLabel.text = $"{value:F0}";
    }

    private void OnIMDbChanged(float value)
    {
        if (_imdbLabel != null) _imdbLabel.text = $"{value/10:F0}";
    }
    
    private void UpdateAllLabels()
    {
        if (_rottenTomatoesSlider != null) OnRottenTomatoesChanged(_rottenTomatoesSlider.value);
        if (_metacriticSlider != null) OnMetacriticChanged(_metacriticSlider.value);
        if (_imdbSlider != null) OnIMDbChanged(_imdbSlider.value);
    }
}
