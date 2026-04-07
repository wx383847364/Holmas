using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class HrefClickEvent:UnityEvent<string> { }

public class EmojiText : Text, IPointerClickHandler
{
    private const bool EMOJI_LARGE = true;
    public  Dictionary<string, EmojiInfo> EmojiIndex = null;
    private TextAsset EmojiTextAsset;//配置文本
    private bool EmojiTextAssetIsDirty = false;

    public struct EmojiInfo
    {
        public float x;
        public float y;
        public float size;
        public int len;
    }

    [TextArea(3, 10)]
    [SerializeField]
    public string m_RichText = string.Empty;
    public override string text
    {
        get
        {
            return m_Text;
        }
        set
        {
            // Debug.LogWarning("<color=#ffff00>m_RichText================</color>\n" + value);
            if (String.IsNullOrEmpty(value))
            {
                if (String.IsNullOrEmpty(m_Text) || String.IsNullOrEmpty(m_RichText))
                    return;
                m_RichText = "";
                m_Text = GetOutputText("");
                SetVerticesDirty();
            }
            else if (m_RichText != value)
            {
                m_RichText = value;
                m_Text = GetOutputText(value);
                SetVerticesDirty();
                SetLayoutDirty();
            }
            // Debug.LogWarning("<color=#0000ff>m_Text================</color>\n" + m_Text);
        }
    }

    //设置配置文本
    public void SetEmojiConfig(TextAsset textAsset)
    {
        if (EmojiTextAsset != textAsset)
        {
            EmojiTextAsset = textAsset;
            EmojiTextAssetIsDirty = true;
        }
    }

    readonly UIVertex[] m_TempVerts = new UIVertex[4];
    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        if (font == null)
            return;

        if (EmojiTextAsset != null && EmojiTextAssetIsDirty)
        {
            if (EmojiIndex == null)
            {
                EmojiIndex = new Dictionary<string, EmojiInfo>();
            }
            else
            {
                EmojiIndex.Clear();
            }
            EmojiTextAssetIsDirty = false;
            //load emoji data, and you can overwrite this segment code base on your project.

            string[] lines = EmojiTextAsset.text.Split ('\n');
            for(int i = 1 ; i < lines.Length; i ++)
            {
                if (! string.IsNullOrEmpty (lines [i])) {
                    string[] strs = lines [i].Split ('\t');
                    EmojiInfo info;
                    info.x = float.Parse (strs [3]);
                    info.y = float.Parse (strs [4]);
                    info.size = float.Parse (strs [5]);
                    info.len = 0;
                    EmojiIndex.Add (strs [1], info);
                }
            }
        }

        //key是标签在字符串中的索引

        Dictionary<int,EmojiInfo> emojiDic = new Dictionary<int, EmojiInfo> ();
        if (supportRichText && EmojiTextAsset != null) {
            MatchCollection matches = Regex.Matches (text, "\\[[a-z0-9A-Z]+\\]");//把表情标签全部匹配出来
            for (int i = 0; i < matches.Count; i++) {
                EmojiInfo info;
                if (EmojiIndex.TryGetValue (matches [i].Value, out info)) {
                    info.len = matches [i].Length;
                    emojiDic.Add (matches [i].Index, info);
                }
            }
        }

        // We don't care if we the font Texture changes while we are doing our Update.
        // The end result of cachedTextGenerator will be valid for this instance.
        // Otherwise we can get issues like Case 619238.
        m_DisableFontTextureRebuiltCallback = true;

        Vector2 extents = rectTransform.rect.size;

        var settings = GetGenerationSettings(extents);
        cachedTextGenerator.Populate(text, settings);

        Rect inputRect = rectTransform.rect;

        // get the text alignment anchor point for the text in local space
        Vector2 textAnchorPivot = GetTextAnchorPivot(alignment);
        Vector2 refPoint = Vector2.zero;
        refPoint.x = Mathf.Lerp(inputRect.xMin, inputRect.xMax, textAnchorPivot.x);
        refPoint.y = Mathf.Lerp(inputRect.yMin, inputRect.yMax, textAnchorPivot.y);

        // Determine fraction of pixel to offset text mesh.
        Vector2 roundingOffset = PixelAdjustPoint(refPoint) - refPoint;

        // Apply the offset to the vertices
        IList<UIVertex> verts = cachedTextGenerator.verts;
        float unitsPerPixel = 1 / pixelsPerUnit;
        //Last 4 verts are always a new line...
        int vertCount = verts.Count - 4;

