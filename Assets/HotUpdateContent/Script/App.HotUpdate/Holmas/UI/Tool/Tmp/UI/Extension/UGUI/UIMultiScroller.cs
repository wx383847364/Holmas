using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;


namespace Zeus.Framework.UI
{
    //无限循环ScrollView
    public class UIMultiScroller : MonoBehaviour
    {
        public enum Arrangement { Horizontal, Vertical, }
        public Arrangement _movement = Arrangement.Horizontal;

        /// <summary>
        /// 单行或单列的Item数量
        /// </summary>
        [Range(1, 20)]
        public int maxPerLine = 5;

        /// <summary>
        /// 行间距X
        /// </summary>
        [Range(0, 100)]
        public int spacingX = 5;

        /// <summary>
        /// 行间距Y
        /// </summary>
        [Range(0, 100)]
        public int spacingY = 5;


        //Item的宽高
        public int cellWidth = 500;
        public int cellHeight = 100;

        //默认加载的行数，一般比可显示行数大2~3行
        [Range(0, 20)]
        public int viewCount = 6;
        public GameObject itemPrefab;
        public RectTransform _content;
        public ScrollRect _ScrollRect;

        private int _index = -1;
        private List<UIMultiScrollIndex> _itemList;
        private int _dataCount;

        private Queue<int> _instantiateQueue;  //将Instantiate操作平摊到每一帧执行
        private Queue<UIMultiScrollIndex> _unUsedQueue;  //将未显示出来的Item存入未使用队列里面，等待需要使用的时候直接取出

        /// <summary>
        /// Item创建
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="obj">创建的物体</param>
        //[XLua.CSharpCallLua]
        public delegate void OnItemCreateHandler(int index, GameObject obj);

        //第一步 在Lua中 添加监听这个委托(1/2)
        public OnItemCreateHandler OnItemCreate;

        public delegate void OnItemRecycleHandler(int index, GameObject obj);

        public OnItemRecycleHandler OnItemRecycle;


        #region 数据项(Item)的数量
        /// <summary>
        /// 总数量
        /// </summary>
        public int DataCount
        {
            get { return _dataCount; }
            private set { }
        }
        #endregion

        private bool init = false;
        public bool Start
        {
            get { return init; }
            //set { }
        }

        //第二步，Init 设置Item数量并显示 (2/2)
        public void Init(int dataCount)
        {
            if (init) return;
            init = true;

            _dataCount = dataCount;
            UpdateTotalWidth();

            _itemList = new List<UIMultiScrollIndex>();
            _instantiateQueue = new Queue<int>();
            _unUsedQueue = new Queue<UIMultiScrollIndex>();
            OnValueChange(Vector2.zero);
        }

        private void OnDestroy()
        {
            itemPrefab = null;
            _content = null;

            _itemList = null;
            _unUsedQueue = null;
            OnItemCreate = null;
        }


        /// <summary>
        /// 重新设置Scroller
        /// </summary>
        /// <param name="dataCount"></param>
        public void ResetScroller()
        {
            _index = -1;
            ResetPosIndex();

            for (int i = 0; i < _itemList.Count; ++i)
            {
                RecycleItem(_itemList[i]);
            }

            _itemList.Clear();
            OnValueChange(Vector2.zero);
        }

        private void RecycleItem(UIMultiScrollIndex item)
        {
            if (OnItemRecycle != null)
            {
                OnItemRecycle(item.Index, item.gameObject);
            }
            _unUsedQueue.Enqueue(item);
        }

        #region OnValueChange
        public void OnValueChange(Vector2 pos)
        {
            if (_itemList == null) return;

            int index = GetPosIndex();

            if (_index != index && index > -1)
            {
                _index = index;

                for (int i = _itemList.Count; i > 0; i--)
                {
                    UIMultiScrollIndex item = _itemList[i - 1];
                    if (item.Index < index * maxPerLine || (item.Index >= (index + viewCount) * maxPerLine))
                    {
                        _itemList.Remove(item);
                        RecycleItem(item);
                    }
                }
                for (int i = _index * maxPerLine; i < (_index + viewCount) * maxPerLine; i++)
                {
                    if (i < 0) continue;
                    if (i > _dataCount - 1) continue;
                    bool isOk = false;
                    foreach (UIMultiScrollIndex item in _itemList)
                    {
                        if (item.Index == i) isOk = true;
                    }
                    if (isOk) continue;
                    CreateItem(i);

                }
            }
        }
        #endregion

        private void Update()
        {
            if (_instantiateQueue != null && _instantiateQueue.Count > 0)
            {
                UIMultiScrollIndex itemBase;
                int guidIndex = _instantiateQueue.Dequeue();
                GameObject obj = Instantiate(itemPrefab, _content);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localScale = Vector3.one;
                itemBase = obj.GetComponent<UIMultiScrollIndex>();
                if (itemBase == null)
                {
                    itemBase = obj.AddComponent<UIMultiScrollIndex>();
                }

                itemBase.Scroller = this;
                itemBase.Index = guidIndex;
                _itemList.Add(itemBase);

                if (OnItemCreate != null)
                {
                    OnItemCreate(guidIndex, itemBase.gameObject);
                }
            }
        }

