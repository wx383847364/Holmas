using UnityEngine;
using UnityEngine.UI;

//根据Canvas.Scaler调整特效大小，用于适应UI
[ExecuteInEditMode]
class UIEffectAutoModifyScale : MonoBehaviour
{
    [SerializeField]
    private bool updateMode = false;
    [SerializeField]
    private Vector3 localScaler = Vector3.one;

    private void Start()
    {
        transform.localScale = localScaler * UGUIRoot.ScaleFactor;
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!updateMode) return;
        Start(); //Editor下修改屏幕的大小实时预览缩放效果
    }

    void OnValidate()
    {
        if (!updateMode) return;
        transform.localScale = localScaler;
    }
#endif
}

