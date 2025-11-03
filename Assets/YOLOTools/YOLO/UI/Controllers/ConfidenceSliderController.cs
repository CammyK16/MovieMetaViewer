using System;
using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace YOLOTools.YOLO.UI.Controllers
{
    public class ConfidenceSliderController : MonoBehaviour
    {

        [SerializeField] private TextMeshProUGUI textMeshProUGUI;
        [SerializeField] private YOLOHandler yoloHandler;

        void Start()
        {
            gameObject.GetComponent<Slider>().value = (float)Math.Round(yoloHandler._confidenceThreshold, 2);
            textMeshProUGUI.text = Math.Round(yoloHandler._confidenceThreshold, 2)
                .ToString(CultureInfo.InvariantCulture);
        }

        public void OnConfidenceSliderValueChanged(Slider slider)
        {
            textMeshProUGUI.text = Math.Round(slider.value, 2).ToString(CultureInfo.InvariantCulture);
            yoloHandler._confidenceThreshold = (float)Math.Round(slider.value, 2);
        }
    }
}