        private void CreateItem(int index)
        {

            if (_unUsedQueue.Count > 0)
            {
                UIMultiScrollIndex itemBase;
                itemBase = _unUsedQueue.Dequeue();
                itemBase.Scroller = this;
                itemBase.Index = index;
                _itemList.Add(itemBase);

                if (OnItemCreate != null)
                {
                    OnItemCreate(index, itemBase.gameObject);
                }
            }
            else
            {
                _instantiateQueue.Enqueue(index);
            }
        }

        #region GetPosIndex
        /// <summary>
        /// 获取最上位置的索引
        /// </summary>
        /// <returns></returns>
        private int GetPosIndex()
        {
            int retValue = 0;
            switch (_movement)
            {
                case Arrangement.Horizontal:
                    {
                        retValue = Mathf.FloorToInt(_content.anchoredPosition.x / -(cellWidth + spacingY));
                        break;
                    }
                case Arrangement.Vertical:
                    {
                        retValue = Mathf.FloorToInt(_content.anchoredPosition.y / (cellHeight + spacingY));
                        break;
                    }
            }

            if (retValue < 0) retValue = 0;

            return retValue;
        }
        #endregion

        private void ResetPosIndex()
        {
            Vector2 oldPos = _content.anchoredPosition;
            switch (_movement)
            {
                case Arrangement.Horizontal:
                    {
                        oldPos.x = 0f;
                        break;
                    }
                case Arrangement.Vertical:
                    {
                        oldPos.y = 0f;
                        break;
                    }
            }
            _content.anchoredPosition = oldPos;
        }

        #region GetPosition
        /// <summary>
        /// 根据索引号 获取当前item的位置
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Vector3 GetPosition(int i)
        {
            switch (_movement)
            {
                case Arrangement.Horizontal:
                    return new Vector3(cellWidth * (i / maxPerLine), -(cellHeight + spacingY) * (i % maxPerLine), 0f);
                case Arrangement.Vertical:
                    return new Vector3(cellWidth * (i % maxPerLine) + (i % maxPerLine) * spacingX, -(cellHeight + spacingY) * (i / maxPerLine), 0f);
            }
            return Vector3.zero;
        }
        #endregion

        #region UpdateTotalWidth
        /// <summary>
        /// 这个方法的目的 就是根据总数量 行列 来计算content的真正宽度或者高度
        /// </summary>
        private void UpdateTotalWidth()
        {
            int lineCount = Mathf.CeilToInt((float)_dataCount / maxPerLine);
            switch (_movement)
            {
                case Arrangement.Horizontal:
                    _content.sizeDelta = new Vector2(cellWidth * lineCount + spacingY * (lineCount - 1), _content.sizeDelta.y);
                    break;
                case Arrangement.Vertical:
                    _content.sizeDelta = new Vector2(_content.sizeDelta.x, cellHeight * lineCount + spacingY * (lineCount - 1));
                    break;
            }
        }
        #endregion

        public GameObject GetItemByIndex(int index)
        {
            foreach (UIMultiScrollIndex item in _itemList)
            {
                if (item.Index == index)
                    return item.gameObject;
            }
            return null;
        }

        [SerializeField]
        [Header("可见区域的高度")]
        private float VisibleRegionHeight = 0;

        [SerializeField]
        [Header("可见区域的行数")]
        private int VisibleRegionRows = 0;

        /// <summary>
        /// 根据索引编号 获取这项滚动条在屏幕中间时候的位置
        /// </summary>
        /// <param name="currIndex"></param>
        /// <returns></returns>
        public float GetScrollbarValueByIndex(int currIndex)
        {
            //指定索引的高度 = 这个索引的位置  - 单行的一半 目的是保证行能居中
            float y = GetPosition(currIndex).y - (cellHeight * 0.5f);
            return GetScrollbarValueByPosY(y);
        }

        /// <summary>
        /// 根据y的值 获取滚动条的值
        /// </summary>
        /// <param name="currY"></param>
        /// <returns></returns>
        public float GetScrollbarValueByPosY(float currY)
        {
            float retValue = 1;

            //拖拽区域高度
            float DragRange = Mathf.Max(0f, _content.sizeDelta.y - VisibleRegionHeight);
            if (DragRange > 0 && currY < GetPosition(VisibleRegionRows / 2).y)
            {
                retValue = (1f - (VisibleRegionHeight * -0.5f - currY) / DragRange);
                return Mathf.Max(0.001f, retValue);
            }
            else
            {
                return retValue;
            }
        }
    }
}