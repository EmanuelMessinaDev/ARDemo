using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Button that cycles through the locales on each press, showing the short code of the current
/// locale in the center and its full name in the label.
/// </summary>
public class LocaleButton : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Locale Locale;

        [Tooltip("Text shown in the center of the button. If empty, the uppercase locale code is used.")]
        public string ShortName;

        [Tooltip("Text shown in the label. If empty, the native language name is used.")]
        public string FullName;
    }

    [SerializeField]
    private Button _button;

    [SerializeField]
    [Tooltip("Text in the center of the button that displays the short name of the current locale.")]
    private TMP_Text _shortText;

    [SerializeField]
    [Tooltip("Optional text that displays the full name of the current locale.")]
    private TMP_Text _label;

    [SerializeField]
    [Tooltip("Locales to cycle through, in order. If empty, the project's available locales are used.")]
    private Entry[] _entries;

    private readonly List<Entry> _resolved = new List<Entry>();
    private bool _initialized;

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;

        ResolveEntries();
        Refresh(LocalizationSettings.SelectedLocale);
        LocalizationSettings.SelectedLocaleChanged += Refresh;
        _initialized = true;
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(Press);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(Press);
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= Refresh;
    }

    /// <summary>
    /// Selects the locale after the current one.
    /// </summary>
    public void Press()
    {
        if (!_initialized || _resolved.Count == 0)
        {
            return;
        }

        int index = IndexOf(LocalizationSettings.SelectedLocale);
        LocalizationSettings.SelectedLocale = _resolved[(index + 1) % _resolved.Count].Locale;
    }

    private void ResolveEntries()
    {
        _resolved.Clear();

        if (_entries != null && _entries.Length > 0)
        {
            _resolved.AddRange(_entries);
            return;
        }

        foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
        {
            _resolved.Add(new Entry { Locale = locale });
        }
    }

    private int IndexOf(Locale locale) => _resolved.FindIndex(e => e.Locale == locale);

    private void Refresh(Locale locale)
    {
        int index = IndexOf(locale);
        if (index < 0)
        {
            return;
        }

        Entry entry = _resolved[index];

        if (_shortText != null)
        {
            _shortText.text = string.IsNullOrEmpty(entry.ShortName)
                ? entry.Locale.Identifier.Code.ToUpperInvariant()
                : entry.ShortName;
        }

        if (_label != null)
        {
            _label.text = string.IsNullOrEmpty(entry.FullName) ? NativeName(entry.Locale) : entry.FullName;
        }
    }

    private static string NativeName(Locale locale)
    {
        CultureInfo culture = locale.Identifier.CultureInfo;
        if (culture == null)
        {
            return locale.LocaleName;
        }

        string name = culture.NativeName;
        return name.Length > 0 ? char.ToUpper(name[0], culture) + name.Substring(1) : name;
    }

    private void Reset()
    {
        _button = GetComponent<Button>();
        _shortText = GetComponentInChildren<TMP_Text>();
    }
}
