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
/// Class containing AR-related methods for ThreeStage_CV_Controller in Detector\ Homography.unity. 
/// * placedPrefab [GameObject] - GameObject spawned at RaycastHit world point from Detected Aruco placement.
/// * cameraObject [Camera] - Main camera used by device for AR-related functions. 
/// * POINT_COUNT [int] - (static) number of ArUco markers in use. By default 7. Changing this value may break things. 
/// * camerapos_array [List<Vector3>] - List of cached camera world positions. Used for orientation-dependent textures. 
/// - Note: Each of the following 'Point Arrays' has corresponding array indices. world_point[i] and c1_scr_points[i] refer to the same ArUco Marker. 
/// * world_points [Vector3[POINT_COUNT]] - Array of calculated world_points of ArUco markers indexed by (src) id. 
/// * c1_scr_points [Point[POINT_COUNT]] - Screen Points of ArUco markers from C1 Perspective (Initial Capture).
/// * c2_scr_points [Point[POINT_COUNT]] - Screen Points of ArUco markers from C2 Perspective (Warped Output)
/// * spawnedObjects [GameObject[POINT_COUNT]] - Game Objects corresponding to each world_point & instantiated from m_PlacedPrefab
public class ThreeStage_AR_Controller : MonoBehaviour
{
    #region Required Game Objects
    [SerializeField]
    [Tooltip("Instantiates this prefab on a gameObject at the touch location.")]
    GameObject m_PlacedPrefab;
    public GameObject placedPrefab
    {
        get { return m_PlacedPrefab; }
        set { m_PlacedPrefab = value; }
    }

    [SerializeField]
    Camera m_cam;
    public Camera cameraObject 
    {
        get { return m_cam; }
        set { m_cam = value; } 
    } 
    #endregion

    #region Private variables
    // Constants
    private static int POINT_COUNT = 7; 

    // Lists & Arrays
    private List<Vector3> camerapos_array = new List<Vector3>();
    private Vector3[] world_points = new Vector3[POINT_COUNT];
    private Point[] c1_scr_points = new Point[POINT_COUNT];
    private Point[] c2_scr_points = new Point[POINT_COUNT];
    private GameObject[] spawnedObjects = new GameObject[POINT_COUNT];
    #endregion

    #region Simple Helpers
    // Public Getter for [c2_scr_points]
    public Point[] GetScreenpoints() { return c2_scr_points; }

    // Counter for nulls in [world_points] array. Iterates through array and accumulates
    // total number of undetected ArUco markers. 
    //
    // Returns: number of null values in [world_points]. 
    public int count_world_nulls() {
        int acc = 0; 
        for (int i = 0; i < POINT_COUNT; i++)
        {
            if (world_points[i] == null) {
                acc++; 
            }
        }
        return acc; 
    }

    // (Public Method) Checks if number of undetected world point locations of ArUco markers
    // is zero. 
    //
    // Returns: True if all ArUco world points have been calculated. False otherwise. 
    public bool WorldFull() { return (count_world_nulls() == 0); }
    #endregion

    #region Conversion Functions
    // Conversion functions from Camera to Pixel Coordinates for X and Y values.
    // Note: Conversion for X and Y not simply scaling values due to different aspect ratios
    // Note: Pixel Coordinates are specific to Galaxy Note 8. Will differ from device to device.  
    // - [Camera Coord System] used by OpenCV Mats (640 x 480)
    // - [Screen Coord System] used by device Screen and ARRaycast (2200 x 1080)
    float PixelToCameraX(double x) { return (float) ((640.0/2200.0) * x); }
    float PixelToCameraY(double y) { return (float) ((320.0/1080.0)*(1080.0 - y) + 80.0); }
    float CameraToPixelX(double x) { return (float) (3.4375 * x); }
    float CameraToPixelY(double y) { return (float) (1080.0 - (3.375*(y - 80.0))); }
    #endregion

    #region Public Methods
    // (Public Method) Takes current C1 screen points from CV_Controller and finds World coordinates 
    // for each with ARFoundation Detected Planes and Raycasting. 
    // 
    // Takes ephemeral C1 values from CV Controller in Camera Space and caches in [c1_scr_points].
    // Converts each C1 point into Pixel Coordinates and sends ARRaycast to that point, storing in [s_Hits]. 
    // Each world_point is taken to be the first value in [s_Hits] (the closest detected collision with 
    // an AR Plane) and is cached in [world_points]. A corresponding spawnedObject is also moved to the 
    // collision point. 
    public void SetWorldPoints()
    {
        ThreeStage_CV_Controller CV_Controller = GameObject.Find("CV_Controller").GetComponent<ThreeStage_CV_Controller>();
        c1_scr_points = CV_Controller.GetRecentC1Points();

        for (int i = 0; i < POINT_COUNT; i++) {
            if (c1_scr_points[i] != null) {
                Vector2 screen_vec = 
                    new Vector2(CameraToPixelX(c1_scr_points[i].x), CameraToPixelY(c1_scr_points[i].y));
                bool arRayBool = m_ARRaycastManager.Raycast(screen_vec, s_Hits, TrackableType.PlaneWithinPolygon);
                if (arRayBool) {
                    world_points[i] = s_Hits[0].pose.position; 
                    spawnedObjects[i].transform.position = world_points[i];
                }
            }
        }
    }

    // (Public Method) Sets the C2 screen point values from cached world points. 
    // 
    // Iterates through non-null entries in [world_points] and uses Unity's Camera.WorldToScreenPoint() 
    // function to convert World point value to screen point of the camera [m_cam] at current camera
    // position and orientation. 
    public void SetScreenPoints()
    {
        for (int i = 0; i < POINT_COUNT; i++)
        {
            if (world_points[i] != null) {
                Vector3 scr_point = m_cam.WorldToScreenPoint(world_points[i]);
                c2_scr_points[i] = new Point(PixelToCameraX(scr_point.x), PixelToCameraY(scr_point.y));
            }
        }
    }

    // Caches the camera's world points when textures are captured. 
    public void CacheCamPoints()
    {
        camerapos_array.Add(m_cam.transform.position);
    }

    public int GetClosestIndex() {
        Vector3 curr_cam = m_cam.transform.position;

        int min_i = 0; 
        int i = 0; 
        float min_dist = Vector3.Distance(curr_cam, camerapos_array[0]);
        foreach (Vector3 camera_pos in camerapos_array) {
            i++; 
            float dist = Vector3.Distance(curr_cam, camera_pos); 
            if (dist < min_dist) {
                min_dist = dist; 
                min_i = i; 
            }
        }
        return min_i; 
    }
    #endregion

    #region Unity Methods
    void Awake()
    {
        Debug.Log("StartTest");
        m_ARRaycastManager = GetComponent<ARRaycastManager>();
        m_SessionOrigin = GetComponent<ARSessionOrigin>();
        spawnedObjects[0] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[1] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[2] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[3] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[4] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[5] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
        spawnedObjects[6] = Instantiate(m_PlacedPrefab, new Vector3(0.0f, 0.0f, 0.0f), new Quaternion(0.0f, 0.0f, 0.0f, 0.0f));
    }

    void Update() {}
    #endregion

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
    static List<ARRaycastHit> e_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;
    ARSessionOrigin m_SessionOrigin;
    ARRaycastHit face;
}