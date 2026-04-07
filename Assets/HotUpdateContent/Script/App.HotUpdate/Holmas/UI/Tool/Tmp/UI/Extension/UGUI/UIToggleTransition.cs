using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIToggleTransition : MonoBehaviour
{
    //public UnityEngine.UI.Selectable.Transition transition;
    public string selectedTrigger = "Pressed";
    public string unselectedTrigger = "Normal";

    public GameObject selectedObj;

    private Animator animator;
    private Toggle toggle;
    // Use this for initialization
    void Awake()
    {
        Init();
    }

    public void Init()
    {
        animator = GetComponent<Animator>();
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(OnValueChanged);
    }

    public void OnEnable()
    {
        OnValueChanged(toggle.isOn);
    }

    public void OnValueChanged(bool isSelected)
    {
        if (animator != null)
        {
            if (isSelected)
            {
                animator.Play(selectedTrigger);
            }
            else
            {
                animator.Play(unselectedTrigger);
            }
        }

        if (selectedObj != null)
        {
            selectedObj.SetActive(isSelected);
        }
    }
}
