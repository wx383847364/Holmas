using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NestPrefab : MonoBehaviour {

    public GameObject  nestPrefab;
    public bool InstantiatePrefab = true;

    [HideInInspector]
    public GameObject instantiateGameObject;
    // Use this for initialization
    void Awake ()
    {
        if (nestPrefab != null && InstantiatePrefab)
        {
            instantiateGameObject = GameObject.Instantiate(nestPrefab);
            instantiateGameObject.transform.SetParent(transform, false);
            instantiateGameObject.transform.localPosition = Vector3.zero;           
        }
	}
}
