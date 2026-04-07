using UnityEngine.U2D;

namespace Zeus.Framework.UI
{
    [System.Serializable]
    public class UIAtlasElement
    {        
        public string name;
        public SpriteAtlas reference;
        public UIAtlasElement() : this("")
        { }
        public UIAtlasElement(string name)
        {
            this.name = name;
        }
    }
}