        toFill.Clear();
        //  仅在文本中没有表情时走该逻辑
        if (emojiDic.Count == 0 && roundingOffset != Vector2.zero)
        {
            for (int i = 0; i < vertCount; ++i)
            {
                int tempVertsIndex = i & 3;
                m_TempVerts[tempVertsIndex] = verts[i];
                m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                m_TempVerts[tempVertsIndex].position.x += roundingOffset.x;
                m_TempVerts[tempVertsIndex].position.y += roundingOffset.y;
                if (tempVertsIndex == 3)
                    toFill.AddUIVertexQuad(m_TempVerts);
            }
        }
        else
        {
            float repairDistance = 0;
            float repairDistanceHalf = 0;
            float repairY = 0;
            if (vertCount > 0) {
                repairY = verts [3].position.y;
            }
            for (int i = 0; i < vertCount; ++i) {
                EmojiInfo info;
                int index = i / 4;//每个字符4个顶点
                if (emojiDic.TryGetValue (index, out info)) {//这个顶点位置是否为表情开始的index

                    HrefInfosIndexAdjust(i);//矫正一下超链接的Index

                    //compute the distance of '[' and get the distance of emoji
                    //计算表情标签2个顶点之间的距离， * 3 得出宽度（表情有3位）
                    float charDis = (verts [i + 1].position.x - verts [i].position.x) * 3;
                    m_TempVerts [3] = verts [i];//1
                    m_TempVerts [2] = verts [i + 1];//2
                    m_TempVerts [1] = verts [i + 2];//3
                    m_TempVerts [0] = verts [i + 3];//4

                    //the real distance of an emoji
                    m_TempVerts [2].position += new Vector3 (charDis, 0, 0);
                    m_TempVerts [1].position += new Vector3 (charDis, 0, 0);

                    float fixWidth = m_TempVerts[2].position.x - m_TempVerts[3].position.x;
                    float fixHeight = (m_TempVerts[2].position.y - m_TempVerts[1].position.y);
                    //make emoji has equal width and height
                    float fixValue = (fixWidth - fixHeight);//把宽度变得跟高度一样
                    m_TempVerts [2].position -= new Vector3 (fixValue, 0, 0);
                    m_TempVerts [1].position -= new Vector3 (fixValue, 0, 0);

                    float curRepairDis = 0;
                    if (verts [i].position.y < repairY) {// to judge current char in the same line or not
                        repairDistance = repairDistanceHalf;
                        repairDistanceHalf = 0;
                        repairY = verts [i + 3].position.y;
                    }
                    curRepairDis = repairDistance;
                    int dot = 0;//repair next line distance
                    for (int j = info.len - 1; j > 0; j--) {
                        int infoIndex = i + j * 4 + 3;
                        if (verts.Count > infoIndex && verts[infoIndex].position.y >= verts [i + 3].position.y) {
                            repairDistance += verts [i + j * 4 + 1].position.x - m_TempVerts [2].position.x;
                            break;
                        } else {
                            dot = i + 4 * j;

                        }
                    }
                    if (dot > 0) {
                        int nextChar = i + info.len * 4;
                        if (nextChar < verts.Count) {
                            repairDistanceHalf = verts [nextChar].position.x - verts [dot].position.x;
                        }
                    }

                    //repair its distance
                    for (int j = 0; j < 4; j++) {
                        m_TempVerts [j].position -= new Vector3 (curRepairDis, 0, 0);
                    }

                    m_TempVerts [0].position *= unitsPerPixel;
                    m_TempVerts [1].position *= unitsPerPixel;
                    m_TempVerts [2].position *= unitsPerPixel;
                    m_TempVerts [3].position *= unitsPerPixel;

                    float pixelOffset = emojiDic [index].size / 32 / 2;
                    m_TempVerts [0].uv1 = new Vector2 (emojiDic [index].x + pixelOffset, emojiDic [index].y + pixelOffset);
                    m_TempVerts [1].uv1 = new Vector2 (emojiDic [index].x - pixelOffset + emojiDic [index].size, emojiDic [index].y + pixelOffset);
                    m_TempVerts [2].uv1 = new Vector2 (emojiDic [index].x - pixelOffset + emojiDic [index].size, emojiDic [index].y - pixelOffset + emojiDic [index].size);
                    m_TempVerts [3].uv1 = new Vector2 (emojiDic [index].x + pixelOffset, emojiDic [index].y - pixelOffset + emojiDic [index].size);

                    toFill.AddUIVertexQuad (m_TempVerts);

                    i += 4 * info.len - 1;
                } else {
                    int tempVertsIndex = i & 3;
                    if (tempVertsIndex == 0 && verts [i].position.y < repairY) {
                        repairY = verts [i + 3].position.y;
                        repairDistance = repairDistanceHalf;
                        repairDistanceHalf = 0;
                    }
                    m_TempVerts [tempVertsIndex] = verts [i];
                    m_TempVerts [tempVertsIndex].position -= new Vector3 (repairDistance, 0, 0);
                    m_TempVerts [tempVertsIndex].position *= unitsPerPixel;
                    if (tempVertsIndex == 3)
                        toFill.AddUIVertexQuad (m_TempVerts);
                }
            }
        }
        m_DisableFontTextureRebuiltCallback = false;

