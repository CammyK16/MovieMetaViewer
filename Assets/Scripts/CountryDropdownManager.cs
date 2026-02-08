using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class CountryDropdownManager : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown _dropdown;

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

        var list = JsonUtility.FromJson<CountryList>(json.text);
        if (list?.countries == null || list.countries.Count == 0)
        {
            Debug.LogError("CountryDropdownManager::Populate - JSON loaded but contained no items!");
            return;
        }

        _dropdown.ClearOptions();
        _dropdown.AddOptions(list.countries.Select(c => c.name).ToList());
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