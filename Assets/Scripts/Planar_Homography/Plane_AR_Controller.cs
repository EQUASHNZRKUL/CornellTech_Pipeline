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

    private Vector3 world_nw;
    private Vector3 world_ne;
    private Vector3 world_sw;
    private Vector3 world_se;

    private Point[] c1_scr_points = new Point[4];
    private Point[] c2_scr_points = new Point[4];

    public Point[] GetScreenpoints(bool c1)
    {
        if (c1)
            return c1_scr_points;
        else 
            return c2_scr_points;
    }

    public Vector3[] GetWorldpoints()
    {
        Vector3[] ret = new Vector3[4];
        ret[0] = world_nw; ret[1] = world_ne; ret[2] = world_sw; ret[3] = world_se;
        return ret;
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

    void RaycastSpawn(Vector2 touchpos)
    {
        // Spawns the Square to extract the screen coordinates in question. 

        bool arRayBool = m_ARRaycastManager.Raycast(touchpos, s_Hits, TrackableType.PlaneWithinPolygon);
        if (arRayBool)
        {
            var hitPose = s_Hits[0].pose;
            if (spawnedObject == null)
                spawnedObject = Instantiate(m_PlacedPrefab, hitPose.position, hitPose.rotation);
            else
                spawnedObject.transform.position = hitPose.position;
                // if (s_Hits[0].trackableId != cached_trackableid)
                //     spawnedObject.transform.rotation = hitPose.rotation;

            // World Coordinates of corners
            world_nw = spawnedObject.transform.TransformPoint(new Vector3(-DATA_SCALE, 0f, DATA_SCALE));
            world_ne = spawnedObject.transform.TransformPoint(new Vector3(DATA_SCALE, 0f, DATA_SCALE));
            world_sw = spawnedObject.transform.TransformPoint(new Vector3(-DATA_SCALE, 0f, -DATA_SCALE));
            world_se = spawnedObject.transform.TransformPoint(new Vector3(DATA_SCALE, 0f, -DATA_SCALE));
        }
    }

    void SetScreenPoints(bool c1)
    {
        Camera cam = GameObject.Find("AR Camera").GetComponent<Camera>();

        // Screen Coordinates of corners
        Vector3 cam_nw = cam.WorldToScreenPoint(world_nw);
        Vector3 cam_ne = cam.WorldToScreenPoint(world_ne);
        Vector3 cam_sw = cam.WorldToScreenPoint(world_sw);
        Vector3 cam_se = cam.WorldToScreenPoint(world_se);

        Point[] scr_array = c1_scr_points;
        if (!c1)
        {
            scr_array = c2_scr_points;
        }

        scr_array[0] = new Point(ScreenToCameraX(cam_nw.x), ScreenToCameraY(cam_nw.y));
        scr_array[1] = new Point(ScreenToCameraX(cam_ne.x), ScreenToCameraY(cam_ne.y));
        scr_array[2] = new Point(ScreenToCameraX(cam_sw.x), ScreenToCameraY(cam_sw.y));
        scr_array[3] = new Point(ScreenToCameraX(cam_se.x), ScreenToCameraY(cam_se.y));

        Debug.LogFormat("Screen Point #0: {0}, {1}", cam_nw.x, cam_nw.y);
        Debug.LogFormat("Screen Point #1: {0}, {1}", cam_ne.x, cam_ne.y);
        Debug.LogFormat("Screen Point #2: {0}, {1}", cam_sw.x, cam_sw.y);
        Debug.LogFormat("Screen Point #3: {0}, {1}", cam_se.x, cam_se.y);

        for (int i = 0; i < 4; i++)
        {
            Debug.LogFormat("Homography Point #{0}: {1}", i, c1_scr_points[i]);
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
                RaycastSpawn(touch.position);
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