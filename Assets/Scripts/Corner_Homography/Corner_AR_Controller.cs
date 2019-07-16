using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using OpenCVForUnity.CoreModule;

[RequireComponent(typeof(ARRaycastManager))]
public class Corner_AR_Controller : MonoBehaviour
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
    GameObject m_CvControllerObject;
    public GameObject CV_Controller_Object 
    {
        get { return m_CvControllerObject; }
        set { m_CvControllerObject = value; } 
    } 

    private CV_Controller m_cv;
    public static float DATA_SCALE = 0.05f;
    private TrackableId cached_trackableid;

    private Vector3[] world_points = new Vector3[4];

    private Point[] c1_scr_points = new Point[4];
    private Point[] c2_scr_points = new Point[4];

    private GameObject[] spawnedObjects = new GameObject[4];

    public Point[] GetScreenpoints(bool c1)
    {
        if (c1)
            return c1_scr_points;
        else 
            return c2_scr_points;
    }

    void Awake()
    {
        Debug.Log("StartTest");
        m_ARRaycastManager = GetComponent<ARRaycastManager>();
        m_SessionOrigin = GetComponent<ARSessionOrigin>();
        m_cv = CV_Controller_Object.GetComponent<CV_Controller>();
        spawnedObjects[0] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[1] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[2] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[3] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));

    }

    float PixelToCameraX(double x)
    {
        return (float) ((640.0/2200.0) * x);
    }

    float PixelToCameraY(double y)
    {
        return (float) ((320.0/1080.0)*(1080.0 - y) + 80.0);
    }

    float CameraToPixelX(double x)
    {
        return (float) (3.4375 * x);
    }

    float CameraToPixelY(double y)
    {
        return (float) (1080.0 - (3.375*(y - 80.0)));
    }

    void SetWorldPoints()
    {
        Corner_CV_Controller CV_Controller = GameObject.Find("CV_Controller").GetComponent<Corner_CV_Controller>();
        c1_scr_points = CV_Controller.GetC1Points();

        for (int i = 0; i < 4; i++)
        {
            // Point mat_point = c1_points[i];
            Vector2 screen_vec = new Vector2(CameraToPixelX(c1_scr_points[i].x), CameraToPixelY(c1_scr_points[i].y));
            bool arRayBool = m_ARRaycastManager.Raycast(screen_vec, s_Hits, TrackableType.PlaneWithinPolygon);
            world_points[i] = s_Hits[0].pose.position; 
            spawnedObjects[i].transform.position = world_points[i];
        }
    }

    // Sets the C2 screen point values from world points
    void SetScreenPoints()
    {
        Camera cam = GameObject.Find("AR Camera").GetComponent<Camera>();

        for (int i = 0; i < 4; i++)
        {
            Vector3 scr_point = cam.WorldToScreenPoint(world_points[i]);
            c2_scr_points[i] = new Point(PixelToCameraX(scr_point.x), PixelToCameraY(scr_point.y));
        }
    }

    void Update()
    {
        // TOUCH SECTION
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                // Cache worldpoints 
                SetWorldPoints(); 
            }
        }

        // FRAME SECTION
        SetScreenPoints();
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static List<ARRaycastHit> e_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;
    ARSessionOrigin m_SessionOrigin;
    ARRaycastHit face;
}