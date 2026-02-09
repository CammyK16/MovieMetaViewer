using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CustomScripts
{
    public class CountryDropdownManager : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown _dropdown;

        CountryList _countryList;

        private void Awake()
        {
            Populate();
        }

        private void Populate()
        {
            TextAsset json = Resources.Load<TextAsset>("countries");
            if (json == null)
            {
                Debug.LogError("CountryDropdownManager::Populate - Could not load country JSON!");
                return;
            }

            _countryList = JsonUtility.FromJson<CountryList>(json.text);
            if (_countryList?.countries == null || _countryList.countries.Count == 0)
            {
                Debug.LogError("CountryDropdownManager::Populate - JSON loaded but contained no items!");
                return;
            }

            _dropdown.ClearOptions();
            _dropdown.AddOptions(_countryList.countries.Select(c => c.name).ToList());
        }

        public List<string> GetSelectedCountries()
        {
            List<string> countries = new List<string>();

            var selectionBitmask = _dropdown.value;
            for (int i = 0; i < 32; i++)
            {
                bool isSelected = (selectionBitmask & (1u << i)) != 0;
                if (isSelected) countries.Add(_countryList.countries[i].alpha2);
            }
            return countries;
        }
    }

    [Serializable]
    public class Country
    {
        public string name;
        public string alpha2;
        public string countryCode;
    }

    [Serializable]
    public class CountryList
    {
        public List<Country> countries;
    }
}
