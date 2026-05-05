using UnityEngine;

namespace App.HotUpdate.Holmas.UI.Core
{
    public sealed class HolmasFontRuntimeSettings : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Res/Font/HolmasFontRuntimeSettings.asset";

        [SerializeField]
        private Font formalBodyFont;

        [SerializeField]
        private Font fallbackFont;

        public Font FormalBodyFont => formalBodyFont;

        public Font FallbackFont => fallbackFont;

        public Font ResolvePreferredProjectFont()
        {
            return formalBodyFont != null ? formalBodyFont : fallbackFont;
        }

        public void Configure(Font formalBody, Font fallback)
        {
            formalBodyFont = formalBody;
            fallbackFont = fallback;
        }
    }
}
