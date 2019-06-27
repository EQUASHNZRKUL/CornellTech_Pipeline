using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ARCircleSpawner : MonoBehaviour
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

    [SerializeField]
    GameObject m_ThrownPrefab;
    public GameObject thrownPrefab
    {
        get {return m_ThrownPrefab; }
        set {m_ThrownPrefab = value; }
    }

    public GameObject thrownObject { get; private set; }
    
    [SerializeField]
    GameObject m_CvControllerObject;
    public GameObject CV_Controller_Object 
    {
        get { return m_CvControllerObject; }
        set { m_CvControllerObject = value; } 
    } 

    private CV_Controller m_cv;

    void Awake()
    {
        Debug.Log("StartTest");
        m_ARRaycastManager = GetComponent<ARRaycastManager>();
        m_SessionOrigin = GetComponent<ARSessionOrigin>();
        m_cv = CV_Controller_Object.GetComponent<CV_Controller>();
    }

    void MarkerSpawn()
    {
        // Debug.Log(string.Format("[SCREEN] ray_x: {0}\n ray_y: {1}\n ray_r: {2}", 
        // ray_x, ray_y, ray_r));

        Vector2 ray_pos = m_cv.GetPos();
        // Debug.LogFormat("{0}, {1}", ray_pos, m_cv.GetRad());

        bool arRayBool = m_ARRaycastManager.Raycast(ray_pos, s_Hits, TrackableType.PlaneWithinPolygon);
        bool edgeRayBool = m_ARRaycastManager.Raycast(ray_pos + (new Vector2(m_cv.GetRad(), 0)), e_Hits, TrackableType.PlaneWithinPolygon);
        // Debug.Log(arRayBool);
        if (arRayBool)
        {
            var hit = s_Hits[0];
            var edge = e_Hits[0];
            float dist = Vector3.Distance(hit.pose.position, edge.pose.position);
            // Debug.Log(dist);
            // Debug.Log(spawnedObject.transform.localScale);

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(m_PlacedPrefab, hit.pose.position, hit.pose.rotation);
                spawnedObject.transform.localScale = (new Vector3(dist, dist, dist))*10;
                // Debug.Log(spawnedObject.transform.localScale);
            }
            else
            {
                spawnedObject.transform.position = hit.pose.position;
                spawnedObject.transform.localScale = (new Vector3(dist, dist, dist))*10;
                // Debug.Log(spawnedObject.transform.localScale);
            }
        }
    }

    void Update()
    {
        // Debug.Log(string.Format("[SCREEN] ray_x: {0}\n ray_y: {1}\n ray_r: {2}", 
        // ray_x, ray_y, ray_r));

        Vector2 ray_pos = m_cv.GetPos();
        // Debug.LogFormat("{0}, {1}", ray_pos, m_cv.GetRad());

        bool arRayBool = m_ARRaycastManager.Raycast(ray_pos, s_Hits, TrackableType.PlaneWithinPolygon);
        bool edgeRayBool = m_ARRaycastManager.Raycast(ray_pos + (new Vector2(m_cv.GetRad(), 0)), e_Hits, TrackableType.PlaneWithinPolygon);
        // Debug.Log(arRayBool);
        if (arRayBool)
        {
            var hit = s_Hits[0];
            face = hit;
            var edge = e_Hits[0];
            float dist = Vector3.Distance(hit.pose.position, edge.pose.position);
            // Debug.Log(dist);
            // Debug.Log(spawnedObject.transform.localScale);

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(m_PlacedPrefab, hit.pose.position, hit.pose.rotation);
                spawnedObject.transform.localScale = (new Vector3(dist, dist, dist))*10;
                // Debug.Log(spawnedObject.transform.localScale);
            }
            else
            {
                spawnedObject.transform.position = hit.pose.position;
                spawnedObject.transform.localScale = (new Vector3(dist, dist, dist))*10;
                // Debug.Log(spawnedObject.transform.localScale);
            }
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                Transform cam_transform = GameObject.Find("AR Camera").transform;
                Debug.Log(face.pose.position);
                thrownObject = Instantiate(m_ThrownPrefab, cam_transform.position, face.pose.rotation);
                // Debug.Log(face);
                // thrownObject.GetComponent<Rigidbody>().AddForce(cam_transform.forward * 3);
                // m_SessionOrigin.MakeContentAppearAt(thrownObject.transform, new Vector3(0,0,0));
            }
        }
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static List<ARRaycastHit> e_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;
    ARSessionOrigin m_SessionOrigin;
    ARRaycastHit face;
}