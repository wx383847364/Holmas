using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class RawImageAspectRatio : MonoBehaviour {
#if UNITY_EDITOR
    private float StandardRatio = 1.7778f;
    private float WHRatio = 1.7778f;
    private float lastWidth = 0;
    private float lastHeight = 0;
    private RawImage rawImage;


    // Use this for initialization
    void Start () {
        StandardRatio = Screen.width * 1f / Screen.height;
        rawImage = GetComponent<RawImage>();
    }
	
	// Update is called once per frame
	void Update () {
        if (rawImage.rectTransform.sizeDelta.x != lastWidth || rawImage.rectTransform.sizeDelta.y != lastHeight)
        {
            StandardRatio = Screen.width * 1f / Screen.height;
            lastWidth = rawImage.rectTransform.sizeDelta.x;
            lastHeight = rawImage.rectTransform.sizeDelta.y;
            WHRatio = lastWidth / lastHeight;
            Rect rect = rawImage.uvRect;
            rect.x = (StandardRatio - WHRatio) / 2 / StandardRatio;
            rect.width = WHRatio / StandardRatio;
            rawImage.uvRect = rect;
        }
    }
#endif
}