        UIVertex vert = new UIVertex();
        // 处理超链接包围框
        foreach (var hrefInfo in m_HrefInfos)
        {
            hrefInfo.boxes.Clear();
            if (hrefInfo.startIndex >= toFill.currentVertCount)
            {
                continue;
            }
            // 将超链接里面的文本顶点索引坐标加入到包围框
            toFill.PopulateUIVertex(ref vert, hrefInfo.startIndex);
            var pos = vert.position;
            var bounds = new Bounds(pos, Vector3.zero);
            for (int i = hrefInfo.startIndex, m = hrefInfo.endIndex; i < m; i++)
            {
                if (i >= toFill.currentVertCount)
                {
                    break;
                }

                toFill.PopulateUIVertex(ref vert, i);
                pos = vert.position;
                if (pos.x < bounds.min.x) // 换行重新添加包围框
                {
                    hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
                    bounds = new Bounds(pos, Vector3.zero);
                }
                else
                {
                    bounds.Encapsulate(pos); // 扩展包围框
                }
            }
            hrefInfo.boxes.Add(new Rect(bounds.min, bounds.size));
        }
    }

    /// <summary>
    /// 超链接正则
    /// </summary>
    public static readonly Regex s_HrefRegex =
        new Regex(@"<a href=([^>\n\s]+)>(.*?)(</a>)", RegexOptions.Singleline);

    /// <summary>
    /// 超链接信息列表
    /// </summary>
    private readonly List<HrefInfo> m_HrefInfos = new List<HrefInfo>();

    /// <summary>
    /// 文本构造器
    /// </summary>
    protected static readonly StringBuilder s_TextBuilder = new StringBuilder();
    /// <summary>
    /// 获取超链接解析后的最后输出文本
    /// </summary>
    /// <returns></returns>
    protected virtual string GetOutputText(string inputText)
    {
        // Debug.LogWarning("<color=#ffff00>GetOutputText->inputText================</color>\n" + inputText);
        s_TextBuilder.Length = 0;
        m_HrefInfos.Clear();
        var indexText = 0;

        foreach (Match match in s_HrefRegex.Matches(inputText))
        {
            s_TextBuilder.Append(inputText.Substring(indexText, match.Index - indexText));

            var group = match.Groups[1];
            var hrefInfo = new HrefInfo
            {
                startIndex = s_TextBuilder.Length * 4, // 超链接里的文本起始顶点索引
                endIndex = (s_TextBuilder.Length + match.Groups[2].Length - 1) * 4 + 3,
                href = group.Value
            };
            m_HrefInfos.Add(hrefInfo);

            s_TextBuilder.Append(match.Groups[2].Value);
            indexText = match.Index + match.Length;
        }

        s_TextBuilder.Append(inputText.Substring(indexText, inputText.Length - indexText));
        return s_TextBuilder.ToString();
    }

    private void HrefInfosIndexAdjust(int imgIndex)
    {
        foreach (var hrefInfo in m_HrefInfos)//如果后面有超链接，需要把位置往前挪
        {
            if (imgIndex < hrefInfo.startIndex)
            {
                hrefInfo.startIndex -= 8;
                hrefInfo.endIndex -= 8;
            }
        }
    }

    public HrefClickEvent m_OnHrefClick = new HrefClickEvent();
    public HrefClickEvent onHrefClick
    {
        get { return m_OnHrefClick; }
        set { m_OnHrefClick = value; }
    }

    /// <summary>
    /// 点击事件检测是否点击到超链接文本
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position, eventData.pressEventCamera, out lp);

        // Debug.Log("OnPointerClick>>>>>>>" + lp + eventData);
        foreach (var hrefInfo in m_HrefInfos)
        {
            var boxes = hrefInfo.boxes;
            for (var i = 0; i < boxes.Count; ++i)
            {
                // Debug.Log("boxes.Count>>>>Contains>>>" + lp + boxes[i]);
                if (boxes[i].Contains(lp))
                {
// Debug.Log("OnPointerClick>>>>>>>onHrefClick>>>>>" + hrefInfo.href);
                    m_OnHrefClick.Invoke(hrefInfo.href);
                    return;
                }
            }

        }
    }

    /// <summary>
    /// 超链接信息类
    /// </summary>
    private class HrefInfo
    {
        public int startIndex;
        public int endIndex;
        public string href;
        public readonly List<Rect> boxes = new List<Rect>();
    }
}

