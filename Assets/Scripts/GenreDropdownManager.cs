using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class GenreDropdownManager : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown _dropdown;

    public List<string> GetSelectedGenres()
    {
        List<string> genres = new List<string>();

        var selectionBitmask = _dropdown.value;
        for (int i = 0; i < 32; i++)
        {
            bool isSelected = (selectionBitmask & (1u << i)) != 0;
            if (isSelected) genres.Add(_dropdown.options[i].text);
        }

        return genres;
    }   
}
