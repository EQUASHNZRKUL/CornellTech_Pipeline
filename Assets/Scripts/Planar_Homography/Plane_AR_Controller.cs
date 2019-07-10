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
public class Plane_AR_Controller : MonoBehaviour
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

    public Vector3[] world_points = new Vector3[4];

    private Point[] c1_scr_points = new Point[4];
    private Point[] c2_scr_points = new Point[4];

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
    }

    void MarkerSpawn()
    {
        Vector2 ray_pos = m_cv.GetPos();

        bool arRayBool = m_ARRaycastManager.Raycast(ray_pos, s_Hits, TrackableType.PlaneWithinPolygon);
        bool edgeRayBool = m_ARRaycastManager.Raycast(ray_pos + (new Vector2(m_cv.GetRad(), 0)), e_Hits, TrackableType.PlaneWithinPolygon);

        if (arRayBool)
        {
            var hit = s_Hits[0];
            face = hit;
            var edge = e_Hits[0];
            float dist = Vector3.Distance(hit.pose.position, edge.pose.position);

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(m_PlacedPrefab, hit.pose.position, hit.pose.rotation);
                spawnedObject.transform.localScale = (new Vector3(dist, dist, dist))*10;
            }
            else
            {
                spawnedObject.transform.position = hit.pose.position;
                spawnedObject.transform.localScale = (new Vector3(dist, dist, dist))*10;
            }
        }
    }

    float ScreenToCameraX(double x)
    {
        return (float) ((640.0/2200.0) * x);
    }

    float ScreenToCameraY(double y)
    {
        return (float) ((320.0/1080.0)*(1080.0 - y) + 80.0);
    }

    void SetWorldPoints()
    {
        Plane_CV_Controller CV_Controller = GameObject.Find("CV_Controller").GetComponent<Plane_CV_Controller>();
        Point[] c1_points = CV_Controller.GetC1Points();

        // for (int i = 0; i < c1_points.Length; i++)
        for (int i = 0; i < 4; i++)
        {
            Point screen_point = c1_points[i];
            Vector2 screen_vec = new Vector2((float) screen_point.x, (float) screen_point.y);
            bool arRayBool = m_ARRaycastManager.Raycast(screen_vec, s_Hits, TrackableType.PlaneWithinPolygon);
            world_points[i] = s_Hits[0].pose.position; 
        }
    }

    void SetScreenPoints(bool c1)
    {
        Camera cam = GameObject.Find("AR Camera").GetComponent<Camera>();

        for (int i = 0; i < 4; i++)
        {
            Vector3 scr_point = cam.WorldToScreenPoint(world_points[i]);
            c2_scr_points[i] = new Point(ScreenToCameraX(scr_point.x), ScreenToCameraY(scr_point.y));
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
                // RaycastSpawn(touch.position);
                SetWorldPoints(); 
                SetScreenPoints(true);
            }
        }

        // FRAME SECTION
        SetScreenPoints(false);
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static List<ARRaycastHit> e_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;
    ARSessionOrigin m_SessionOrigin;
    ARRaycastHit face;
}