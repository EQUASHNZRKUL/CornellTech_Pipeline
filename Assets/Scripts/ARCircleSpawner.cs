using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ARCircleSpawner : MonoBehavior
{
    [SerializeField]
    ARCameraManager m_ARCameraManager;
    public ARCameraManager cameraManager
    {
        get {return m_ARCameraManager; }
        set {m_ARCameraManager = value; }
    }

    [SerializeField]
    [Tooltip("Instantiates this prefab on a gameObject at the touch location.")]
    GameObject m_PlacedPrefab;
    public GameObject placedPrefab
    {
        get { return m_PlacedPrefab; }
        set { m_PlacedPrefab = value; }
    }

    public GameObject spawnedObject { get; private set; }
    public Circle_Spawner circle_detector; 

    void Awake()
    {
        Debug.Log("StartTest");
        m_ARRaycastManager = GetComponent<ARRaycastManager>();
    }

    void SendRaycastToPoint()
    {
        // Debug.Log(string.Format("[SCREEN] ray_x: {0}\n ray_y: {1}\n ray_r: {2}", 
        // ray_x, ray_y, ray_r));

        bool arRayBool = m_ARRaycastManager.Raycast(new Vector2(circle_detector.ray_x, circle_detector.ray_y), s_Hits, TrackableType.PlaneWithinPolygon);
        if (arRayBool)
        {
            var hit = s_Hits[0];
            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(m_PlacedPrefab, hit.pose.position, hit.pose.rotation);
            }
            else
            {
                spawnedObject.transform.position = hit.pose.position;
            }
        }
    }

    void Update()
    {
        if (circle_detector.blob_r == -1)
            return;
    }
}