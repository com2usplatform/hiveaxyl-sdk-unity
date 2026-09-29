// Copyright (c) Com2uS Platform Corp. All rights reserved.

#if UNITY_2022_3_OR_NEWER

using System.Globalization;
using UnityEngine;

namespace Hive.Axyl.Core.Unity
{
    /// <summary>
    /// Unity implementation of <see cref="IAppEnvironment"/>.
    /// Wraps Unity engine APIs to provide platform metadata.
    /// <para>
    /// Registered by HiveBootstrap during initialization.
    /// All properties are safe to read from any thread after construction.
    /// </para>
    /// </summary>
    public sealed class UnityAppEnvironment : IAppEnvironment
    {
        private readonly string m_os;
        private readonly string m_environment;
        private readonly string m_osVersion;
        private readonly string m_deviceModel;
        private readonly string m_appVersion;
        private readonly string m_appBundleId;
        private readonly string m_language;

        /// <summary>
        /// Initializes the environment by capturing all platform values.
        /// Must be called on the main thread (Unity API requirement).
        /// </summary>
        public UnityAppEnvironment()
        {
            m_os = GetOS();
            m_environment = Application.isEditor ? "Editor" : "Device";
            m_osVersion = SystemInfo.operatingSystem ?? "";
            m_deviceModel = SystemInfo.deviceModel ?? "";
            m_appVersion = Application.version ?? "";
            m_appBundleId = Application.identifier ?? "";
            m_language = GetBcp47Language();
        }

        /// <inheritdoc/>
        public string Engine => "Unity";

        /// <inheritdoc/>
        public string EngineVersion => Application.unityVersion ?? "";

        /// <inheritdoc/>
        public string OS => m_os;

        /// <inheritdoc/>
        public string Environment => m_environment;

        /// <inheritdoc/>
        public string OSVersion => m_osVersion;

        /// <inheritdoc/>
        public string DeviceModel => m_deviceModel;

        /// <inheritdoc/>
        public string AppVersion => m_appVersion;

        /// <inheritdoc/>
        public string AppBundleId => m_appBundleId;

        /// <inheritdoc/>
        public string Language => m_language;

        private static string GetOS()
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#elif UNITY_STANDALONE_WIN
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "macOS";
#elif UNITY_STANDALONE_LINUX
            return "Linux";
#elif UNITY_WEBGL
            return "WebGL";
#else
            return Application.platform.ToString();
#endif
        }

        private static string GetBcp47Language()
        {
            // Prefer CultureInfo for accurate BCP 47 with region (e.g., "en-GB", "en-AU").
            // Fall back to Application.systemLanguage mapping when CultureInfo is unavailable
            // or returns an invariant/empty culture (common on some Unity platforms).
            var culture = CultureInfo.CurrentUICulture;
            if (culture != null
                && !culture.Equals(CultureInfo.InvariantCulture)
                && !string.IsNullOrEmpty(culture.Name))
            {
                return culture.Name;
            }

            return GetLanguageFallback();
        }

        private static string GetLanguageFallback()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Korean: return "ko-KR";
                case SystemLanguage.English: return "en-US";
                case SystemLanguage.Japanese: return "ja-JP";
                case SystemLanguage.Chinese: return "zh-CN";
                case SystemLanguage.ChineseSimplified: return "zh-CN";
                case SystemLanguage.ChineseTraditional: return "zh-TW";
                case SystemLanguage.French: return "fr-FR";
                case SystemLanguage.German: return "de-DE";
                case SystemLanguage.Spanish: return "es-ES";
                case SystemLanguage.Portuguese: return "pt-BR";
                case SystemLanguage.Russian: return "ru-RU";
                case SystemLanguage.Italian: return "it-IT";
                case SystemLanguage.Thai: return "th-TH";
                case SystemLanguage.Vietnamese: return "vi-VN";
                case SystemLanguage.Indonesian: return "id-ID";
                case SystemLanguage.Turkish: return "tr-TR";
                case SystemLanguage.Arabic: return "ar-SA";
                case SystemLanguage.Dutch: return "nl-NL";
                case SystemLanguage.Polish: return "pl-PL";
                case SystemLanguage.Swedish: return "sv-SE";
                case SystemLanguage.Norwegian: return "nb-NO";
                case SystemLanguage.Danish: return "da-DK";
                case SystemLanguage.Finnish: return "fi-FI";
                case SystemLanguage.Greek: return "el-GR";
                case SystemLanguage.Hebrew: return "he-IL";
                case SystemLanguage.Hungarian: return "hu-HU";
                case SystemLanguage.Czech: return "cs-CZ";
                case SystemLanguage.Romanian: return "ro-RO";
                case SystemLanguage.Ukrainian: return "uk-UA";
                case SystemLanguage.Catalan: return "ca-ES";
                // Unmapped SystemLanguage values fall back to en-US
                default: return "en-US";
            }
        }
    }
}

#endif
