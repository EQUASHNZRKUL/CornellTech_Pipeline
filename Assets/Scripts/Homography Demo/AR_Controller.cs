using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class AR_Controller : MonoBehaviour
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
    public double[] homo_points = new double[8];
    public static float DATA_SCALE = 0.05f;
    private TrackableId cached_trackableid;

    private Vector3 spawn_nw;
    private Vector3 spawn_ne;
    private Vector3 spawn_sw;
    private Vector3 spawn_se;

    public double[] GetHomopoints()
    {
        return homo_points;
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
    }

    double ScreenToCameraX(double x)
    {
        return (640.0/2200.0) * x;
    }

    double ScreenToCameraY(double y)
    {
        return (320.0/1080.0)*(1080.0 - y) + 80.0;
    }

    void RaycastSpawn()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                bool arRayBool = m_ARRaycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon);
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
                    spawn_nw = spawnedObject.transform.TransformPoint(new Vector3(-DATA_SCALE, 0f, DATA_SCALE));
                    spawn_ne = spawnedObject.transform.TransformPoint(new Vector3(DATA_SCALE, 0f, DATA_SCALE));
                    spawn_sw = spawnedObject.transform.TransformPoint(new Vector3(-DATA_SCALE, 0f, -DATA_SCALE));
                    spawn_se = spawnedObject.transform.TransformPoint(new Vector3(DATA_SCALE, 0f, -DATA_SCALE));
                }
            }
        }

        Camera cam = GameObject.Find("AR Camera").GetComponent<Camera>();

        // Screen Coordinates of corners
        Vector3 cam_nw = cam.WorldToScreenPoint(spawn_nw);
        Vector3 cam_ne = cam.WorldToScreenPoint(spawn_ne);
        Vector3 cam_sw = cam.WorldToScreenPoint(spawn_sw);
        Vector3 cam_se = cam.WorldToScreenPoint(spawn_se);

        // Saving to homo_points
        homo_points[0] = ScreenToCameraX(cam_nw.x);
        homo_points[1] = ScreenToCameraY(cam_nw.y);
        homo_points[2] = ScreenToCameraX(cam_ne.x);
        homo_points[3] = ScreenToCameraY(cam_ne.y);
        homo_points[4] = ScreenToCameraX(cam_sw.x);
        homo_points[5] = ScreenToCameraY(cam_sw.y);
        homo_points[6] = ScreenToCameraX(cam_se.x);
        homo_points[7] = ScreenToCameraY(cam_se.y);

        Debug.LogFormat("Screen Point #0: {0}, {1}", cam_nw.x, cam_nw.y);
        Debug.LogFormat("Screen Point #1: {0}, {1}", cam_ne.x, cam_ne.y);
        Debug.LogFormat("Screen Point #2: {0}, {1}", cam_sw.x, cam_sw.y);
        Debug.LogFormat("Screen Point #3: {0}, {1}", cam_se.x, cam_se.y);

        for (int i = 0; i < 4; i++)
        {
            Debug.LogFormat("Homography Point #{0}: {1} x {2}", i, homo_points[2*i], homo_points[(2*i)+1]);
        }
    }

    void Update()
    {
        RaycastSpawn();
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static List<ARRaycastHit> e_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;
    ARSessionOrigin m_SessionOrigin;
    ARRaycastHit face;
}